// Projects board — pure view-model. No React, no network.
//
// Normalizes the read DTOs (projects in range, subtree phases, demand-coverage,
// coverage blocks) into the board's project rows, and keeps every derived,
// deliberately CLIENT-SIDE concept in one place (project-gap.md §★★): the
// sustainability verdict, the "a rischio" reason, lifecycle, filters/sort and
// the portfolio health counts. None of these are server concepts yet — when a
// second consumer needs them, promote the rule, not this file.
//
// Hours semantics (ADR-0025/0026): requiredHours/coveredHours/gapHours come
// from the server (hours = % × capacity, range-scoped). Best-effort = null
// target = NO gap — never a fake zero. The uncovered demand is SCALAR on the
// node over the queried range (backend Decision 4): it has no span of its own.

import {
  AllocationStatus,
  CommitmentLevel,
  DemandProvenance,
  ProjectNodeType,
  ProjectStatus,
  ProjectType,
  type AllocationReadDto,
  type DemandCoverageDto,
  type LoadSegmentDto,
  type ProjectNodeReadDto,
} from '@/api/generated/schemas';
import { ENVELOPE_H, LANE_H } from '@/components/board';
import { parseDurationHours } from '@/lib/duration';

// ── Row height (vertical windowing) ──────────────────────────────────────
// Derived from state, never measured: envelope + (expanded) lanes-container
// border-top + one LANE_H per lane (border-box) + the block's bottom border.
// Must mirror ProjectRow's DOM (lane guard `expanded && lanes.length > 0`)
// and ProjectRow.styles.ts exactly — any height/border change goes through
// the shared tokens or updates this formula in lockstep.
export function projectRowHeight(project: BoardProject, expanded: boolean): number {
  const lanes = project.demands.reduce((n, d) => n + d.coverage.length, 0) + project.holes.length;
  return ENVELOPE_H + (expanded && lanes > 0 ? 1 + lanes * LANE_H : 0) + 1;
}

// ── View-model types ─────────────────────────────────────────────────────

export type CoverageBlock = {
  id: string;
  demandId: string;
  resourceId: string;
  resourceName: string;
  resourceRoleName: string | null;
  demandRoleName: string;
  from: string; // ISO date
  to: string; // ISO date, inclusive
  percent: number;
  hard: boolean;
  mismatch: boolean;
  notes: string | null;
};

export type DemandRowStatus = 'covered' | 'partial' | 'uncovered' | 'over' | 'noTarget';

export type DemandRow = {
  demandId: string;
  roleId: string;
  roleName: string;
  inferred: boolean;
  requiredH: number | null; // null = best-effort (no target, no gap)
  coveredH: number;
  gapH: number | null; // null = best-effort; negative = surplus
  usefulH: number; // covered capped at target (over-coverage is not a credit)
  overH: number; // coverage beyond the target (a problem, not a credit)
  ownerResourceId: string | null;
  ownerName: string | null;
  coverage: CoverageBlock[];
  uncovered: boolean; // target present + no coverage blocks in range
  mismatch: boolean; // ≥1 block covering with a different role
  status: DemandRowStatus;
};

export type BoardPhase = { id: string; label: string; from: string; to: string };

export type BoardProject = {
  id: string;
  name: string;
  client: string | null;
  ownerId: string | null;
  ownerName: string | null;
  critical: boolean; // CommitmentLevel.Critical (4)
  proposed: boolean; // IsProposed (M3): complement of the hard-commit levels
  status: ProjectStatus; // domain state machine (Draft → Active → Closed; ⇄ OnHold; → Cancelled)
  // The PUT /api/projects/{id} is full-replace: the contextual actions must
  // send back the current type/commitment/lead/client untouched.
  type: ProjectType;
  commitmentLevel: CommitmentLevel;
  from: string | null; // planned dates
  to: string | null;
  phases: BoardPhase[];
  demands: DemandRow[];
  holes: DemandRow[]; // uncovered demands (the "da riassegnare" lanes)
  people: string[]; // distinct covering resourceIds
  totals: { requiredH: number; usefulH: number; gapH: number; overH: number };
};

