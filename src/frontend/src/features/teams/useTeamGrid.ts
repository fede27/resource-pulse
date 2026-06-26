import { useMemo, useState } from 'react';
import { useQueries } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import dayjs, { type Dayjs } from 'dayjs';
import { useTeamsGetAll } from '@/api/generated/teams/teams';
import {
  getResourcesGetCapacityQueryOptions,
  useResourcesGetAll,
} from '@/api/generated/resources/resources';
import { getLoadGetResourceLoadQueryOptions } from '@/api/generated/load/load';
import { useRolesGetAll } from '@/api/generated/roles/roles';
import { useLoadBandsGet } from '@/api/generated/load-bands/load-bands';
import { useBucketingGet } from '@/api/generated/bucketing/bucketing';
import type {
  DailyCapacityDto,
  LoadResult,
  ResourceReadDto,
  RoleReadDto,
  TeamReadDto,
} from '@/api/generated/schemas';
import type { DailyLoadDto } from '@/api/load-types';
import {
  buildBuckets,
  fetchWindow,
  grainOf,
  groupRuns,
  membersSampler,
  normalizeBands,
  parseDurationHours,
  resourceSampler,
  rowLoads,
  todayBucketIdx,
  type Bucket,
  type BucketLoad,
  type Grain,
  type Group,
  type LoadBand,
  type ResourceSeries,
} from './loadModel';

const listOf = <T>(data: unknown): T[] => ((data as LoadResult | undefined)?.data ?? []) as T[];

export type TeamGrid = {
  today: Dayjs;
  bands: LoadBand[];
  grain: Grain;
  setGrain: (g: Grain) => void;
  primaryGrain: Grain;
  secondaryGrain: Grain;
  buckets: Bucket[];
  groups: Group[];
  todayIdx: number;

  teams: TeamReadDto[];
  allResources: ResourceReadDto[];
  resourcesById: Record<string, ResourceReadDto>;
  roleNameById: Record<string, string>;
  membersByTeam: Record<string, string[]>;
  unassigned: string[];

  overall: BucketLoad[];
  teamLoads: Record<string, BucketLoad[]>;
  personLoads: Record<string, BucketLoad[]>;

  isLoading: boolean;
  isSeriesFetching: boolean;
};

