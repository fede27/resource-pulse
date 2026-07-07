// Persone board — data layer. Composes the read surface into PersonData[]:
//   1 × GET /api/resources                         (rows = tutte le persone attive)
//   1 × GET /api/roles + GET /api/teams            (group labels)
//   1 × GET /api/project-nodes                     (node id → name, per le lane)
//   P × GET /api/resources/{id}/capacity?from&to   (calendario → ore/giorno)
//   P × GET /api/allocations/by-resource/{id}?from&to (blocchi di copertura)
// plus the org config (bands / fence / bucketing).
//
// Cells, lanes and the inspector all derive from (blocks × capacity) in the
// pure model — hours = % × daily capacity (ADR-0026), so no per-person daily
// load series is fetched. The P× fan-out is the same accepted first-step
// trade-off as the Progetti board (gap doc §GAP 3); the open-demands picker
// query lives in CoverPopover (lazy, GAP 1).

import { useMemo } from 'react';
import dayjs from 'dayjs';
import { useQueries } from '@tanstack/react-query';
import { getAllocationsGetForResourceQueryOptions } from '@/api/generated/allocations/allocations';
import { useBucketingGet } from '@/api/generated/bucketing/bucketing';
import { useLoadBandsGet } from '@/api/generated/load-bands/load-bands';
import { useProjectNodesGetAll } from '@/api/generated/project-nodes/project-nodes';
import {
  getResourcesGetCapacityQueryOptions,
  useResourcesGetAll,
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
  capacityMap,
  toBoardPerson,
  toPersonBlock,
  weeklyCapacity,
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

  const nodeNameById = useMemo(
    () => new Map(nodes.filter((n) => n.id).map((n) => [n.id!, n.name ?? '—'])),
    [nodes],
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

  const capacityQs = useQueries({
    queries: persons.map((p) => getResourcesGetCapacityQueryOptions(p.id, range)),
  });
  const allocQs = useQueries({
    queries: persons.map((p) => getAllocationsGetForResourceQueryOptions(p.id, range)),
  });

  // The query-result arrays are new on every render; a scalar version built
  // from dataUpdatedAt gates the rebuild instead of spreading them into deps.
  const detailVersion =
    capacityQs.reduce((s, q) => s + q.dataUpdatedAt, 0) +
    allocQs.reduce((s, q) => s + q.dataUpdatedAt, 0);

  const people = useMemo<PersonData[]>(
    () =>
      persons.map((r, i) => {
        const capacityByDay = capacityMap(capacityQs[i]?.data ?? []);
        return {
          person: toBoardPerson(r, roleNameById, teamNameById),
          blocks: (allocQs[i]?.data ?? []).map((a) => toPersonBlock(a, nodeNameById)),
          capacityByDay,
          weeklyCapH: weeklyCapacity(capacityByDay, range.from, range.to),
        };
      }),
    // eslint-disable-next-line react-hooks/exhaustive-deps -- detailVersion stands in for the per-person query results
    [persons, roleNameById, teamNameById, nodeNameById, range.from, range.to, detailVersion],
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

  const detailPending =
    capacityQs.some((q) => q.isPending) || allocQs.some((q) => q.isPending);

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
