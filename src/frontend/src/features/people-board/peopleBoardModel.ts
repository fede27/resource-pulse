// Persone board — pure view-model. No React, no network.
//
// The PEOPLE pivot of the coverage timeline: rows = persone, lanes = i loro
// progetti. Same data as the Progetti board read supply-first — only the
// OFFERTA is shown here; the uncovered demand lives on Progetti (and surfaces
// here only inside the drag-to-cover picker).
//
// The heatmap cell is the BUCKET AVERAGE utilization: allocated hours ÷
// capacity hours (hours = % × daily capacity, ADR-0026 — the capacity series
// already encodes the calendar). Only Hard blocks count by default; the
// "conteggia tentative" toggle adds the proposed ones. A bucket with ZERO
// capacity has UNDEFINED utilization (pct = null — 0h over 0h is neither 0%
// nor overload): when blocks touch it, that's the OFF-CALENDAR state
// ("fuori calendario"), rendered as a discreet hatch and excluded from
// peak/KPIs/band filters — 0h are counted, so it is presentation, not load.
// (The backend LoadPercent sentinel on /load is a domain signal for Phase 5
// and stays untouched; this page derives its cells client-side.)

import dayjs from 'dayjs';
import {
  AllocationStatus,
  type AllocationReadDto,
  type OpenDemandDto,
  type ResourceReadDto,
} from '@/api/generated/schemas';
import { isoWeek, type BoardGeo } from '@/components/board';
import type { Grain } from '@/components/timeline';
import { PLANE_H, ROW_H } from './PersonBoardRow.styles';
import { parseDurationHours } from '@/lib/duration';
import { bandIndexFor, overloadFloor, type LoadBand } from '@/lib/loadBands';

const ISO = 'YYYY-MM-DD';
const round1 = (n: number) => Math.round(n * 10) / 10;

// ── View-model types ─────────────────────────────────────────────────────

export type PersonBlock = {
  id: string;
  demandId: string;
  rootProjectId: string;
  projectName: string;
  from: string; // ISO date
  to: string; // ISO date, inclusive
  percent: number;
  hard: boolean;
  demandRoleName: string;
  resourceRoleName: string | null;
  mismatch: boolean;
  notes: string | null;
};

export type BoardPerson = {
  id: string;
  name: string;
  roleId: string | null;
  roleName: string | null;
  teamName: string | null;
};

export type PersonData = {
  person: BoardPerson;
  blocks: PersonBlock[];
  capacityByDay: ReadonlyMap<string, number>; // ISO date → hours
  weeklyCapH: number; // average capacity per week over the fetched range
};

export type BoardBucket = {
  from: string; // ISO, clamped to the domain
  toExcl: string; // ISO, exclusive
  x: number;
  w: number;
  label: string;
};

// ── Normalization ────────────────────────────────────────────────────────

// Root project node id = first segment of the materialized Path "/{rootId}/...".
export function rootIdFromPath(path: string | null | undefined): string {
  return path?.split('/').find((s) => s.length > 0) ?? '';
}

export function toBoardPerson(
  r: ResourceReadDto,
  roleNameById: ReadonlyMap<string, string>,
  teamNameById: ReadonlyMap<string, string>,
): BoardPerson {
  return {
    id: r.id ?? '',
    name: r.name ?? '—',
    roleId: r.roleId ?? null,
    roleName: r.roleId ? (roleNameById.get(r.roleId) ?? null) : null,
    teamName: r.teamId ? (teamNameById.get(r.teamId) ?? null) : null,
  };
}

// Root project id/name come resolved on the DTO since consolidation P3
// (gap doc §GAP 4 closed) — no client-side Path parsing + nodes join.
export function toPersonBlock(a: AllocationReadDto): PersonBlock {
  const resourceRoleName = a.resourceRoleName ?? null;
  const demandRoleName = a.demandRoleName ?? '';
  return {
    id: a.id ?? '',
    demandId: a.demandId ?? '',
    rootProjectId: a.rootProjectId ?? rootIdFromPath(a.projectNodePath),
    projectName: a.rootProjectName ?? '—',
    from: a.periodStart ?? '',
    to: a.periodEnd ?? '',
    percent: a.allocationPercent ?? 0,
    hard: a.status === AllocationStatus.Hard,
    demandRoleName,
    resourceRoleName,
    mismatch: resourceRoleName !== null && demandRoleName !== '' && resourceRoleName !== demandRoleName,
    notes: a.notes ?? null,
  };
}

