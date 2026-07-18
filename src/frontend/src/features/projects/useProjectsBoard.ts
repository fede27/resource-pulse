// Projects board — data layer. Composes the read surface into BoardProject[]:
//   1 × GET /api/projects?from&to&dateSource=Planned        (roots in range)
//   1 × GET /api/project-nodes                              (phases, grouped by Path root)
//   1 × GET /api/allocations/in-range?from&to               (slice del piano — P3)
//   1 × GET /api/resources/capacity?from&to&ids=…           (RLE — ore per blocco)
//   1 × GET /api/resources/load-profiles?…&status=Hard&ids=… (peak + composition — P2)
//   1 × GET /api/demands/coverage?from&to                   (demand rows + gap — P4)
// plus the org config (bands / fence / bucketing) and /api/me.
//
// No N+1 fan-out remains (api-roundtrip-consolidation.md P0–P4): every read is
// a domain-shaped batch pivoted client-side by the DTO-resolved root project /
// resource, so the request count is constant in the portfolio size. Per-block
// hours are derived client-side as % × capacity over the fetched range
// (ADR-0026) from the batch capacity read — RANGE-SCOPED like the coverage
// reconciliation shown alongside (the resolved-hours sidecar was full-window).
// The board bundle endpoint stays deferred (project-gap.md §★★ — page-shaped,
// not domain).

