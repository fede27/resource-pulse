// Pure model for the Team load heatmap. No React, no network.
//
// Load is read as a function of two backend series per resource:
//   - allocated hours  (GET /api/resources/{id}/load   → DailyLoadDto.hours)
//   - capacity hours   (GET /api/resources/{id}/capacity → DailyCapacityDto.hours)
// A bucket's load% = Σ allocated / Σ capacity over its days. Summing hours (not
// averaging percentages) is what makes team / overall rows compose correctly and
// sidesteps the per-day `loadPercent` zero-capacity sentinel entirely.
//
// Thresholds (bands) and the cell grain come from the org configuration
// (/api/config/load-bands and /api/config/bucketing) — never hard-coded here.

import type { Dayjs } from 'dayjs';
import dayjs from 'dayjs';
import { BucketGrain, type LoadBandDto } from '@/api/generated/schemas';

// ── Duration parsing ─────────────────────────────────────────────────────
// .NET TimeSpan serializes as an ISO-8601 duration ("PT8H", "PT7H30M"); we also
// accept the legacy "c" format ("08:00:00") defensively.
export function parseDurationHours(s: string | null | undefined): number {
  if (!s) return 0;
  const iso = /^(-)?P(?:(\d+)D)?(?:T(?:(\d+)H)?(?:(\d+)M)?(?:([\d.]+)S)?)?$/i.exec(s);
  if (iso) {
    const sign = iso[1] ? -1 : 1;
    return (
      sign *
      (Number(iso[2] ?? 0) * 24 +
        Number(iso[3] ?? 0) +
        Number(iso[4] ?? 0) / 60 +
        Number(iso[5] ?? 0) / 3600)
    );
  }
  const c = /^(-)?(?:(\d+)\.)?(\d{1,2}):(\d{2}):(\d{2})(?:\.(\d+))?$/.exec(s);
  if (c) {
    const sign = c[1] ? -1 : 1;
    return (
      sign *
      (Number(c[2] ?? 0) * 24 +
        Number(c[3] ?? 0) +
        Number(c[4] ?? 0) / 60 +
        Number(c[5] ?? 0) / 3600)
    );
  }
  return 0;
}

// ── Bands → colors ───────────────────────────────────────────────────────
export type LoadBand = { label: string; lowerBound: number };

export function normalizeBands(bands: LoadBandDto[] | null | undefined): LoadBand[] {
  const list = (bands ?? [])
    .map((b) => ({ label: b.label ?? '—', lowerBound: b.lowerBound ?? 0 }))
    .sort((a, b) => a.lowerBound - b.lowerBound);
  return list.length ? list : [{ label: '—', lowerBound: 0 }];
}

// Severity ramp: neutral → green → lime → amber → red. The last band is always
// red (overload), the first always neutral; the middle bands interpolate. This
// keeps the visual honest for any 3- or 5-band configuration without baking a
// fixed palette into the cells.
const LOAD_STOPS = [
  { solid: '#bfbfbf', bg: '#eef0f2', fg: 'rgba(0,0,0,.5)' },
  { solid: '#52c41a', bg: '#d9f7be', fg: '#237804' },
  { solid: '#a0d911', bg: '#f4ffb8', fg: '#5b8c00' },
  { solid: '#faad14', bg: '#fff1b8', fg: '#874d00' },
  { solid: '#ff4d4f', bg: '#ffccc7', fg: '#a8071a' },
] as const;

export type CellColor = { bg: string; fg: string; solid: string; empty: boolean };

export function bandStop(index: number, total: number): (typeof LOAD_STOPS)[number] {
  if (total <= 1) return LOAD_STOPS[4];
  if (index <= 0) return LOAD_STOPS[0];
  if (index >= total - 1) return LOAD_STOPS[4];
  const middleCount = total - 2;
  const pos = index - 1;
  const t = middleCount <= 1 ? 0.5 : pos / (middleCount - 1);
  return LOAD_STOPS[1 + Math.round(t * 2)]!;
}

export function bandIndexFor(pct: number, bands: LoadBand[]): number {
  let hit = 0;
  for (let i = 0; i < bands.length; i += 1) {
    if (pct >= bands[i]!.lowerBound) hit = i;
  }
  return hit;
}

