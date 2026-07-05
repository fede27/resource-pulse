// Projects board — data layer. Composes the read surface into BoardProject[]:
//   1 × GET /api/projects?from&to&dateSource=Planned        (roots in range)
//   M × GET /api/project-nodes/{id}/subtree                 (phases)
//   M × GET /api/project-nodes/{id}/demand-coverage?from&to (demand rows + gap)
//   M × GET /api/allocations/by-project-node/{id}?from&to   (coverage blocks)
//   P × GET /api/resources/{id}/load-profile?…&status=Hard  (peak + composition)
// plus the org config (bands / fence / bucketing) and /api/me.
//
// The N+1 fan-out is a deliberate first-step trade-off (project-gap.md §★★): a
// board bundle endpoint is page-shaped, not domain, and stays deferred. Every
// query is cached per (id, from, to) so panning/re-sorting doesn't refetch.

import { useMemo } from 'react';
import dayjs from 'dayjs';
import { useQueries } from '@tanstack/react-query';
import { getAllocationsGetForProjectNodeQueryOptions } from '@/api/generated/allocations/allocations';
import { useBucketingGet } from '@/api/generated/bucketing/bucketing';
import {
  getLoadGetProjectNodeDemandCoverageQueryOptions,
  getLoadGetResourceLoadProfileQueryOptions,
} from '@/api/generated/load/load';
import { useLoadBandsGet } from '@/api/generated/load-bands/load-bands';
import { useMeGet } from '@/api/generated/me/me';
import { getProjectNodesGetSubtreeQueryOptions } from '@/api/generated/project-nodes/project-nodes';
import { useProjectsGetActiveInRange } from '@/api/generated/projects/projects';
import { useResourcesGetAll } from '@/api/generated/resources/resources';
import { useRolesGetAll } from '@/api/generated/roles/roles';
import { useTimeFenceGet } from '@/api/generated/time-fence/time-fence';
import {
  AllocationStatus,
  BucketGrain,
  DateSource,
  type LoadSegmentDto,
  type ProjectNodeReadDto,
  type ResourceReadDto,
  type RoleReadDto,
} from '@/api/generated/schemas';
import type { Grain } from '@/components/timeline';
import { normalizeBands, overloadFloor, type LoadBand } from '@/lib/loadBands';
import { buildBoardProject, peakOf, type BoardProject, type CurrentUser } from './boardModel';
import { fenceEnd, type FenceBoundaries } from './timelineGeo';

const ISO = 'YYYY-MM-DD';
// The load/coverage endpoints cap the range at 366 inclusive days (400 beyond);
// the fetch window is clamped so a wide visual domain never turns into a 400.
const MAX_RANGE_DAYS = 366;

export type BoardDomain = { minISO: string; maxISO: string };

export type PersonPoolEntry = { id: string; name: string; roleName: string | null };

const grainOf = (g: BucketGrain | undefined): Grain =>
  g === BucketGrain.Day ? 'day' : g === BucketGrain.Month ? 'month' : 'week';

export function clampRange(domain: BoardDomain): { from: string; to: string } {
  const min = dayjs(domain.minISO);
  const max = dayjs(domain.maxISO);
  if (max.diff(min, 'day') + 1 <= MAX_RANGE_DAYS) return { from: domain.minISO, to: domain.maxISO };
  return { from: domain.minISO, to: min.add(MAX_RANGE_DAYS - 1, 'day').format(ISO) };
}

export type ProjectsBoard = {
  isLoading: boolean; // first paint gate (config + roots)
  isFetching: boolean; // detail queries still streaming in
  isError: boolean;
  projects: BoardProject[];
  bands: LoadBand[];
  overloadThreshold: number;
  fence: FenceBoundaries;
  todayISO: string;
  primaryGrain: Grain;
  secondaryGrain: Grain;
  me: CurrentUser;
  personPool: PersonPoolEntry[];
  personName: (resourceId: string) => string;
  peakByPerson: (resourceId: string) => number;
  profileByPerson: (resourceId: string) => LoadSegmentDto[];
  fetchRange: { from: string; to: string };
};

