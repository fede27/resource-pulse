// Pure view-model for the "Disponibilità" timeline.
//
// The effective hours per person-day are read authoritatively from the batch
// capacity endpoint (GET /api/resources/capacity — same run-length source the
// Persone/Progetti boards use). What this module adds is the *decomposition and
// state* the inspector explains — base pattern, company closure, ferie
// (Absence) and straordinari (ExtraTime) — computed client-side by faithfully
// mirroring the backend `CapacityCalculator` composition:
//   pattern = resource.workWindows (override) else calendar.workWindows
//   closure covering the day zeros the base
//   effective = max(0, base − Σ absences + Σ extras)   (the batch's number)
// Nothing here re-derives the authoritative effective hours; it explains them.

import dayjs from 'dayjs';
import {
  AdjustmentType,
  type BusinessCalendarReadDto,
  type CompanyClosureReadDto,
  type IndividualAdjustmentDto,
  type ResourceReadDto,
  type WorkWindowDto,
} from '@/api/generated/schemas';
import type { Grain as TimelineGrain } from '@/components/timeline';
import { parseDurationHours } from '@/lib/duration';

const ISO = 'YYYY-MM-DD';

export type DayState = 'work' | 'ferie' | 'extra' | 'closure' | 'off';

export type DayInfo = {
  iso: string;
  weekend: boolean;
  /** Nominal hours from the assigned pattern for this weekday, before closures. */
  baseHours: number;
  /** The closure covering this day, if any (base is zeroed when present). */
  closure: CompanyClosureReadDto | null;
  /** Absence adjustments touching this day. */
  ferie: IndividualAdjustmentDto[];
  /** ExtraTime adjustments touching this day. */
  extra: IndividualAdjustmentDto[];
  /** Absence hours applied this day for display (capped at the available base). */
  ferieHours: number;
  /** Extra hours added this day. */
  extraHours: number;
  state: DayState;
};

// Minutes since midnight for a "HH:mm[:ss]" wire time.
function timeToMinutes(t: string | undefined): number {
  if (!t) return 0;
  const [h = '0', m = '0'] = t.split(':');
  return Number(h) * 60 + Number(m);
}

function windowHours(w: WorkWindowDto): number {
  return (timeToMinutes(w.endTime) - timeToMinutes(w.startTime)) / 60;
}

// True when the window applies to `iso`: same weekday and validity covers it.
function windowAppliesTo(w: WorkWindowDto, iso: string, dow: number): boolean {
  if (w.dayOfWeek !== dow) return false;
  if (w.validFrom && iso < w.validFrom) return false;
  if (w.validTo && iso > w.validTo) return false;
  return true;
}

// The pattern in force for the resource: per-resource overrides win over the
// assigned calendar (mirrors CapacityCalculator).
export function patternFor(
  resource: ResourceReadDto,
  calendar: BusinessCalendarReadDto | undefined,
): WorkWindowDto[] {
  const overrides = resource.workWindows ?? [];
  if (overrides.length > 0) return overrides;
  return calendar?.workWindows ?? [];
}

export function baseHoursOn(pattern: WorkWindowDto[], iso: string): number {
  const dow = dayjs(iso).day();
  return pattern
    .filter((w) => windowAppliesTo(w, iso, dow))
    .reduce((sum, w) => sum + windowHours(w), 0);
}

export function closureOn(
  closures: CompanyClosureReadDto[],
  iso: string,
): CompanyClosureReadDto | null {
  return (
    closures.find(
      (c) => c.dateFrom && c.dateTo && c.dateFrom <= iso && iso <= c.dateTo,
    ) ?? null
  );
}

export function adjustmentsOn(
  adjustments: IndividualAdjustmentDto[],
  iso: string,
): IndividualAdjustmentDto[] {
  return adjustments.filter(
    (a) => a.dateFrom && a.dateTo && a.dateFrom <= iso && iso <= a.dateTo,
  );
}

const round1 = (n: number) => Math.round(n * 10) / 10;