// ── Normalization ────────────────────────────────────────────────────────

const round1 = (n: number) => Math.round(n * 10) / 10;

export function toCoverageBlock(a: AllocationReadDto): CoverageBlock {
  const resourceRoleName = a.resourceRoleName ?? null;
  const demandRoleName = a.demandRoleName ?? '';
  return {
    id: a.id ?? '',
    demandId: a.demandId ?? '',
    resourceId: a.resourceId ?? '',
    resourceName: a.resourceName ?? '—',
    resourceRoleName,
    demandRoleName,
    from: a.periodStart ?? '',
    to: a.periodEnd ?? '',
    percent: a.allocationPercent ?? 0,
    hard: a.status === AllocationStatus.Hard,
    // Mismatch is role-vs-role (§6): the person's own role differs from what the
    // demand asks. A person without a role is not a mismatch — nothing to compare.
    mismatch: resourceRoleName !== null && demandRoleName !== '' && resourceRoleName !== demandRoleName,
    notes: a.notes ?? null,
  };
}

// Thresholds mirror the prototype: "covered" tolerates 2% under, "over" starts
// at 5% over — presentation slack over exact-hours equality, not domain rules.
export function demandRowStatus(requiredH: number | null, coveredH: number, hasBlocks: boolean): DemandRowStatus {
  if (requiredH === null) return 'noTarget';
  if (coveredH <= 0 && !hasBlocks) return 'uncovered';
  if (coveredH < requiredH * 0.98) return 'partial';
  if (coveredH > requiredH * 1.05) return 'over';
  return 'covered';
}

export function toDemandRow(d: DemandCoverageDto, blocks: CoverageBlock[]): DemandRow {
  const requiredH = d.requiredHours != null ? parseDurationHours(d.requiredHours) : null;
  const coveredH = parseDurationHours(d.coveredHours);
  const gapH = d.gapHours != null ? parseDurationHours(d.gapHours) : null;
  const usefulH = requiredH === null ? coveredH : Math.min(coveredH, requiredH);
  const overH = requiredH === null ? 0 : Math.max(0, coveredH - requiredH);
  return {
    demandId: d.demandId ?? '',
    roleId: d.roleId ?? '',
    roleName: d.roleName ?? '—',
    inferred: d.provenance === DemandProvenance.Inferred,
    requiredH,
    coveredH: round1(coveredH),
    gapH: gapH === null ? null : round1(gapH),
    usefulH: round1(usefulH),
    overH: round1(overH),
    ownerResourceId: d.ownerResourceId ?? null,
    ownerName: d.ownerResourceName ?? null,
    coverage: blocks,
    uncovered: requiredH !== null && blocks.length === 0,
    mismatch: blocks.some((b) => b.mismatch),
    status: demandRowStatus(requiredH, coveredH, blocks.length > 0),
  };
}

