// Persone board — data layer. Composes the read surface into PersonData[]:
//   1 × GET /api/resources                         (rows = tutte le persone attive)
//   1 × GET /api/roles + GET /api/teams            (group labels)
//   1 × GET /api/project-nodes                     (opzioni "materializza domanda inferita")
//   1 × GET /api/resources/capacity?from&to        (RLE, tutte le attive — P1)
//   1 × GET /api/allocations/in-range?from&to      (slice del piano — P3)
// plus the org config (bands / fence / bucketing).
//
// Cells, lanes and the inspector all derive from (blocks × capacity) in the
// pure model — hours = % × daily capacity (ADR-0026), so no per-person daily
// load series is fetched. Capacity comes in ONE batch read of run-length
// segments (api-roundtrip-consolidation.md P1) and the coverage blocks in ONE
// flat plan-slice read pivoted by resource client-side (P3): the request count
// no longer depends on the population size. The open-demands picker query
// lives in CoverPopover (lazy, GAP 1).

import { useMemo } from 'react';
import dayjs from 'dayjs';
import { useAllocationsGetInRange } from '@/api/generated/allocations/allocations';
import { useBucketingGet } from '@/api/generated/bucketing/bucketing';
import { useLoadBandsGet } from '@/api/generated/load-bands/load-bands';
import { useProjectNodesGetAll } from '@/api/generated/project-nodes/project-nodes';
import {
  useResourcesGetAll,
  useResourcesGetCapacities,
} from '@/api/generated/resources/resources';
import { useRolesGetAll } from '@/api/generated/roles/roles';
import { useTeamsGetAll } from '@/api/generated/teams/teams';
import { useTimeFenceGet } from '@/api/generated/time-fence/time-fence';
import {
  BucketGrain,
  ProjectNodeType,
  ProjectStatus,
  type ProjectNodeReadDto,
  type ResourceReadDto,
  type RoleReadDto,
  type TeamReadDto,
} from '@/api/generated/schemas';
import { fenceEnd, type BoardDomain, type FenceBoundaries } from '@/components/board';
import type { Grain } from '@/components/timeline';
import { normalizeBands, overloadFloor, type LoadBand } from '@/lib/loadBands';
import {
  capacityMapFromSegments,
  toBoardPerson,
  toPersonBlock,
  weeklyCapacity,
  type PersonBlock,
  type PersonData,
} from './peopleBoardModel';

const ISO = 'YYYY-MM-DD';
// The load/coverage endpoints cap the range at 366 inclusive days; clamp the
// fetch window so a wide visual domain never turns into a 400.
const MAX_RANGE_DAYS = 366;

export function clampRange(domain: BoardDomain): { from: string; to: string } {
  const min = dayjs(domain.minISO);
  const max = dayjs(domain.maxISO);
  if (max.diff(min, 'day') + 1 <= MAX_RANGE_DAYS) return { from: domain.minISO, to: domain.maxISO };
  return { from: domain.minISO, to: min.add(MAX_RANGE_DAYS - 1, 'day').format(ISO) };
}

export type RootProjectOption = { id: string; name: string };

export type PeopleBoard = {
  isLoading: boolean; // first paint gate (config + people)
  isFetching: boolean; // per-person detail queries still streaming in
  isError: boolean;
  people: PersonData[];
  bands: LoadBand[];
  overloadThreshold: number;
  fence: FenceBoundaries;
  todayISO: string;
  primaryGrain: Grain;
  // Non-closed root projects — the "materializza domanda inferita" targets.
  rootProjects: RootProjectOption[];
  fetchRange: { from: string; to: string };
};

const grainOf = (g: BucketGrain | undefined): Grain =>
  g === BucketGrain.Day ? 'day' : g === BucketGrain.Month ? 'month' : 'week';