export function loadColor(pct: number, bands: LoadBand[]): CellColor {
  if (!Number.isFinite(pct)) {
    const s = LOAD_STOPS[4];
    return { bg: s.bg, fg: s.fg, solid: s.solid, empty: false };
  }
  if (pct <= 0.5) {
    return { bg: '#fbfbfb', fg: 'rgba(0,0,0,.25)', solid: '#f0f0f0', empty: true };
  }
  const s = bandStop(bandIndexFor(pct, bands), bands.length);
  return { bg: s.bg, fg: s.fg, solid: s.solid, empty: false };
}

export function bandRangeLabel(bands: LoadBand[], i: number): string {
  const b = bands[i]!;
  const next = bands[i + 1];
  return next ? `${b.lowerBound}–${next.lowerBound}%` : `${b.lowerBound}%+`;
}

export type LegendStop = LoadBand & { range: string; solid: string };
export function legendStops(bands: LoadBand[]): LegendStop[] {
  return bands.map((b, i) => ({
    ...b,
    range: bandRangeLabel(bands, i),
    solid: bandStop(i, bands.length).solid,
  }));
}

// ── Time buckets ─────────────────────────────────────────────────────────
export type Grain = 'day' | 'week' | 'month';

export const grainOf = (g: BucketGrain | undefined): Grain =>
  g === BucketGrain.Day ? 'day' : g === BucketGrain.Month ? 'month' : 'week';

export type Bucket = {
  idx: number;
  grain: Grain;
  start: Dayjs;
  end: Dayjs; // inclusive
  groupKey: string; // consecutive runs form the primary header (year, or month for day grain)
  groupLabel: string;
  label: string; // secondary header (W23 / giu / 18)
  isToday: boolean;
  dates: string[]; // YYYY-MM-DD covered, clipped to the fetch window
};

const mondayOf = (d: Dayjs): Dayjs => d.subtract((d.day() + 6) % 7, 'day').startOf('day');

// One window covers every grain so toggling grain never refetches. The backend
// caps each load/capacity request at 366 inclusive days, so this span must stay
// under it: 52 weeks = 364 days → 365 inclusive. Don't widen past 52 weeks
// without paging the per-resource fetches.
export function fetchWindow(today: Dayjs): { from: Dayjs; to: Dayjs } {
  const from = mondayOf(today).subtract(7, 'day');
  return { from, to: from.add(52, 'week') };
}

function isoWeekNumber(d: Dayjs): number {
  const date = new Date(Date.UTC(d.year(), d.month(), d.date()));
  const day = (date.getUTCDay() + 6) % 7;
  date.setUTCDate(date.getUTCDate() - day + 3);
  const firstThursday = new Date(Date.UTC(date.getUTCFullYear(), 0, 4));
  const ftDay = (firstThursday.getUTCDay() + 6) % 7;
  firstThursday.setUTCDate(firstThursday.getUTCDate() - ftDay + 3);
  return 1 + Math.round((date.getTime() - firstThursday.getTime()) / (7 * 86400000));
}

function clippedDates(start: Dayjs, end: Dayjs, lo: Dayjs, hi: Dayjs): string[] {
  let cur = start.isBefore(lo) ? lo : start;
  const last = end.isAfter(hi) ? hi : end;
  const out: string[] = [];
  while (!cur.isAfter(last)) {
    out.push(cur.format('YYYY-MM-DD'));
    cur = cur.add(1, 'day');
  }
  return out;
}