// RLE expansion promoted to the shared lib with consolidation P3 (the Progetti
// board derives per-block hours from the same batch read); re-exported here so
// existing imports don't churn.
export { capacityMapFromSegments } from '@/lib/capacity';

// Average weekly capacity over [from, to] inclusive — the "≈ Nh/sett" label and
// the %↔ore bridge of the inspector.
export function weeklyCapacity(capacityByDay: ReadonlyMap<string, number>, from: string, to: string): number {
  const days = dayjs(to).diff(dayjs(from), 'day') + 1;
  if (days <= 0) return 0;
  let total = 0;
  for (const h of capacityByDay.values()) total += h;
  return round1((total / days) * 7);
}

// ── Buckets (aligned to the geo's unit ticks) ────────────────────────────

export function bucketsFromGeo(geo: BoardGeo): BoardBucket[] {
  const ticks = geo.unitTicks;
  return ticks
    .map((t, i) => ({
      from: t.iso < geo.minISO ? geo.minISO : t.iso,
      toExcl: ticks[i + 1]?.iso ?? geo.maxISO,
      x: t.x,
      w: t.w,
      label: t.label,
    }))
    .filter((b) => b.from < b.toExcl);
}

// ── Bucket utilization (hours truth: % × daily capacity) ─────────────────

export type BucketStat = {
  pct: number | null; // allocated ÷ capacity, %. Null = zero capacity (undefined).
  allocH: number;
  capH: number;
  active: boolean; // ≥1 block touches the bucket
  // Active blocks on a zero-capacity bucket (weekend at day grain, absence or
  // closure weeks): 0h counted, utilization undefined — a category of its own,
  // never overload.
  offCalendar: boolean;
};

export function bucketStat(data: PersonData, bucket: BoardBucket, includeTentative: boolean): BucketStat {
  const blocks = includeTentative ? data.blocks : data.blocks.filter((b) => b.hard);
  let allocH = 0;
  let capH = 0;
  let active = false;
  let d = dayjs(bucket.from);
  const end = dayjs(bucket.toExcl);
  while (d.isBefore(end)) {
    const iso = d.format(ISO);
    const cap = data.capacityByDay.get(iso) ?? 0;
    capH += cap;
    let rate = 0;
    for (const b of blocks) {
      if (b.from <= iso && iso <= b.to) {
        rate += b.percent;
        active = true;
      }
    }
    allocH += (rate / 100) * cap;
    d = d.add(1, 'day');
  }
  // Rounded at the source (1 decimal, like allocH/capH): band thresholds must
  // compare the SAME value the cells display. Unrounded, 70% × 8h × 5d
  // accumulates to 69.99999999999999 and a 70-floor band pill reads "under"
  // while every cell shows 70.
  const pct = capH > 0 ? round1((allocH / capH) * 100) : null;
  return { pct, allocH: round1(allocH), capH: round1(capH), active, offCalendar: pct === null && active };
}

export type PersonStats = { peak: number; min: number };

export function personStats(data: PersonData, buckets: BoardBucket[], includeTentative: boolean): PersonStats {
  // Zero-capacity buckets (pct null) don't participate: off-calendar is a
  // presentation category, not load — it must not flip a person's row state.
  let peak = 0;
  let min = Number.POSITIVE_INFINITY;
  for (const bk of buckets) {
    const { pct } = bucketStat(data, bk, includeTentative);
    if (pct === null) continue;
    if (pct > peak) peak = pct;
    if (pct < min) min = pct;
  }
  return { peak, min: Number.isFinite(min) ? min : 0 };
}

// ── Bucket composition (inspector: per-project share of the average) ─────

export type BucketShare = {
  rootProjectId: string;
  projectName: string;
  hours: number;
  pct: number; // share of the bucket average — shares sum to the cell value
  allTentative: boolean;
};