export function useProjectsBoard(domain: BoardDomain): ProjectsBoard {
  const todayISO = dayjs().format(ISO);
  const range = clampRange(domain);

  const bandsQ = useLoadBandsGet();
  const fenceQ = useTimeFenceGet();
  const bucketingQ = useBucketingGet();
  const meQ = useMeGet();
  const resourcesQ = useResourcesGetAll();
  const rolesQ = useRolesGetAll();
  const projectsQ = useProjectsGetActiveInRange({
    from: range.from,
    to: range.to,
    dateSource: DateSource.Planned,
  });

  const roots = useMemo(
    () => (projectsQ.data ?? []).filter((p): p is ProjectNodeReadDto & { id: string } => !!p.id),
    [projectsQ.data],
  );

  const subtreeQs = useQueries({
    queries: roots.map((r) => getProjectNodesGetSubtreeQueryOptions(r.id)),
  });
  const coverageQs = useQueries({
    queries: roots.map((r) => getLoadGetProjectNodeDemandCoverageQueryOptions(r.id, range)),
  });
  const allocQs = useQueries({
    queries: roots.map((r) => getAllocationsGetForProjectNodeQueryOptions(r.id, range)),
  });

  const projects = useMemo(
    () =>
      roots.map((r, i) =>
        buildBoardProject(
          r,
          subtreeQs[i]?.data ?? [],
          coverageQs[i]?.data ?? [],
          allocQs[i]?.data ?? [],
        ),
      ),
    // eslint-disable-next-line react-hooks/exhaustive-deps -- query result arrays are new each render; data identities gate the rebuild
    [
      roots,
      ...subtreeQs.map((q) => q.data),
      ...coverageQs.map((q) => q.data),
      ...allocQs.map((q) => q.data),
    ],
  );

  const personIds = useMemo(
    () => [...new Set(projects.flatMap((p) => p.people))].sort(),
    [projects],
  );

  // Hard-only profile: the sustainability verdict counts committed load only —
  // a proposed (tentative) block must not flip someone into "overloaded".
  const profileQs = useQueries({
    queries: personIds.map((id) =>
      getLoadGetResourceLoadProfileQueryOptions(id, { ...range, status: AllocationStatus.Hard }),
    ),
  });

  const profiles = useMemo(() => {
    const map = new Map<string, LoadSegmentDto[]>();
    personIds.forEach((id, i) => {
      const data = profileQs[i]?.data;
      if (data) map.set(id, data);
    });
    return map;
    // eslint-disable-next-line react-hooks/exhaustive-deps -- see above
  }, [personIds, ...profileQs.map((q) => q.data)]);

  const peaks = useMemo(() => {
    const map = new Map<string, number>();
    for (const [id, segs] of profiles) map.set(id, peakOf(segs));
    return map;
  }, [profiles]);

  const personPool = useMemo<PersonPoolEntry[]>(() => {
    const rows = (resourcesQ.data?.data ?? []) as ResourceReadDto[];
    const roleRows = (rolesQ.data?.data ?? []) as RoleReadDto[];
    const roleName = new Map(roleRows.filter((r) => r.id).map((r) => [r.id!, r.name ?? '—']));
    return rows
      .filter((r): r is ResourceReadDto & { id: string } => !!r.id)
      .map((r) => ({
        id: r.id,
        name: r.name ?? '—',
        roleName: r.roleId ? (roleName.get(r.roleId) ?? null) : null,
      }))
      .sort((a, b) => a.name.localeCompare(b.name));
  }, [resourcesQ.data, rolesQ.data]);

  const personNames = useMemo(() => {
    const map = new Map<string, string>();
    for (const p of personPool) map.set(p.id, p.name);
    return map;
  }, [personPool]);

  const bands = useMemo(() => normalizeBands(bandsQ.data?.bands), [bandsQ.data]);

  const fence = useMemo<FenceBoundaries>(
    () => ({
      todayISO,
      frozenEndISO: fenceEnd(todayISO, fenceQ.data?.frozenHorizon?.value, fenceQ.data?.frozenHorizon?.unit),
      slushyEndISO: fenceEnd(todayISO, fenceQ.data?.slushyHorizon?.value, fenceQ.data?.slushyHorizon?.unit),
    }),
    [todayISO, fenceQ.data],
  );

  const me = useMemo<CurrentUser>(
    () => ({
      resourceId: meQ.data?.resourceId ?? null,
      isStaffingManager: meQ.data?.isStaffingManager ?? false,
    }),
    [meQ.data],
  );

  const detailPending =
    subtreeQs.some((q) => q.isPending) ||
    coverageQs.some((q) => q.isPending) ||
    allocQs.some((q) => q.isPending) ||
    profileQs.some((q) => q.isPending);

  return {
    isLoading: projectsQ.isPending || bandsQ.isPending || fenceQ.isPending || bucketingQ.isPending,
    isFetching: projectsQ.isFetching || (roots.length > 0 && detailPending),
    isError: projectsQ.isError,
    projects,
    bands,
    overloadThreshold: overloadFloor(bands),
    fence,
    todayISO,
    primaryGrain: grainOf(bucketingQ.data?.primaryGrain),
    secondaryGrain: grainOf(bucketingQ.data?.secondaryGrain),
    me,
    personPool,
    personName: (id) => personNames.get(id) ?? '—',
    peakByPerson: (id) => peaks.get(id) ?? 0,
    profileByPerson: (id) => profiles.get(id) ?? [],
    fetchRange: range,
  };
}