// Build the visible columns for a grain. The day grain shows only a short
// near-term window (a full year of daily columns is unreadable); week and month
// span the whole fetch window.
export function buildBuckets(grain: Grain, today: Dayjs): Bucket[] {
  const { from, to } = fetchWindow(today);
  const todayStr = today.format('YYYY-MM-DD');
  const out: Bucket[] = [];

  if (grain === 'day') {
    const start = from;
    const end = today.add(35, 'day');
    let cur = start;
    let i = 0;
    while (!cur.isAfter(end)) {
      const ds = cur.format('YYYY-MM-DD');
      out.push({
        idx: i,
        grain,
        start: cur,
        end: cur,
        groupKey: cur.format('YYYY-MM'),
        groupLabel: cur.format('MMM YYYY'),
        label: cur.format('D'),
        isToday: ds === todayStr,
        dates: [ds],
      });
      cur = cur.add(1, 'day');
      i += 1;
    }
    return out;
  }

  if (grain === 'month') {
    let cur = from.startOf('month');
    const end = to.startOf('month');
    let i = 0;
    while (!cur.isAfter(end)) {
      const mStart = cur.startOf('month');
      const mEnd = cur.endOf('month');
      out.push({
        idx: i,
        grain,
        start: mStart,
        end: mEnd,
        groupKey: String(cur.year()),
        groupLabel: String(cur.year()),
        label: cur.format('MMM'),
        isToday: today.year() === cur.year() && today.month() === cur.month(),
        dates: clippedDates(mStart, mEnd, from, to),
      });
      cur = cur.add(1, 'month');
      i += 1;
    }
    return out;
  }

  // week
  let cur = mondayOf(from);
  let i = 0;
  while (!cur.isAfter(to)) {
    const wEnd = cur.add(6, 'day');
    out.push({
      idx: i,
      grain,
      start: cur,
      end: wEnd,
      groupKey: String(cur.year()),
      groupLabel: String(cur.year()),
      label: `W${String(isoWeekNumber(cur)).padStart(2, '0')}`,
      isToday: !today.isBefore(cur) && !today.isAfter(wEnd),
      dates: clippedDates(cur, wEnd, from, to),
    });
    cur = cur.add(7, 'day');
    i += 1;
  }
  return out;
}

// Consecutive buckets sharing a groupKey form one primary-header span.
export type Group = { key: string; label: string; count: number };
export function groupRuns(buckets: Bucket[]): Group[] {
  const groups: Group[] = [];
  for (const b of buckets) {
    const last = groups[groups.length - 1];
    if (last && last.key === b.groupKey) last.count += 1;
    else groups.push({ key: b.groupKey, label: b.groupLabel, count: 1 });
  }
  return groups;
}

export function todayBucketIdx(buckets: Bucket[]): number {
  return buckets.findIndex((b) => b.isToday);
}

export function bucketTooltip(b: Bucket): string {
  if (b.grain === 'month') return b.start.format('MMMM YYYY');
  if (b.grain === 'day') return b.start.format('dddd D MMMM YYYY');
  return `${b.start.format('D MMM')} – ${b.end.format('D MMM YYYY')}`;
}

// ── Load aggregation ─────────────────────────────────────────────────────
export type DaySample = { alloc: number; cap: number };
export type ResourceSeries = Map<string, DaySample>; // dateStr → sample

export type BucketLoad = {
  capH: number;
  allocH: number;
  pct: number; // Infinity = load on zero-capacity days
  empty: boolean;
  reduced: boolean; // capacity notably below the row's own median (closure/holiday)
};

const EMPTY_SAMPLE: DaySample = { alloc: 0, cap: 0 };

// Compute a row's per-bucket load from a sampling function. `sample(date)` is
// the resource's own series, or the per-date sum across a team's members.
export function rowLoads(
  buckets: Bucket[],
  sample: (date: string) => DaySample,
): BucketLoad[] {
  const raw = buckets.map((b) => {
    let cap = 0;
    let alloc = 0;
    for (const d of b.dates) {
      const s = sample(d);
      cap += s.cap;
      alloc += s.alloc;
    }
    const pct = cap > 0 ? (alloc / cap) * 100 : alloc > 0 ? Infinity : 0;
    return { capH: cap, allocH: alloc, pct };
  });

  const positives = raw
    .map((r) => r.capH)
    .filter((c) => c > 0)
    .sort((a, b) => a - b);
  const median = positives.length ? positives[Math.floor(positives.length / 2)]! : 0;

  return raw.map((r) => ({
    ...r,
    empty: r.capH === 0 && r.allocH === 0,
    reduced: median > 0 && r.capH > 0 && r.capH < median * 0.6,
  }));
}

export const resourceSampler =
  (series: ResourceSeries | undefined) =>
  (date: string): DaySample =>
    series?.get(date) ?? EMPTY_SAMPLE;

export const membersSampler =
  (seriesList: (ResourceSeries | undefined)[]) =>
  (date: string): DaySample => {
    let alloc = 0;
    let cap = 0;
    for (const s of seriesList) {
      const v = s?.get(date);
      if (v) {
        alloc += v.alloc;
        cap += v.cap;
      }
    }
    return { alloc, cap };
  };

export function dayjsToday(iso?: string): Dayjs {
  return iso ? dayjs(iso) : dayjs();
}