import { useMemo } from 'react';
import dayjs from 'dayjs';
import { useAllocationsGetInRange } from '@/api/generated/allocations/allocations';
import { useBucketingGet } from '@/api/generated/bucketing/bucketing';
import {
  useLoadGetDemandCoverageInRange,
  useLoadGetResourceLoadProfiles,
} from '@/api/generated/load/load';
import { useLoadBandsGet } from '@/api/generated/load-bands/load-bands';
import { useMeGet } from '@/api/generated/me/me';
import { useProjectNodesGetAll } from '@/api/generated/project-nodes/project-nodes';
import { useProjectsGetActiveInRange } from '@/api/generated/projects/projects';
import {
  useResourcesGetAll,
  useResourcesGetCapacities,
} from '@/api/generated/resources/resources';
import { useRolesGetAll } from '@/api/generated/roles/roles';
import { useTimeFenceGet } from '@/api/generated/time-fence/time-fence';
import {
  AllocationStatus,
  BucketGrain,
  DateSource,
  type AllocationReadDto,
  type DemandCoverageDto,
  type LoadSegmentDto,
  type ProjectNodeReadDto,
  type ResourceReadDto,
  type RoleReadDto,
} from '@/api/generated/schemas';
import type { Grain } from '@/components/timeline';
import { normalizeBands, overloadFloor, type LoadBand } from '@/lib/loadBands';
import { capacityMapFromSegments, blockHoursInRange } from '@/lib/capacity';
import {
  buildBoardProject,
  peakOf,
  type BoardProject,
  type CoverageBlock,
  type CurrentUser,
} from './boardModel';
import { fenceEnd, type FenceBoundaries } from '@/components/board';

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
  // Hours a block resolves to over the FETCHED RANGE (% × capacity, ADR-0026),
  // derived from the batch capacity read (consolidation P1/P3 — replaces the
  // per-block resolved-hours sidecar). Null while capacity is still loading.
  blockHoursOf: (block: CoverageBlock) => number | null;
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

  // One flat read of every node; each root's subtree is the Path-prefix group
  // (materialized path ⇒ first segment = root id). Replaces the former M×
  // GET /{id}/subtree fan-out — P0 of api-roundtrip-consolidation.md.
  const nodesQ = useProjectNodesGetAll();

  const nodesByRoot = useMemo(() => {
    const rows = (nodesQ.data?.data ?? []) as ProjectNodeReadDto[];
    const map = new Map<string, ProjectNodeReadDto[]>();
    for (const n of rows) {
      const rootId = n.path?.split('/').find((s) => s.length > 0);
      if (!rootId) continue;
      const group = map.get(rootId);
      if (group) group.push(n);
      else map.set(rootId, [n]);
    }
    return map;
  }, [nodesQ.data]);

  // Cross-project demand reconciliation in ONE call (P4), pivoted by the
  // DTO-resolved root project client-side — replaces M× per-node demand-coverage.
  // Roots Closed/Cancelled are excluded server-side (I4), matching the board.
  const coverageQ = useLoadGetDemandCoverageInRange({ from: range.from, to: range.to });

  const coverageByRoot = useMemo(() => {
    const map = new Map<string, DemandCoverageDto[]>();
    for (const c of coverageQ.data ?? []) {
      const rootId = c.rootProjectId;
      if (!rootId) continue;
      const list = map.get(rootId);
      if (list) list.push(c);
      else map.set(rootId, [c]);
    }
    return map;
  }, [coverageQ.data]);

  // One flat plan-slice read for every coverage in range (P3), pivoted by the
  // DTO-resolved root project client-side — replaces M× by-project-node.
  const allocationsQ = useAllocationsGetInRange({ from: range.from, to: range.to });

  const allocationsByRoot = useMemo(() => {
    const map = new Map<string, AllocationReadDto[]>();
    for (const a of allocationsQ.data ?? []) {
      const rootId = a.rootProjectId;
      if (!rootId) continue;
      const list = map.get(rootId);
      if (list) list.push(a);
      else map.set(rootId, [a]);
    }
    return map;
  }, [allocationsQ.data]);

  const projects = useMemo(
    () =>
      roots.map((r) =>
        buildBoardProject(
          r,
          nodesByRoot.get(r.id) ?? [],
          coverageByRoot.get(r.id) ?? [],
          allocationsByRoot.get(r.id) ?? [],
        ),
      ),
    [roots, nodesByRoot, coverageByRoot, allocationsByRoot],
  );

  const personIds = useMemo(
    () => [...new Set(projects.flatMap((p) => p.people))].sort(),
    [projects],
  );

  // Hard-only profile: the sustainability verdict counts committed load only —
  // a proposed (tentative) block must not flip someone into "overloaded".
  // ONE batch read for the whole covering population (consolidation P2);
  // explicit ids because coverage may reference deactivated people.
  const profilesQ = useLoadGetResourceLoadProfiles(
    { from: range.from, to: range.to, status: AllocationStatus.Hard, ids: personIds },
    { query: { enabled: personIds.length > 0 } },
  );

  const profiles = useMemo(() => {
    const map = new Map<string, LoadSegmentDto[]>();
    for (const p of profilesQ.data ?? []) {
      if (p.resourceId) map.set(p.resourceId, p.segments ?? []);
    }
    return map;
  }, [profilesQ.data]);

  const peaks = useMemo(() => {
    const map = new Map<string, number>();
    for (const [id, segs] of profiles) map.set(id, peakOf(segs));
    return map;
  }, [profiles]);

  // Batch capacity for the covering people (P1) — EXPLICIT ids: coverage may
  // reference a since-deactivated resource, which the omitted-ids form (active
  // only) would drop. Feeds the per-block hours derivation (% × capacity over
  // the fetched range, ADR-0026) that replaced the resolved-hours sidecar.
  const capacitiesQ = useResourcesGetCapacities(
    { from: range.from, to: range.to, ids: personIds },
    { query: { enabled: personIds.length > 0 } },
  );

  const capacityByPerson = useMemo(() => {
    const map = new Map<string, ReadonlyMap<string, number>>();
    for (const rc of capacitiesQ.data ?? []) {
      if (rc.resourceId) map.set(rc.resourceId, capacityMapFromSegments(rc.segments ?? []));
    }
    return map;
  }, [capacitiesQ.data]);

  const blockHoursOf = useMemo(
    () =>
      (block: CoverageBlock): number | null => {
        const capacityByDay = capacityByPerson.get(block.resourceId);
        if (!capacityByDay) return null; // capacity still loading (or unknown person)
        const from = block.from > range.from ? block.from : range.from;
        const to = block.to < range.to ? block.to : range.to;
        return Math.round(blockHoursInRange(from, to, block.percent, capacityByDay) * 10) / 10;
      },
    [capacityByPerson, range.from, range.to],
  );

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
    nodesQ.isPending ||
    allocationsQ.isPending ||
    coverageQ.isPending ||
    (personIds.length > 0 && (profilesQ.isPending || capacitiesQ.isPending));

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
    blockHoursOf,
    fetchRange: range,
  };
}