export function useTeamGrid(): TeamGrid {
  const { i18n } = useTranslation();
  // Anchored once at mount so the fetch window and query keys stay stable.
  const today = useMemo(() => dayjs(), []);
  const window = useMemo(() => fetchWindow(today), [today]);
  const fromISO = window.from.format('YYYY-MM-DD');
  const toISO = window.to.format('YYYY-MM-DD');

  const teamsQuery = useTeamsGetAll();
  const resourcesQuery = useResourcesGetAll();
  const rolesQuery = useRolesGetAll();
  const bandsQuery = useLoadBandsGet();
  const bucketingQuery = useBucketingGet();

  const teams = useMemo(
    () =>
      listOf<TeamReadDto>(teamsQuery.data)
        .slice()
        .sort((a, b) => (a.name ?? '').localeCompare(b.name ?? '')),
    [teamsQuery.data],
  );
  const allResources = useMemo(
    () => listOf<ResourceReadDto>(resourcesQuery.data),
    [resourcesQuery.data],
  );
  const roleNameById = useMemo(() => {
    const out: Record<string, string> = {};
    listOf<RoleReadDto>(rolesQuery.data).forEach((r) => {
      if (r.id && r.name) out[r.id] = r.name;
    });
    return out;
  }, [rolesQuery.data]);

  const resourcesById = useMemo(() => {
    const out: Record<string, ResourceReadDto> = {};
    allResources.forEach((r) => {
      if (r.id) out[r.id] = r;
    });
    return out;
  }, [allResources]);

  const teamIdSet = useMemo(() => new Set(teams.map((t) => t.id).filter(Boolean) as string[]), [teams]);

  const membersByTeam = useMemo(() => {
    const out: Record<string, string[]> = {};
    teams.forEach((t) => {
      if (t.id) out[t.id] = [];
    });
    allResources.forEach((r) => {
      if (r.id && r.teamId && out[r.teamId]) out[r.teamId]!.push(r.id);
    });
    Object.values(out).forEach((ids) =>
      ids.sort((a, b) =>
        (resourcesById[a]?.name ?? '').localeCompare(resourcesById[b]?.name ?? ''),
      ),
    );
    return out;
  }, [teams, allResources, resourcesById]);

  const unassigned = useMemo(
    () =>
      allResources
        .filter((r) => r.id && (!r.teamId || !teamIdSet.has(r.teamId)))
        .map((r) => r.id!)
        .sort((a, b) =>
          (resourcesById[a]?.name ?? '').localeCompare(resourcesById[b]?.name ?? ''),
        ),
    [allResources, teamIdSet, resourcesById],
  );

  // Series are needed only for resources that occupy a row (team members).
  const memberResourceIds = useMemo(
    () => Object.values(membersByTeam).flat(),
    [membersByTeam],
  );

  const loadResults = useQueries({
    queries: memberResourceIds.map((id) => ({
      ...getLoadGetResourceLoadQueryOptions<DailyLoadDto[]>(id, { from: fromISO, to: toISO }),
      staleTime: 60_000,
    })),
  });
  const capResults = useQueries({
    queries: memberResourceIds.map((id) => ({
      ...getResourcesGetCapacityQueryOptions(id, { from: fromISO, to: toISO }),
      staleTime: 60_000,
    })),
  });

  const loadSig = loadResults.map((r) => r.dataUpdatedAt).join(',');
  const capSig = capResults.map((r) => r.dataUpdatedAt).join(',');
  const series = useMemo(() => {
    const map: Record<string, ResourceSeries> = {};
    memberResourceIds.forEach((id, i) => {
      const s: ResourceSeries = new Map();
      for (const c of (capResults[i]?.data ?? []) as DailyCapacityDto[]) {
        if (c.date) s.set(c.date, { alloc: 0, cap: parseDurationHours(c.hours) });
      }
      for (const l of (loadResults[i]?.data ?? []) as DailyLoadDto[]) {
        if (!l.date) continue;
        const prev = s.get(l.date) ?? { alloc: 0, cap: 0 };
        s.set(l.date, { alloc: parseDurationHours(l.hours), cap: prev.cap });
      }
      map[id] = s;
    });
    return map;
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [memberResourceIds, loadSig, capSig]);

  // ── Config ──
  const bands = useMemo(() => normalizeBands(bandsQuery.data?.bands), [bandsQuery.data]);
  const primaryGrain = grainOf(bucketingQuery.data?.primaryGrain);
  const secondaryGrain = grainOf(bucketingQuery.data?.secondaryGrain);
  const [pickedGrain, setPickedGrain] = useState<Grain | null>(null);
  const grain = pickedGrain ?? primaryGrain;

  const buckets = useMemo(
    () => buildBuckets(grain, today),
    // i18n.language drives the localized month/day labels inside buildBuckets.
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [grain, today, i18n.language],
  );
  const groups = useMemo(() => groupRuns(buckets), [buckets]);
  const todayIdx = useMemo(() => todayBucketIdx(buckets), [buckets]);

  // ── Computed loads ──
  const overall = useMemo(
    () => rowLoads(buckets, membersSampler(memberResourceIds.map((id) => series[id]))),
    [buckets, series, memberResourceIds],
  );
  const teamLoads = useMemo(() => {
    const out: Record<string, BucketLoad[]> = {};
    teams.forEach((t) => {
      if (!t.id) return;
      const members = membersByTeam[t.id] ?? [];
      out[t.id] = rowLoads(buckets, membersSampler(members.map((id) => series[id])));
    });
    return out;
  }, [teams, membersByTeam, buckets, series]);
  const personLoads = useMemo(() => {
    const out: Record<string, BucketLoad[]> = {};
    memberResourceIds.forEach((id) => {
      out[id] = rowLoads(buckets, resourceSampler(series[id]));
    });
    return out;
  }, [memberResourceIds, buckets, series]);

  return {
    today,
    bands,
    grain,
    setGrain: setPickedGrain,
    primaryGrain,
    secondaryGrain,
    buckets,
    groups,
    todayIdx,
    teams,
    allResources,
    resourcesById,
    roleNameById,
    membersByTeam,
    unassigned,
    overall,
    teamLoads,
    personLoads,
    isLoading:
      teamsQuery.isLoading ||
      resourcesQuery.isLoading ||
      bandsQuery.isLoading ||
      bucketingQuery.isLoading,
    isSeriesFetching:
      loadResults.some((r) => r.isFetching) || capResults.some((r) => r.isFetching),
  };
}
