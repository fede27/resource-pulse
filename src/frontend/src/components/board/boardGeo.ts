// Board timeline — scrollable pixel geometry shared by the Progetti and
// Persone boards. Pure: no React, no network.
//
// A board is a bounded-domain continuous gantt (absolute bars over a shared
// axis), configurable as domain [minISO, maxISO] × bucket (day/week/month).
// This is a different rendering model from the virtualized bucket-cell grid in
// `@/components/timeline` (heatmap cells), hence its own geometry.
//
// Time-fence boundaries are rolling-from-today (never stored as dates,
// ADR-0020); the caller computes them from /api/config/time-fence and passes
// the resolved ISO dates in.

import dayjs, { type Dayjs } from 'dayjs';
import type { Grain } from '@/components/timeline';

const ISO = 'YYYY-MM-DD';

// pixels-per-day per bucket (bucket "width" ≈ dayPx × days-in-bucket)
export const BUCKET_DAYPX: Record<Grain, number> = { day: 34, week: 13.4, month: 4.3 };

export type MajorBand = { x: number; w: number; label: string };

export type UnitTick = {
  x: number;
  w: number;
  /** Start date of the tick's span (unclamped — the first week tick may begin before the domain). */
  iso: string;
  label: string;
  isWeekend: boolean;
  isMonday: boolean;
  isMonthStart: boolean;
};

export type BoardGeo = {
  minISO: string;
  maxISO: string;
  bucket: Grain;
  dayPx: number;
  contentW: number;
  totalDays: number;
  /** Clamped x position (px) of an ISO date within the domain. */
  xPx: (iso: string) => number;
  /** Width (px) of [fromISO, toExclusiveISO). Min 3px so slivers stay visible. */
  wPx: (fromISO: string, toExclusiveISO: string) => number;
  /** Width (px) of an inclusive date span [fromISO, toInclusiveISO]. */
  wPxInclusive: (fromISO: string, toInclusiveISO: string) => number;
  inDomain: (iso: string) => boolean;
  unitTicks: UnitTick[];
  majorBands: MajorBand[];
  todayX: number;
  todayIn: boolean;
  frozenX: number;
  slushyX: number;
};

// ISO-8601 week number (weeks start Monday; week 1 contains the first Thursday).
export function isoWeek(d: Dayjs): number {
  const target = d.startOf('day');
  const dayNr = (target.day() + 6) % 7; // Mon=0 … Sun=6
  const thursday = target.add(3 - dayNr, 'day');
  const firstThursday = (() => {
    const jan4 = dayjs(new Date(thursday.year(), 0, 4));
    const jan4Nr = (jan4.day() + 6) % 7;
    return jan4.add(3 - jan4Nr, 'day');
  })();
  return 1 + Math.round(thursday.diff(firstThursday, 'day') / 7);
}

export function mondayOf(iso: string): Dayjs {
  const d = dayjs(iso);
  return d.subtract((d.day() + 6) % 7, 'day');
}

export type FenceBoundaries = {
  todayISO: string;
  frozenEndISO: string;
  slushyEndISO: string;
};