export function bucketComposition(
  data: PersonData,
  bucket: BoardBucket,
  includeTentative: boolean,
): { total: BucketStat; byProject: BucketShare[] } {
  const total = bucketStat(data, bucket, includeTentative);
  const blocks = includeTentative ? data.blocks : data.blocks.filter((b) => b.hard);
  const acc = new Map<string, { name: string; hours: number; anyHard: boolean }>();
  let d = dayjs(bucket.from);
  const end = dayjs(bucket.toExcl);
  while (d.isBefore(end)) {
    const iso = d.format(ISO);
    const cap = data.capacityByDay.get(iso) ?? 0;
    if (cap > 0) {
      for (const b of blocks) {
        if (b.from <= iso && iso <= b.to) {
          const cur = acc.get(b.rootProjectId) ?? { name: b.projectName, hours: 0, anyHard: false };
          cur.hours += (b.percent / 100) * cap;
          cur.anyHard = cur.anyHard || b.hard;
          acc.set(b.rootProjectId, cur);
        }
      }
    }
    d = d.add(1, 'day');
  }
  const byProject = [...acc.entries()]
    .map(([rootProjectId, v]) => ({
      rootProjectId,
      projectName: v.name,
      hours: round1(v.hours),
      pct: total.capH > 0 ? (v.hours / total.capH) * 100 : 0,
      allTentative: !v.anyHard,
    }))
    .sort((a, b) => b.hours - a.hours || a.projectName.localeCompare(b.projectName));
  return { total, byProject };
}

// ── Lanes (expanded row: one per root project) ───────────────────────────

export type ProjectLane = {
  rootProjectId: string;
  projectName: string;
  blocks: PersonBlock[];
};

export function personLanes(blocks: PersonBlock[]): ProjectLane[] {
  const byRoot = new Map<string, PersonBlock[]>();
  for (const b of blocks) {
    const list = byRoot.get(b.rootProjectId) ?? [];
    list.push(b);
    byRoot.set(b.rootProjectId, list);
  }
  return [...byRoot.entries()]
    .map(([rootProjectId, list]) => ({
      rootProjectId,
      projectName: list[0]?.projectName ?? '—',
      blocks: [...list].sort((a, b) => a.from.localeCompare(b.from)),
    }))
    .sort((a, b) => (a.blocks[0]?.from ?? '').localeCompare(b.blocks[0]?.from ?? ''));
}

// ── Row height (vertical windowing) ──────────────────────────────────────
// Derived from state, never measured: heatmap row + (expanded) lanes-container
// border-top + one PLANE_H per project lane plus the free-capacity lane
// (border-box) + the block's bottom border. Must mirror PersonBoardRow's DOM
// and PersonBoardRow.styles.ts exactly — any height/border change goes through
// the shared tokens or updates this formula in lockstep.
export function personRowHeight(data: PersonData, expanded: boolean): number {
  return ROW_H + (expanded ? 1 + (personLanes(data.blocks).length + 1) * PLANE_H : 0) + 1;
}

// ── Inspector focus period (revised design: the inspector always answers
// "quando?" explicitly — Ora / Prossimi / Tutto / Scegli…, or the clicked cell)

export type PeriodMode = 'cell' | 'current' | 'next' | 'all' | 'custom';
export type FocusPeriod = { from: string; toExcl: string };

// Index of the bucket containing today; else the first bucket after today;
// else 0 (horizon entirely in the past).
export function todayBucketIdx(buckets: BoardBucket[], todayISO: string): number {
  let i = buckets.findIndex((bk) => bk.from <= todayISO && todayISO < bk.toExcl);
  if (i < 0) i = buckets.findIndex((bk) => bk.toExcl > todayISO);
  return i < 0 ? 0 : i;
}

// 'current' = today's bucket · 'next' = today's bucket + the following 3 (at
// the board grain) · 'all' = the visible horizon · 'custom' = user-picked.
export function resolvePeriod(
  mode: PeriodMode,
  buckets: BoardBucket[],
  todayISO: string,
  custom: FocusPeriod | null,
  cell: BoardBucket | null,
): FocusPeriod {
  if (buckets.length === 0) {
    return { from: todayISO, toExcl: dayjs(todayISO).add(7, 'day').format(ISO) };
  }
  const last = buckets.length - 1;
  if (mode === 'cell' && cell) return { from: cell.from, toExcl: cell.toExcl };
  if (mode === 'all') return { from: buckets[0]!.from, toExcl: buckets[last]!.toExcl };
  if (mode === 'custom' && custom) return custom;
  const i = todayBucketIdx(buckets, todayISO);
  if (mode === 'next') {
    const j = Math.min(last, i + 3);
    return { from: buckets[i]!.from, toExcl: buckets[j]!.toExcl };
  }
  return { from: buckets[i]!.from, toExcl: buckets[i]!.toExcl };
}