export function buildBoardProject(
  root: ProjectNodeReadDto,
  subtree: ProjectNodeReadDto[],
  demandCoverage: DemandCoverageDto[],
  allocations: AllocationReadDto[],
): BoardProject {
  const blocks = allocations.map(toCoverageBlock);
  const byDemand = new Map<string, CoverageBlock[]>();
  for (const b of blocks) {
    const list = byDemand.get(b.demandId) ?? [];
    list.push(b);
    byDemand.set(b.demandId, list);
  }

  const demands = demandCoverage
    .map((d) => toDemandRow(d, byDemand.get(d.demandId ?? '') ?? []))
    .sort((a, b) => a.roleName.localeCompare(b.roleName));

  const phases = subtree
    .filter((n) => n.nodeType === ProjectNodeType.Phase && n.plannedStart && n.plannedEnd)
    .map((n) => ({
      id: n.id ?? '',
      label: n.name ?? '',
      from: n.plannedStart!,
      to: n.plannedEnd!,
    }))
    .sort((a, b) => a.from.localeCompare(b.from));

  const totals = demands.reduce(
    (acc, d) => ({
      requiredH: acc.requiredH + (d.requiredH ?? 0),
      usefulH: acc.usefulH + d.usefulH,
      gapH: acc.gapH + Math.max(0, d.gapH ?? 0),
      overH: acc.overH + d.overH,
    }),
    { requiredH: 0, usefulH: 0, gapH: 0, overH: 0 },
  );

  return {
    id: root.id ?? '',
    name: root.name ?? '—',
    client: root.client ?? null,
    ownerId: root.leadResourceId ?? null,
    ownerName: root.leadResourceName ?? null,
    critical: root.commitmentLevel === CommitmentLevel.Critical,
    proposed: root.isProposed ?? false,
    status: root.status ?? ProjectStatus.Active,
    type: root.type ?? ProjectType.Customer,
    commitmentLevel: root.commitmentLevel ?? CommitmentLevel.Planned,
    from: root.plannedStart ?? null,
    to: root.plannedEnd ?? null,
    phases,
    demands,
    holes: demands.filter((d) => d.uncovered),
    people: [...new Set(blocks.map((b) => b.resourceId))],
    totals: {
      requiredH: round1(totals.requiredH),
      usefulH: round1(totals.usefulH),
      gapH: round1(totals.gapH),
      overH: round1(totals.overH),
    },
  };
}

// ── Utilization peaks (from /load-profile, hard-only) ────────────────────

export function peakOf(segments: LoadSegmentDto[] | undefined): number {
  if (!segments?.length) return 0;
  return segments.reduce((m, s) => Math.max(m, s.percent ?? 0), 0);
}

// ── Sustainability verdict (client-side, project-gap.md §★★) ─────────────

export type Verdict = 'uncovered' | 'atRisk' | 'sustainable';
export const VERDICT_SEVERITY: Record<Verdict, number> = { uncovered: 0, atRisk: 1, sustainable: 2 };

export type AtRiskReason = 'overload' | 'mismatch' | 'both' | null;

export function projectVerdict(
  p: BoardProject,
  peakByPerson: (resourceId: string) => number,
  overloadThreshold: number,
): { verdict: Verdict; reason: AtRiskReason } {
  if (p.holes.length > 0) return { verdict: 'uncovered', reason: null };
  const overload = p.people.some((id) => peakByPerson(id) >= overloadThreshold);
  const mismatch = p.demands.some((d) => d.mismatch);
  if (overload && mismatch) return { verdict: 'atRisk', reason: 'both' };
  if (overload) return { verdict: 'atRisk', reason: 'overload' };
  if (mismatch) return { verdict: 'atRisk', reason: 'mismatch' };
  return { verdict: 'sustainable', reason: null };
}

// People on the project whose hard peak crosses the threshold (why "a rischio").
export function conflictPeople(
  p: BoardProject,
  peakByPerson: (resourceId: string) => number,
  overloadThreshold: number,
): { resourceId: string; peak: number }[] {
  return p.people
    .map((id) => ({ resourceId: id, peak: peakByPerson(id) }))
    .filter((x) => x.peak >= overloadThreshold);
}

// ── Lifecycle & provenance (derived — no server concept yet) ─────────────

export type Lifecycle = 'future' | 'active' | 'closed';

export function lifecycleOf(p: BoardProject, todayISO: string): Lifecycle {
  // The domain status wins over dates: a Closed/Cancelled project is "closed"
  // even when its planned window is still running.
  if (p.status === ProjectStatus.Closed || p.status === ProjectStatus.Cancelled) return 'closed';
  if (p.to && p.to < todayISO) return 'closed';
  if (p.from && p.from > todayISO) return 'future';
  return 'active';
}

// ── Contextual actions (kebab) ───────────────────────────────────────────
// Availability mirrors ProjectNode's state machine (Draft → Active → Closed;
// Active ⇄ OnHold; any non-terminal → Cancelled). Terminal states expose no
// actions, so the kebab hides entirely.

export type ProjectAction =
  | { kind: 'start' }
  | { kind: 'complete' }
  | { kind: 'suspend' }
  | { kind: 'resume' }
  | { kind: 'setCommitment'; level: CommitmentLevel }
  | { kind: 'cancel' };

