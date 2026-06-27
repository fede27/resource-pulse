import { useCallback, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import dayjs, { type Dayjs } from 'dayjs';
import { useTeamsGetAll } from '@/api/generated/teams/teams';
import {
  resourcesGetCapacity,
  useResourcesGetAll,
} from '@/api/generated/resources/resources';
import { loadGetResourceLoad } from '@/api/generated/load/load';
import { useRolesGetAll } from '@/api/generated/roles/roles';
import { useLoadBandsGet } from '@/api/generated/load-bands/load-bands';
import { useBucketingGet } from '@/api/generated/bucketing/bucketing';
import type {
  LoadResult,
  ResourceReadDto,
  RoleReadDto,
  TeamReadDto,
} from '@/api/generated/schemas';
import type { DailyLoadDto } from '@/api/load-types';
import {
  buildBuckets,
  CELL_WIDTH,
  dateRangeForIndices,
  groupRuns,
  HORIZON,
  useChunkedSeries,
  useTimelineViewport,
  type Bucket,
  type ChunkedSeries,
  type ChunkFetcher,
  type Grain,
  type Group,
  type TimelineViewport,
} from '@/components/timeline';
import {
  EMPTY_LOAD,
  grainOf,
  normalizeBands,
  parseDurationHours,
  rowLoads,
  type BucketLoad,
  type DaySample,
  type LoadBand,
} from './loadModel';

export const NAME_W = 300;
const EMPTY_SAMPLE: DaySample = { alloc: 0, cap: 0 };

const listOf = <T>(data: unknown): T[] => ((data as LoadResult | undefined)?.data ?? []) as T[];

export type TeamGrid = {
  today: Dayjs;
  bands: LoadBand[];
  grain: Grain;
  setGrain: (g: Grain) => void;
  primaryGrain: Grain;
  secondaryGrain: Grain;

  viewport: TimelineViewport;
  cellW: number;
  nameW: number;
  buckets: Bucket[]; // visible slice only
  groups: Group[];

  teams: TeamReadDto[];
  allResources: ResourceReadDto[];
  resourcesById: Record<string, ResourceReadDto>;
  roleNameById: Record<string, string>;
  membersByTeam: Record<string, string[]>;
  unassigned: string[];

  // Per-bucket loads, positionally aligned to `buckets`.
  overall: BucketLoad[];
  teamLoads: Record<string, BucketLoad[]>;
  personLoads: Record<string, BucketLoad[]>;
  // Current-period (today) loads — independent of horizontal scroll.
  nowOverall: BucketLoad;
  nowByTeam: Record<string, BucketLoad>;
  nowByPerson: Record<string, BucketLoad>;

  isColumnLoading: (b: Bucket) => boolean;
  isLoading: boolean;
  isSeriesFetching: boolean;
};

export function useTeamGrid(): TeamGrid {
  const { i18n } = useTranslation();
  // Epoch anchored once at mount; k = 0 is today's bucket for the active grain.
  const today = useMemo(() => dayjs(), []);

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

  const teamIdSet = useMemo(
    () => new Set(teams.map((t) => t.id).filter(Boolean) as string[]),
    [teams],
  );

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

  // ── Config ──
  const bands = useMemo(() => normalizeBands(bandsQuery.data?.bands), [bandsQuery.data]);
  const primaryGrain = grainOf(bucketingQuery.data?.primaryGrain);
  const secondaryGrain = grainOf(bucketingQuery.data?.secondaryGrain);
  const [pickedGrain, setPickedGrain] = useState<Grain | null>(null);
  const grain = pickedGrain ?? primaryGrain;

  // ── Virtualized horizontal viewport ──
  const cellW = CELL_WIDTH[grain];
  const horizon = HORIZON[grain];
  const viewport = useTimelineViewport({
    cellW,
    nameW: NAME_W,
    kMin: -horizon,
    kMax: horizon,
    recenterKey: grain,
  });
  const { kStart, kEnd } = viewport.visible;

  const buckets = useMemo(
    () => buildBuckets(grain, today, kStart, kEnd, today),
    // i18n.language drives the localized month/day labels.
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [grain, today, kStart, kEnd, i18n.language],
  );
  const groups = useMemo(() => groupRuns(buckets), [buckets]);

  // ── Lazy chunked load+capacity, merged into DaySample ──
  const fetchChunk = useCallback<ChunkFetcher<DaySample>>(async (id, from, to, signal) => {
    // `loadGetResourceLoad` is generated as `Promise<void>` — the LoadController
    // GETs predate the `[ProducesResponseType<T>]` regen (see api/load-types.ts);
    // the axios mutator returns the parsed body at runtime, so assert the real
    // shape here. `resourcesGetCapacity` is already typed, hence the asymmetry.
    const [load, cap] = await Promise.all([
      loadGetResourceLoad(id, { from, to }, undefined, signal) as unknown as Promise<DailyLoadDto[]>,
      resourcesGetCapacity(id, { from, to }, undefined, signal),
    ]);
    const m = new Map<string, DaySample>();
    for (const c of cap ?? []) {
      if (c.date) m.set(c.date, { alloc: 0, cap: parseDurationHours(c.hours) });
    }
    for (const l of load ?? []) {
      if (!l.date) continue;
      const prev = m.get(l.date);
      m.set(l.date, { alloc: parseDurationHours(l.hours), cap: prev?.cap ?? 0 });
    }
    return m;
  }, []);

  // Two windows over the same chunk-aligned cache. Sharing `namespace` means the
  // chunk covering today is fetched once and reused by both: the visible slice
  // drives the grid (strictly windowed — O(visible) active queries, regardless
  // of how far we've scrolled), while a single today-bucket window keeps the
  // scroll-independent "now" stats/chips populated. This is deliberately NOT a
  // widen-visible-to-include-today range, which would fill the whole gap with
  // contiguous chunks and keep every intermediate query mounted.
  const visRange = useMemo(
    () => dateRangeForIndices(grain, today, kStart, kEnd),
    [grain, today, kStart, kEnd],
  );
  const todayRange = useMemo(() => dateRangeForIndices(grain, today, 0, 0), [grain, today]);

  const chunkedVisible = useChunkedSeries<DaySample>({
    namespace: 'resource-load',
    entityIds: memberResourceIds,
    from: visRange.from,
    to: visRange.to,
    chunkDays: 90,
    fetchChunk,
    enabled: viewport.ready && memberResourceIds.length > 0,
  });
  const chunkedToday = useChunkedSeries<DaySample>({
    namespace: 'resource-load',
    entityIds: memberResourceIds,
    from: todayRange.from,
    to: todayRange.to,
    chunkDays: 90,
    fetchChunk,
    enabled: memberResourceIds.length > 0,
  });

  // ── Samplers + computed loads ──
  const sampleMembersOf = useCallback(
    (series: ChunkedSeries<DaySample>, ids: string[]) =>
      (date: string): DaySample => {
        let alloc = 0;
        let cap = 0;
        for (const id of ids) {
          const v = series.sample(id, date);
          if (v) {
            alloc += v.alloc;
            cap += v.cap;
          }
        }
        return { alloc, cap };
      },
    [],
  );
  const sampleOneOf = useCallback(
    (series: ChunkedSeries<DaySample>, id: string) =>
      (date: string): DaySample =>
        series.sample(id, date) ?? EMPTY_SAMPLE,
    [],
  );

  const overall = useMemo(
    () => rowLoads(buckets, sampleMembersOf(chunkedVisible, memberResourceIds)),
    [buckets, sampleMembersOf, chunkedVisible, memberResourceIds],
  );
  const teamLoads = useMemo(() => {
    const out: Record<string, BucketLoad[]> = {};
    teams.forEach((t) => {
      if (!t.id) return;
      out[t.id] = rowLoads(buckets, sampleMembersOf(chunkedVisible, membersByTeam[t.id] ?? []));
    });
    return out;
  }, [teams, membersByTeam, buckets, sampleMembersOf, chunkedVisible]);
  const personLoads = useMemo(() => {
    const out: Record<string, BucketLoad[]> = {};
    memberResourceIds.forEach((id) => {
      out[id] = rowLoads(buckets, sampleOneOf(chunkedVisible, id));
    });
    return out;
  }, [memberResourceIds, buckets, sampleOneOf, chunkedVisible]);

  // ── "Now" loads (today bucket), scroll-independent ──
  const todayBuckets = useMemo(
    () => buildBuckets(grain, today, 0, 0, today),
    [grain, today],
  );
  const nowOf = useCallback(
    (sample: (d: string) => DaySample): BucketLoad => rowLoads(todayBuckets, sample)[0] ?? EMPTY_LOAD,
    [todayBuckets],
  );
  const nowOverall = useMemo(
    () => nowOf(sampleMembersOf(chunkedToday, memberResourceIds)),
    [nowOf, sampleMembersOf, chunkedToday, memberResourceIds],
  );
  const nowByTeam = useMemo(() => {
    const out: Record<string, BucketLoad> = {};
    teams.forEach((t) => {
      if (t.id) out[t.id] = nowOf(sampleMembersOf(chunkedToday, membersByTeam[t.id] ?? []));
    });
    return out;
  }, [teams, membersByTeam, nowOf, sampleMembersOf, chunkedToday]);
  const nowByPerson = useMemo(() => {
    const out: Record<string, BucketLoad> = {};
    memberResourceIds.forEach((id) => {
      out[id] = nowOf(sampleOneOf(chunkedToday, id));
    });
    return out;
  }, [memberResourceIds, nowOf, sampleOneOf, chunkedToday]);

  const isColumnLoading = useCallback(
    (b: Bucket) =>
      memberResourceIds.length > 0 && !chunkedVisible.isChunkReady(b.start.format('YYYY-MM-DD')),
    [chunkedVisible, memberResourceIds],
  );

  return {
    today,
    bands,
    grain,
    setGrain: setPickedGrain,
    primaryGrain,
    secondaryGrain,
    viewport,
    cellW,
    nameW: NAME_W,
    buckets,
    groups,
    teams,
    allResources,
    resourcesById,
    roleNameById,
    membersByTeam,
    unassigned,
    overall,
    teamLoads,
    personLoads,
    nowOverall,
    nowByTeam,
    nowByPerson,
    isColumnLoading,
    isLoading:
      teamsQuery.isLoading ||
      resourcesQuery.isLoading ||
      bandsQuery.isLoading ||
      bucketingQuery.isLoading,
    isSeriesFetching: chunkedVisible.isFetching || chunkedToday.isFetching,
  };
}