// Buckets (at the board grain) the focus period spans — the "media N
// settimane/mesi" note counts these, not the breakdown rows.
export function bucketsInPeriod(buckets: BoardBucket[], period: FocusPeriod): number {
  return buckets.filter((bk) => bk.from < period.toExcl && bk.toExcl > period.from).length;
}

// ── Sub-grain distribution (adaptive: > ~10 days → weeks, else days) ──────

export type SubBucket = { from: string; toExcl: string; label: string };

export function breakdownGrain(from: string, toExcl: string): 'week' | 'day' | null {
  const span = dayjs(toExcl).diff(dayjs(from), 'day');
  return span > 10 ? 'week' : span > 1 ? 'day' : null;
}

export function subPeriods(from: string, toExcl: string, sub: 'week' | 'day'): SubBucket[] {
  const out: SubBucket[] = [];
  const end = dayjs(toExcl);
  if (sub === 'week') {
    let d = dayjs(from).subtract((dayjs(from).day() + 6) % 7, 'day');
    while (d.isBefore(end)) {
      const next = d.add(7, 'day');
      const f = d.format(ISO) < from ? from : d.format(ISO);
      const t = next.isAfter(end) ? toExcl : next.format(ISO);
      out.push({ from: f, toExcl: t, label: `S${isoWeek(d)}` });
      d = next;
    }
  } else {
    let d = dayjs(from);
    while (d.isBefore(end)) {
      out.push({ from: d.format(ISO), toExcl: d.add(1, 'day').format(ISO), label: d.format('D MMM') });
      d = d.add(1, 'day');
    }
  }
  return out;
}

// ── Filters, grouping, sorting ───────────────────────────────────────────

export type GroupBy = 'role' | 'team';
export type PeopleSort = 'severity' | 'idle' | 'name' | 'role';

export function matchesQuery(p: BoardPerson, query: string): boolean {
  const q = query.trim().toLowerCase();
  if (!q) return true;
  return p.name.toLowerCase().includes(q) || (p.roleName ?? '').toLowerCase().includes(q);
}

// Band filter: the person matches when ANY visible bucket's average falls in a
// selected band (indices into the configured bands — config-driven, never a
// hard-coded 4-band set). Empty selection = no filter.
export function matchesBands(
  data: PersonData,
  buckets: BoardBucket[],
  selected: ReadonlySet<number>,
  bands: LoadBand[],
  includeTentative: boolean,
): boolean {
  if (selected.size === 0) return true;
  return buckets.some((bk) => {
    const { pct } = bucketStat(data, bk, includeTentative);
    if (pct === null) return false; // off-calendar/no-capacity: no band
    return selected.has(bandIndexFor(pct, bands));
  });
}

export function sortPeople(
  list: PersonData[],
  sort: PeopleSort,
  statsOf: (id: string) => PersonStats,
): PersonData[] {
  const arr = [...list];
  const name = (d: PersonData) => d.person.name;
  if (sort === 'severity') {
    arr.sort((a, b) => statsOf(b.person.id).peak - statsOf(a.person.id).peak || name(a).localeCompare(name(b)));
  } else if (sort === 'idle') {
    arr.sort((a, b) => statsOf(a.person.id).min - statsOf(b.person.id).min || name(a).localeCompare(name(b)));
  } else if (sort === 'name') {
    arr.sort((a, b) => name(a).localeCompare(name(b)));
  } else {
    // By role, people without a role last.
    arr.sort((a, b) => {
      const ra = a.person.roleName;
      const rb = b.person.roleName;
      if (ra === null && rb !== null) return 1;
      if (ra !== null && rb === null) return -1;
      return (ra ?? '').localeCompare(rb ?? '') || name(a).localeCompare(name(b));
    });
  }
  return arr;
}

export type PeopleGroup = { key: string; label: string | null; people: PersonData[] };