// Decimal hours → the backend's constant TimeSpan wire format ("HH:mm:ss").
// Round to the minute; the domain stores an interval and parses either form.
export function hoursToWire(hours: number): string {
  const totalMinutes = Math.max(0, Math.round(hours * 60));
  const hh = Math.floor(totalMinutes / 60);
  const mm = totalMinutes % 60;
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${pad(hh)}:${pad(mm)}:00`;
}

// Full explanation of one person-day (state + decomposition). Effective hours
// are NOT computed here — they come from the batch capacity read.
export function dayInfo(
  resource: ResourceReadDto,
  calendar: BusinessCalendarReadDto | undefined,
  closures: CompanyClosureReadDto[],
  iso: string,
): DayInfo {
  const pattern = patternFor(resource, calendar);
  const baseHours = baseHoursOn(pattern, iso);
  const closure = closureOn(closures, iso);
  const effectiveBase = closure ? 0 : baseHours;

  const touching = adjustmentsOn(resource.adjustments ?? [], iso);
  const ferie = touching.filter((a) => a.type === AdjustmentType.Absence);
  const extra = touching.filter((a) => a.type === AdjustmentType.ExtraTime);

  const absenceRaw = ferie.reduce(
    (sum, a) => sum + (a.hours != null ? parseDurationHours(a.hours) : effectiveBase),
    0,
  );
  const ferieHours = Math.min(effectiveBase, absenceRaw);
  const extraHours = extra.reduce((sum, a) => sum + parseDurationHours(a.hours), 0);

  const dow = dayjs(iso).day();
  const weekend = dow === 0 || dow === 6;

  let state: DayState = 'off';
  if (baseHours > 0 && !closure) state = 'work';
  if (closure) state = 'closure';
  if (ferie.length > 0 && (closure || baseHours > 0)) state = 'ferie';
  if (extra.length > 0) state = 'extra';

  return {
    iso,
    weekend,
    baseHours: round1(baseHours),
    closure,
    ferie,
    extra,
    ferieHours: round1(ferieHours),
    extraHours: round1(extraHours),
    state,
  };
}

// ── Bucketing ──────────────────────────────────────────────────────────────

// Same three grains as the boards (`@/components/timeline`) — the availability
// grid reads the domain through the shared time filter, so its bucket unit must
// be the same vocabulary.
export type Grain = TimelineGrain;

export type Bucket = { from: string; to: string };

export function mondayOf(iso: string): string {
  const d = dayjs(iso);
  const dow = (d.day() + 6) % 7; // 0 = Monday
  return d.subtract(dow, 'day').format(ISO);
}

export function addDays(iso: string, n: number): string {
  return dayjs(iso).add(n, 'day').format(ISO);
}

// Aligned buckets covering the inclusive domain [fromISO, toISO]: single days,
// ISO weeks (Monday-anchored) or calendar months. The first and last bucket may
// extend past the domain edges — a half week is not a unit anyone reads, so the
// bucket wins over the edge.
export function buildBuckets(fromISO: string, toISO: string, grain: Grain): Bucket[] {
  const out: Bucket[] = [];
  const end = dayjs(toISO);
  if (end.isBefore(dayjs(fromISO))) return out;

  if (grain === 'day') {
    for (let d = dayjs(fromISO); !d.isAfter(end); d = d.add(1, 'day')) {
      const from = d.format(ISO);
      out.push({ from, to: from });
    }
    return out;
  }

  if (grain === 'month') {
    for (let d = dayjs(fromISO).startOf('month'); !d.isAfter(end); d = d.add(1, 'month')) {
      out.push({ from: d.format(ISO), to: d.endOf('month').format(ISO) });
    }
    return out;
  }

  for (let d = dayjs(mondayOf(fromISO)); !d.isAfter(end); d = d.add(7, 'day')) {
    const from = d.format(ISO);
    out.push({ from, to: addDays(from, 6) });
  }
  return out;
}

// The extent of what this board actually has to show. Capacity is defined every
// day, so "fit to the content" would be a no-op; what carries information is the
// exceptions — ferie, straordinari, chiusure. Null when there are none.
export function exceptionsExtent(
  resources: ResourceReadDto[],
  closures: CompanyClosureReadDto[],
): { minISO: string; maxISO: string } | null {
  let min: string | null = null;
  let max: string | null = null;
  const widen = (from: string | null | undefined, to: string | null | undefined) => {
    if (!from || !to) return;
    if (min === null || from < min) min = from;
    if (max === null || to > max) max = to;
  };
  for (const r of resources) for (const a of r.adjustments ?? []) widen(a.dateFrom, a.dateTo);
  for (const c of closures) widen(c.dateFrom, c.dateTo);
  return min !== null && max !== null ? { minISO: min, maxISO: max } : null;
}

export function bucketDays(bucket: Bucket): string[] {
  const out: string[] = [];
  const end = dayjs(bucket.to);
  for (let d = dayjs(bucket.from); !d.isAfter(end); d = d.add(1, 'day')) {
    out.push(d.format(ISO));
  }
  return out;
}

export type BucketAgg = {
  /** Effective hours over the bucket (from the authoritative capacity map). */
  hours: number;
  state: DayState;
  hasFerie: boolean;
  hasExtra: boolean;
  hasClosure: boolean;
};

// Aggregate one bucket for a person: effective hours from the capacity map,
// state rolled up from the per-day decomposition.
export function bucketAgg(
  resource: ResourceReadDto,
  calendar: BusinessCalendarReadDto | undefined,
  closures: CompanyClosureReadDto[],
  capacityByDay: ReadonlyMap<string, number>,
  bucket: Bucket,
): BucketAgg {
  let hours = 0;
  let base = 0;
  let hasFerie = false;
  let hasExtra = false;
  let hasClosure = false;
  for (const iso of bucketDays(bucket)) {
    hours += capacityByDay.get(iso) ?? 0;
    const di = dayInfo(resource, calendar, closures, iso);
    base += di.closure ? 0 : di.baseHours;
    if (di.ferie.length > 0) hasFerie = true;
    if (di.extra.length > 0) hasExtra = true;
    if (di.closure) hasClosure = true;
  }
  let state: DayState = 'off';
  if (base > 0 || hours > 0) state = 'work';
  if (hasClosure && hours === 0) state = 'closure';
  if (hasFerie && hours < base) state = 'ferie';
  if (hasExtra) state = 'extra';
  return { hours: round1(hours), state, hasFerie, hasExtra, hasClosure };
}

export function sumEffective(
  capacityByDay: ReadonlyMap<string, number>,
  fromISO: string,
  toISO: string,
): number {
  let sum = 0;
  const end = dayjs(toISO);
  for (let d = dayjs(fromISO); !d.isAfter(end); d = d.add(1, 'day')) {
    sum += capacityByDay.get(d.format(ISO)) ?? 0;
  }
  return round1(sum);
}

// Nominal (calendar/override) hours over a range, ignoring closures and
// adjustments — the "what the pattern says" baseline shown next to effective.
export function sumNominal(
  resource: ResourceReadDto,
  calendar: BusinessCalendarReadDto | undefined,
  fromISO: string,
  toISO: string,
): number {
  const pattern = patternFor(resource, calendar);
  let sum = 0;
  const end = dayjs(toISO);
  for (let d = dayjs(fromISO); !d.isAfter(end); d = d.add(1, 'day')) {
    sum += baseHoursOn(pattern, d.format(ISO));
  }
  return round1(sum);
}

// Weekly nominal hours of a pattern (7 consecutive days from any Monday).
export function weeklyNominal(
  resource: ResourceReadDto,
  calendar: BusinessCalendarReadDto | undefined,
): number {
  const monday = mondayOf(dayjs().format(ISO));
  return sumNominal(resource, calendar, monday, addDays(monday, 6));
}

// Weekly nominal hours of a raw window set (Mon–Sun of the current week) — used
// by the calendar picker to summarize each calendar's magnitude.
export function windowsWeeklyHours(windows: WorkWindowDto[]): number {
  const monday = mondayOf(dayjs().format(ISO));
  let sum = 0;
  for (let i = 0; i < 7; i += 1) sum += baseHoursOn(windows, addDays(monday, i));
  return round1(sum);
}

// ── Team grouping ────────────────────────────────────────────────────────────

export type TeamGroup<T> = { teamId: string | null; teamName: string; members: T[] };

// Group items by team, preserving first-seen team order; null team last.
export function groupByTeam<T>(
  items: T[],
  teamIdOf: (item: T) => string | null | undefined,
  teamNameById: ReadonlyMap<string, string>,
  noTeamLabel: string,
): TeamGroup<T>[] {
  const order: (string | null)[] = [];
  const byTeam = new Map<string | null, T[]>();
  for (const p of items) {
    const key = teamIdOf(p) ?? null;
    if (!byTeam.has(key)) {
      byTeam.set(key, []);
      order.push(key);
    }
    byTeam.get(key)!.push(p);
  }
  // null team sorts last.
  order.sort((a, b) => (a === null ? 1 : b === null ? -1 : 0));
  return order.map((key) => ({
    teamId: key,
    teamName: key === null ? noTeamLabel : (teamNameById.get(key) ?? noTeamLabel),
    members: byTeam.get(key) ?? [],
  }));
}