export function buildGeo(
  minISO: string,
  maxISO: string,
  bucket: Grain,
  fence: FenceBoundaries,
): BoardGeo {
  const A = dayjs(minISO).startOf('day');
  const B = dayjs(maxISO).startOf('day');
  const dayPx = BUCKET_DAYPX[bucket];
  const totalDays = Math.max(1, B.diff(A, 'day'));
  const contentW = Math.max(360, Math.round(totalDays * dayPx));

  const rawX = (iso: string) => dayjs(iso).diff(A, 'day', true) * dayPx;
  const xPx = (iso: string) => Math.max(0, Math.min(contentW, rawX(iso)));
  const wPx = (f: string, t: string) => Math.max(3, xPx(t) - xPx(f));
  const wPxInclusive = (f: string, t: string) => wPx(f, dayjs(t).add(1, 'day').format(ISO));
  const inDomain = (iso: string) => {
    const d = dayjs(iso);
    return !d.isBefore(A) && !d.isAfter(B);
  };

  // Month bands (major axis row for day/week buckets)
  const monthBands = (): MajorBand[] => {
    const out: MajorBand[] = [];
    let d = A.startOf('month');
    while (!d.isAfter(B)) {
      const next = d.add(1, 'month');
      const segStart = d.isBefore(A) ? A : d;
      const segEnd = next.isAfter(B) ? B : next;
      const x = xPx(segStart.format(ISO));
      const w = xPx(segEnd.format(ISO)) - x;
      const showYear = d.month() === 0 || out.length === 0;
      out.push({ x, w, label: showYear ? d.format('MMM YYYY') : d.format('MMM') });
      d = next;
    }
    return out;
  };

  // Year bands (major axis row for the month bucket)
  const yearBands = (): MajorBand[] => {
    const out: MajorBand[] = [];
    let y = A.year();
    while (y <= B.year()) {
      const ys = dayjs(new Date(y, 0, 1));
      const ye = ys.add(1, 'year');
      const segStart = ys.isBefore(A) ? A : ys;
      const segEnd = ye.isAfter(B) ? B : ye;
      const x = xPx(segStart.format(ISO));
      out.push({ x, w: xPx(segEnd.format(ISO)) - x, label: String(y) });
      y += 1;
    }
    return out;
  };

  const weekTicks = (): UnitTick[] => {
    const out: UnitTick[] = [];
    let d = mondayOf(minISO);
    while (!d.isAfter(B)) {
      const next = d.add(7, 'day');
      const x = xPx(d.format(ISO));
      const w = xPx(next.format(ISO)) - x;
      // Include weeks that merely overlap the domain (the first one is clipped
      // at x=0) so the axis has no blank leading stretch.
      if (next.isAfter(A)) {
        out.push({
          x,
          w,
          iso: d.format(ISO),
          label: String(isoWeek(d)),
          isWeekend: false,
          isMonday: true,
          isMonthStart: d.date() <= 7,
        });
      }
      d = next;
    }
    return out;
  };

  const dayTicks = (): UnitTick[] => {
    const out: UnitTick[] = [];
    let d = A;
    while (!d.isAfter(B)) {
      const next = d.add(1, 'day');
      const x = xPx(d.format(ISO));
      const wd = (d.day() + 6) % 7;
      out.push({
        x,
        w: xPx(next.format(ISO)) - x,
        iso: d.format(ISO),
        label: String(d.date()),
        isWeekend: wd >= 5,
        isMonday: wd === 0,
        isMonthStart: d.date() === 1,
      });
      d = next;
    }
    return out;
  };

  const monthTicks = (): UnitTick[] => {
    const out: UnitTick[] = [];
    let d = A.startOf('month');
    while (!d.isAfter(B)) {
      const next = d.add(1, 'month');
      const segStart = d.isBefore(A) ? A : d;
      const segEnd = next.isAfter(B) ? B : next;
      const x = xPx(segStart.format(ISO));
      const showYear = d.month() === 0 || out.length === 0;
      out.push({
        x,
        w: xPx(segEnd.format(ISO)) - x,
        iso: d.format(ISO),
        label: showYear ? d.format('MMM YYYY') : d.format('MMM'),
        isWeekend: false,
        isMonday: false,
        isMonthStart: out.length === 0,
      });
      d = next;
    }
    return out;
  };

  const unitTicks = bucket === 'day' ? dayTicks() : bucket === 'month' ? monthTicks() : weekTicks();
  const majorBands = bucket === 'month' ? yearBands() : monthBands();

  return {
    minISO,
    maxISO,
    bucket,
    dayPx,
    contentW,
    totalDays,
    xPx,
    wPx,
    wPxInclusive,
    inDomain,
    unitTicks,
    majorBands,
    todayX: xPx(fence.todayISO),
    todayIn: inDomain(fence.todayISO),
    frozenX: xPx(fence.frozenEndISO),
    slushyX: xPx(fence.slushyEndISO),
  };
}

// ── Rolling fence boundaries from the org configuration ──────────────────
// DurationUnit is integer on the wire: Days:1, Weeks:2, Months:3.
export function fenceEnd(
  todayISO: string,
  value: number | undefined,
  unit: number | undefined,
): string {
  const t = dayjs(todayISO);
  const v = value ?? 0;
  const d = unit === 3 ? t.add(v, 'month') : unit === 2 ? t.add(v * 7, 'day') : t.add(v, 'day');
  return d.format(ISO);
}