// Groups preserve the incoming (already sorted) order inside each group; the
// groups themselves sort alphabetically with the "no role/team" group last.
export function groupPeople(list: PersonData[], groupBy: GroupBy): PeopleGroup[] {
  const groups = new Map<string, PeopleGroup>();
  for (const d of list) {
    const label = groupBy === 'team' ? d.person.teamName : d.person.roleName;
    const key = label ?? '';
    const g = groups.get(key) ?? { key, label, people: [] };
    g.people.push(d);
    groups.set(key, g);
  }
  return [...groups.values()].sort((a, b) => {
    if (a.label === null) return 1;
    if (b.label === null) return -1;
    return a.label.localeCompare(b.label);
  });
}

// ── Portfolio KPIs (over ALL people, unfiltered) ─────────────────────────

export type PeopleKpis = { overloaded: number; underused: number };

// Overloaded: peak in the open-ended overload band. Underused: never reaches
// the second band's floor (the first band is the under-band by construction —
// bands are half-open and start at 0, ADR-0020).
export function peopleKpis(
  all: PersonData[],
  statsOf: (id: string) => PersonStats,
  bands: LoadBand[],
): PeopleKpis {
  const overloadAt = overloadFloor(bands);
  const healthyAt = bands[1]?.lowerBound ?? 0;
  let overloaded = 0;
  let underused = 0;
  for (const d of all) {
    const { peak } = statsOf(d.person.id);
    if (peak >= overloadAt) overloaded += 1;
    else if (peak < healthyAt) underused += 1;
  }
  return { overloaded, underused };
}

// ── Drag-to-cover helpers ────────────────────────────────────────────────

// Inverse of the geo's xPx for the free-capacity lane drag.
export function isoAtX(geo: BoardGeo, x: number): string {
  const days = Math.round(x / geo.dayPx);
  const clamped = Math.max(0, Math.min(geo.totalDays, days));
  return dayjs(geo.minISO).add(clamped, 'day').format(ISO);
}

// Snap a dragged date to the natural boundary of the bucket grain.
export function snapISO(iso: string, grain: Grain): string {
  const d = dayjs(iso);
  if (grain === 'day') return iso;
  if (grain === 'week') {
    const monday = d.subtract((d.day() + 6) % 7, 'day');
    const off = d.diff(monday, 'day');
    return (off >= 4 ? monday.add(7, 'day') : monday).format(ISO);
  }
  const first = d.startOf('month');
  const next = first.add(1, 'month');
  return (d.diff(first, 'day') >= next.diff(d, 'day') ? next : first).format(ISO);
}

// Capacity hours in an inclusive window — the denominator of the proposal %.
export function capacityInWindow(
  capacityByDay: ReadonlyMap<string, number>,
  from: string,
  toIncl: string,
): number {
  let total = 0;
  let d = dayjs(from);
  const end = dayjs(toIncl);
  while (!d.isAfter(end)) {
    total += capacityByDay.get(d.format(ISO)) ?? 0;
    d = d.add(1, 'day');
  }
  return round1(total);
}

// Default proposal rate: enough of the person to absorb the demand's residual
// within the dragged window (clamped 10–100, stepped by 5 — presentation slack,
// mirroring the prototype). Best-effort demands have no residual → flat 40%.
export function proposalPercent(residualH: number | null, capHWindow: number): number {
  if (residualH === null || residualH <= 0 || capHWindow <= 0) return 40;
  return Math.min(100, Math.max(10, Math.round(((residualH / capHWindow) * 100) / 5) * 5));
}

// ── Open demands (the picker's rows, from GET /api/demands/open) ─────────

export type OpenDemand = {
  demandId: string;
  projectNodeId: string;
  rootProjectId: string;
  rootProjectName: string;
  roleName: string;
  residualH: number | null; // null = best-effort (no target ⇒ no defined gap)
  coveredH: number;
  notes: string | null;
};

export function toOpenDemand(d: OpenDemandDto): OpenDemand {
  return {
    demandId: d.demandId ?? '',
    projectNodeId: d.projectNodeId ?? '',
    rootProjectId: d.rootProjectId ?? '',
    rootProjectName: d.rootProjectName ?? '—',
    roleName: d.roleName ?? '—',
    residualH: d.gapHours != null ? round1(parseDurationHours(d.gapHours)) : null,
    coveredH: round1(parseDurationHours(d.coveredHours)),
    notes: d.notes ?? null,
  };
}
