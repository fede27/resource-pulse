// Generic, domain-free time-axis math for the virtualized timeline grid.
//
// Buckets are addressed by a logical integer index `k` relative to an epoch:
// k = 0 is the bucket containing the epoch (typically "today"), k < 0 is the
// past, k > 0 the future. x-position is therefore deterministic, which is what
// lets the grid virtualize columns and scroll a bounded-but-large extent.
//
// No React, no network, no generated schemas — reusable by any timeline view.

import type { Dayjs } from 'dayjs';

export type Grain = 'day' | 'week' | 'month';

export type Bucket = {
  idx: number; // logical index relative to the epoch (0 = epoch's bucket)
  grain: Grain;
  start: Dayjs;
  end: Dayjs; // inclusive
  groupKey: string; // consecutive equal keys form one primary-header span
  groupLabel: string;
  label: string; // secondary header (W23 / giu / 18)
  isToday: boolean;
  dates: string[]; // YYYY-MM-DD covered (inclusive)
};

export type Group = { key: string; label: string; startIdx: number; count: number };

// Default cell width and ± scroll horizon (in buckets) per grain. The horizon is
// the bounded-but-large extent: large enough to feel endless, finite enough for
// a native scrollbar and O(visible) DOM.
export const CELL_WIDTH: Record<Grain, number> = { day: 30, week: 32, month: 64 };
export const HORIZON: Record<Grain, number> = { day: 540, week: 312, month: 120 };

const mondayOf = (d: Dayjs): Dayjs => d.subtract((d.day() + 6) % 7, 'day').startOf('day');

export function bucketStart(grain: Grain, epoch: Dayjs, k: number): Dayjs {
  switch (grain) {
    case 'day':
      return epoch.startOf('day').add(k, 'day');
    case 'month':
      return epoch.startOf('month').add(k, 'month');
    default:
      return mondayOf(epoch).add(k * 7, 'day');
  }
}

export function bucketEnd(grain: Grain, epoch: Dayjs, k: number): Dayjs {
  const s = bucketStart(grain, epoch, k);
  if (grain === 'day') return s;
  if (grain === 'month') return s.endOf('month');
  return s.add(6, 'day');
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

function buildOne(grain: Grain, epoch: Dayjs, k: number, today: Dayjs): Bucket {
  const start = bucketStart(grain, epoch, k);
  const end = bucketEnd(grain, epoch, k);

  const dates: string[] = [];
  let cur = start;
  while (!cur.isAfter(end)) {
    dates.push(cur.format('YYYY-MM-DD'));
    cur = cur.add(1, 'day');
  }

  let label: string;
  let groupKey: string;
  let groupLabel: string;
  if (grain === 'day') {
    label = start.format('D');
    groupKey = start.format('YYYY-MM');
    groupLabel = start.format('MMM YYYY');
  } else if (grain === 'month') {
    label = start.format('MMM');
    groupKey = String(start.year());
    groupLabel = groupKey;
  } else {
    label = `W${String(isoWeekNumber(start)).padStart(2, '0')}`;
    groupKey = String(start.year());
    groupLabel = groupKey;
  }

  return {
    idx: k,
    grain,
    start,
    end,
    groupKey,
    groupLabel,
    label,
    isToday: !today.isBefore(start) && !today.isAfter(end),
    dates,
  };
}

// Build the buckets for a contiguous (inclusive) index range — only ever called
// for the visible slice (+ buffer), so cost is O(visible).
export function buildBuckets(
  grain: Grain,
  epoch: Dayjs,
  kStart: number,
  kEnd: number,
  today: Dayjs,
): Bucket[] {
  const out: Bucket[] = [];
  for (let k = kStart; k <= kEnd; k += 1) out.push(buildOne(grain, epoch, k, today));
  return out;
}

export function groupRuns(buckets: Bucket[]): Group[] {
  const groups: Group[] = [];
  for (const b of buckets) {
    const last = groups[groups.length - 1];
    if (last && last.key === b.groupKey) last.count += 1;
    else groups.push({ key: b.groupKey, label: b.groupLabel, startIdx: b.idx, count: 1 });
  }
  return groups;
}

export function dateRangeForIndices(
  grain: Grain,
  epoch: Dayjs,
  kStart: number,
  kEnd: number,
): { from: Dayjs; to: Dayjs } {
  return { from: bucketStart(grain, epoch, kStart), to: bucketEnd(grain, epoch, kEnd) };
}

export function bucketTooltip(b: Bucket): string {
  if (b.grain === 'month') return b.start.format('MMMM YYYY');
  if (b.grain === 'day') return b.start.format('dddd D MMMM YYYY');
  return `${b.start.format('D MMM')} – ${b.end.format('D MMM YYYY')}`;
}