export type ProjectActionKind = ProjectAction['kind'];

export function availableActionKinds(status: ProjectStatus): ProjectActionKind[] {
  switch (status) {
    case ProjectStatus.Draft:
      return ['start', 'setCommitment', 'cancel'];
    case ProjectStatus.Active:
      return ['complete', 'suspend', 'setCommitment', 'cancel'];
    case ProjectStatus.OnHold:
      return ['resume', 'setCommitment', 'cancel'];
    default:
      return [];
  }
}

// Row chip: the states worth a visual mark (Active is the norm — no chip).
export type StatusChipKey = 'draft' | 'onHold' | 'closed' | 'cancelled';

export function statusChipKey(status: ProjectStatus): StatusChipKey | null {
  switch (status) {
    case ProjectStatus.Draft:
      return 'draft';
    case ProjectStatus.OnHold:
      return 'onHold';
    case ProjectStatus.Closed:
      return 'closed';
    case ProjectStatus.Cancelled:
      return 'cancelled';
    default:
      return null;
  }
}

export type Provenance = 'committed' | 'proposed';
export const provenanceOf = (p: BoardProject): Provenance => (p.proposed ? 'proposed' : 'committed');

// ── Filters & sort ───────────────────────────────────────────────────────

export type SortKey = 'sustain' | 'name' | 'start' | 'owner';

export type BoardFilters = {
  lifecycle: Set<Lifecycle>;
  provenance: Set<Provenance>;
  sustain: Set<Verdict>;
  mineOwner: boolean;
  mineHoles: boolean;
  people: Set<string>;
  roles: Set<string>;
  hideEmpty: boolean;
  sort: SortKey;
};

export const defaultFilters = (): BoardFilters => ({
  lifecycle: new Set<Lifecycle>(['future', 'active']), // default: hide closed
  provenance: new Set<Provenance>(['committed', 'proposed']),
  sustain: new Set<Verdict>(['sustainable', 'atRisk', 'uncovered']),
  mineOwner: false,
  mineHoles: false,
  people: new Set<string>(),
  roles: new Set<string>(),
  hideEmpty: false,
  sort: 'sustain',
});

export type CurrentUser = { resourceId: string | null; isStaffingManager: boolean };

// An uncovered demand "is mine" when I own it, or when it has no owner and I am
// the staffing manager (the ball defaults to staffing).
export function holeIsMine(d: DemandRow, me: CurrentUser): boolean {
  if (me.resourceId && d.ownerResourceId === me.resourceId) return true;
  return d.ownerResourceId === null && me.isStaffingManager;
}

export type BoardContext = {
  verdictOf: (p: BoardProject) => Verdict;
  me: CurrentUser;
  todayISO: string;
  domain: { minISO: string; maxISO: string };
};

export function filterProjects(
  projects: BoardProject[],
  f: BoardFilters,
  ctx: BoardContext,
): BoardProject[] {
  return projects.filter((p) => {
    if (!f.lifecycle.has(lifecycleOf(p, ctx.todayISO))) return false;
    if (!f.provenance.has(provenanceOf(p))) return false;
    if (!f.sustain.has(ctx.verdictOf(p))) return false;
    if (f.mineOwner && !(ctx.me.resourceId && p.ownerId === ctx.me.resourceId)) return false;
    if (f.mineHoles && !p.holes.some((h) => holeIsMine(h, ctx.me))) return false;
    if (f.people.size && ![...f.people].some((id) => p.people.includes(id))) return false;
    if (f.roles.size && ![...f.roles].some((r) => p.demands.some((d) => d.roleName === r))) return false;
    if (f.hideEmpty) {
      if (!p.from || !p.to) return false;
      if (!(p.from <= ctx.domain.maxISO && p.to >= ctx.domain.minISO)) return false;
    }
    return true;
  });
}