export function usePeopleBoard(domain: BoardDomain): PeopleBoard {
  const todayISO = dayjs().format(ISO);
  const range = clampRange(domain);

  const bandsQ = useLoadBandsGet();
  const fenceQ = useTimeFenceGet();
  const bucketingQ = useBucketingGet();
  const resourcesQ = useResourcesGetAll();
  const rolesQ = useRolesGetAll();
  const teamsQ = useTeamsGetAll();
  const nodesQ = useProjectNodesGetAll();

  const persons = useMemo(() => {
    const rows = (resourcesQ.data?.data ?? []) as ResourceReadDto[];
    return rows
      .filter((r): r is ResourceReadDto & { id: string } => !!r.id && r.isActive !== false)
      .sort((a, b) => (a.name ?? '').localeCompare(b.name ?? ''));
  }, [resourcesQ.data]);

  const roleNameById = useMemo(() => {
    const rows = (rolesQ.data?.data ?? []) as RoleReadDto[];
    return new Map(rows.filter((r) => r.id).map((r) => [r.id!, r.name ?? '—']));
  }, [rolesQ.data]);

  const teamNameById = useMemo(() => {
    const rows = (teamsQ.data?.data ?? []) as TeamReadDto[];
    return new Map(rows.filter((t) => t.id).map((t) => [t.id!, t.name ?? '—']));
  }, [teamsQ.data]);

  const nodes = useMemo(
    () => (nodesQ.data?.data ?? []) as ProjectNodeReadDto[],
    [nodesQ.data],
  );

  const rootProjects = useMemo<RootProjectOption[]>(
    () =>
      nodes
        .filter(
          (n): n is ProjectNodeReadDto & { id: string } =>
            !!n.id &&
            n.nodeType === ProjectNodeType.Project &&
            n.status !== ProjectStatus.Closed &&
            n.status !== ProjectStatus.Cancelled,
        )
        .map((n) => ({ id: n.id, name: n.name ?? '—' }))
        .sort((a, b) => a.name.localeCompare(b.name)),
    [nodes],
  );

  // One batch capacity read for the whole active population (P1 of
  // api-roundtrip-consolidation.md) — RLE segments per resource, expanded to
  // the per-day maps the pure model works with.
  const capacitiesQ = useResourcesGetCapacities({ from: range.from, to: range.to });

  const capacityByResource = useMemo(() => {
    const map = new Map<string, ReadonlyMap<string, number>>();
    for (const rc of capacitiesQ.data ?? []) {
      if (rc.resourceId) map.set(rc.resourceId, capacityMapFromSegments(rc.segments ?? []));
    }
    return map;
  }, [capacitiesQ.data]);

  // One flat plan-slice read for every coverage in range (P3 of
  // api-roundtrip-consolidation.md), pivoted by resource client-side.
  const allocationsQ = useAllocationsGetInRange({ from: range.from, to: range.to });

  const blocksByResource = useMemo(() => {
    const map = new Map<string, PersonBlock[]>();
    for (const a of allocationsQ.data ?? []) {
      if (!a.resourceId) continue;
      const block = toPersonBlock(a);
      const list = map.get(a.resourceId);
      if (list) list.push(block);
      else map.set(a.resourceId, [block]);
    }
    return map;
  }, [allocationsQ.data]);

  const emptyCapacity: ReadonlyMap<string, number> = useMemo(() => new Map(), []);

  const people = useMemo<PersonData[]>(
    () =>
      persons.map((r) => {
        const capacityByDay = capacityByResource.get(r.id) ?? emptyCapacity;
        return {
          person: toBoardPerson(r, roleNameById, teamNameById),
          blocks: blocksByResource.get(r.id) ?? [],
          capacityByDay,
          weeklyCapH: weeklyCapacity(capacityByDay, range.from, range.to),
        };
      }),
    [persons, roleNameById, teamNameById, capacityByResource, blocksByResource, emptyCapacity, range.from, range.to],
  );

  const bands = useMemo(() => normalizeBands(bandsQ.data?.bands), [bandsQ.data]);

  const fence = useMemo<FenceBoundaries>(
    () => ({
      todayISO,
      frozenEndISO: fenceEnd(todayISO, fenceQ.data?.frozenHorizon?.value, fenceQ.data?.frozenHorizon?.unit),
      slushyEndISO: fenceEnd(todayISO, fenceQ.data?.slushyHorizon?.value, fenceQ.data?.slushyHorizon?.unit),
    }),
    [todayISO, fenceQ.data],
  );

  const detailPending = capacitiesQ.isPending || allocationsQ.isPending;

  return {
    isLoading:
      resourcesQ.isPending || bandsQ.isPending || fenceQ.isPending || bucketingQ.isPending,
    isFetching: resourcesQ.isFetching || (persons.length > 0 && detailPending),
    isError: resourcesQ.isError,
    people,
    bands,
    overloadThreshold: overloadFloor(bands),
    fence,
    todayISO,
    primaryGrain: grainOf(bucketingQ.data?.primaryGrain),
    rootProjects,
    fetchRange: range,
  };
}