export function sortProjects(
  projects: BoardProject[],
  sort: SortKey,
  verdictOf: (p: BoardProject) => Verdict,
): BoardProject[] {
  const arr = [...projects];
  if (sort === 'sustain') {
    arr.sort(
      (a, b) =>
        VERDICT_SEVERITY[verdictOf(a)] - VERDICT_SEVERITY[verdictOf(b)] || a.name.localeCompare(b.name),
    );
  } else if (sort === 'name') {
    arr.sort((a, b) => a.name.localeCompare(b.name));
  } else if (sort === 'start') {
    arr.sort((a, b) => (a.from ?? '').localeCompare(b.from ?? ''));
  } else {
    arr.sort((a, b) => (a.ownerName ?? '').localeCompare(b.ownerName ?? '') || a.name.localeCompare(b.name));
  }
  return arr;
}

// Count of non-default filter facets (for the "Filtri" badge).
export function activeFilterCount(f: BoardFilters): number {
  let n = 0;
  if (f.lifecycle.size !== 3) n += 1;
  if (f.provenance.size !== 2) n += 1;
  if (f.sustain.size !== 3) n += 1;
  if (f.mineOwner) n += 1;
  if (f.mineHoles) n += 1;
  if (f.people.size) n += 1;
  if (f.roles.size) n += 1;
  if (f.hideEmpty) n += 1;
  return n;
}

// Distinct demand roles across the board (role filter facet).
export function allRoles(projects: BoardProject[]): string[] {
  const set = new Set<string>();
  for (const p of projects) for (const d of p.demands) set.add(d.roleName);
  return [...set].sort();
}

// ── Portfolio health (over ALL projects — the question is portfolio-level) ─

export type PortfolioHealth = {
  total: number;
  sustainable: number;
  atRisk: number;
  uncovered: number;
  totalHoles: number;
  overloadedPeople: number;
};

export function portfolioHealth(
  projects: BoardProject[],
  verdictOf: (p: BoardProject) => Verdict,
  peakByPerson: (resourceId: string) => number,
  overloadThreshold: number,
): PortfolioHealth {
  const counts: Record<Verdict, number> = { uncovered: 0, atRisk: 0, sustainable: 0 };
  for (const p of projects) counts[verdictOf(p)] += 1;
  const allPeople = new Set<string>();
  for (const p of projects) for (const id of p.people) allPeople.add(id);
  return {
    total: projects.length,
    sustainable: counts.sustainable,
    atRisk: counts.atRisk,
    uncovered: counts.uncovered,
    totalHoles: projects.reduce((a, p) => a + p.holes.length, 0),
    overloadedPeople: [...allPeople].filter((id) => peakByPerson(id) >= overloadThreshold).length,
  };
}

// A person's TENTATIVE blocks across the board (the hard-only profile excludes
// them; the inspector shows them as a "(proposto, non conteggiato)" note).
export function tentativeNotesOf(
  projects: BoardProject[],
  resourceId: string,
): { project: string; percent: number }[] {
  const out: { project: string; percent: number }[] = [];
  for (const p of projects) {
    for (const d of p.demands) {
      for (const b of d.coverage) {
        if (b.resourceId === resourceId && !b.hard) out.push({ project: p.name, percent: b.percent });
      }
    }
  }
  return out;
}

// ── Inspector target (view state shared by rows and the drawer) ──────────

export type InspectTarget =
  | { kind: 'project'; project: BoardProject }
  | { kind: 'person'; project: BoardProject; resourceId: string; block: CoverageBlock }
  | { kind: 'hole'; project: BoardProject; demand: DemandRow };

// ── Timeline domain helpers ──────────────────────────────────────────────

export function projectsExtent(
  projects: BoardProject[],
  fallback: { minISO: string; maxISO: string },
): { minISO: string; maxISO: string } {
  const dated = projects.filter((p) => p.from && p.to);
  if (!dated.length) return fallback;
  let min = dated[0]!.from!;
  let max = dated[0]!.to!;
  for (const p of dated) {
    if (p.from! < min) min = p.from!;
    if (p.to! > max) max = p.to!;
  }
  return { minISO: min, maxISO: max };
}
