// Covering a demand — pure view-model (study «Copertura della domanda»: A as
// the base, C1 inside it, B as the open state of the person picker).
//
// The demand is asked in HOURS and a coverage is stored as a PERCENT of one
// person, so the same "50%" is 60 h of one and 80 h of another. This module
// translates between the two FOR A GIVEN PERSON over the demand's window, and
// says what the person's load becomes — before anything is created.
//
// It reads nothing: capacity and the commitment profile are passed in, from the
// same batch read-models the boards use. The creation itself goes through the
// envelope (`createByHours` / `create`), where the server does the conversion
// that counts.

import dayjs from 'dayjs';
import type { LoadSegmentDto } from '@/api/generated/schemas';

const ISO = 'YYYY-MM-DD';

// One day of one person inside the demand's window: the hours they have, and
// the rate% every block they already carry — on any project — adds up to.
export type CoverDay = { iso: string; capacity: number; other: number };

export type CoverUnit = 'hours' | 'percent';

export function coverDays(
  from: string,
  to: string,
  capacityByDay: ReadonlyMap<string, number> | undefined,
  segments: ReadonlyArray<LoadSegmentDto>,
): CoverDay[] {
  const days: CoverDay[] = [];
  const end = dayjs(to);
  for (let d = dayjs(from); !d.isAfter(end); d = d.add(1, 'day')) {
    const iso = d.format(ISO);
    const segment = segments.find((s) => (s.from ?? '') <= iso && iso <= (s.to ?? ''));
    days.push({ iso, capacity: capacityByDay?.get(iso) ?? 0, other: segment?.percent ?? 0 });
  }
  return days;
}

const round1 = (n: number) => Math.round(n * 10) / 10;
const round2 = (n: number) => Math.round(n * 100) / 100;

export const capacityHours = (days: ReadonlyArray<CoverDay>): number =>
  round1(days.reduce((sum, d) => sum + d.capacity, 0));

// Days the person is actually there: a holiday week contributes none.
export const presentDays = (days: ReadonlyArray<CoverDay>): number => days.filter((d) => d.capacity > 0).length;

// What is left of the person once everything they already carry is taken out,
// day by day. A day already over 100% frees nothing; it does not owe hours.
export const freeHours = (days: ReadonlyArray<CoverDay>): number =>
  round1(days.reduce((sum, d) => sum + (d.capacity * Math.max(0, 100 - d.other)) / 100, 0));

// Null when the person has no capacity in the window: no percentage of nothing
// yields hours, and the row cannot be created.
export function percentForHours(hours: number, days: ReadonlyArray<CoverDay>): number | null {
  const capacity = capacityHours(days);
  return capacity > 0 ? round2((hours / capacity) * 100) : null;
}

export const hoursForPercent = (percent: number, days: ReadonlyArray<CoverDay>): number =>
  round1((percent / 100) * capacityHours(days));

// ── Load per bucket, before → after ──────────────────────────────────────

export type CoverGrain = 'week' | 'month';

// Weeks while they fit on a line or two; months for anything longer.
export const grainFor = (days: ReadonlyArray<CoverDay>): CoverGrain => (days.length <= 62 ? 'week' : 'month');

export type CoverBucket = {
  key: string;
  from: string;
  to: string;
  // Capacity-weighted average rate% over the bucket. Null when the bucket has
  // no capacity at all: utilization is undefined there, not 0% and not overload.
  before: number | null;
  after: number | null;
  addedHours: number;
};

export function coverBuckets(days: ReadonlyArray<CoverDay>, percent: number, grain: CoverGrain): CoverBucket[] {
  const buckets: Array<{ key: string; days: CoverDay[] }> = [];
  for (const day of days) {
    const d = dayjs(day.iso);
    // Monday-based week: the bucket is named by the Monday it starts on.
    const key = grain === 'week' ? d.subtract((d.day() + 6) % 7, 'day').format(ISO) : d.format('YYYY-MM');
    const last = buckets[buckets.length - 1];
    if (last && last.key === key) last.days.push(day);
    else buckets.push({ key, days: [day] });
  }

  return buckets.map((b) => {
    const capacity = b.days.reduce((sum, d) => sum + d.capacity, 0);
    const before =
      capacity > 0 ? Math.round(b.days.reduce((sum, d) => sum + d.other * d.capacity, 0) / capacity) : null;
    return {
      key: b.key,
      from: b.days[0]!.iso,
      to: b.days[b.days.length - 1]!.iso,
      before,
      after: before === null ? null : Math.round(before + percent),
      addedHours: round1((percent / 100) * capacity),
    };
  });
}

// ── The rows of the dialog (one row = one coverage to create) ────────────

export type CoverRow = { key: string; resourceId: string | null; amount: number | null };

// The hours a row adds, whatever unit it was typed in.
export function rowHours(row: CoverRow, unit: CoverUnit, days: ReadonlyArray<CoverDay>): number {
  if (row.amount === null || row.amount <= 0) return 0;
  return unit === 'hours' ? row.amount : hoursForPercent(row.amount, days);
}

export function rowPercent(row: CoverRow, unit: CoverUnit, days: ReadonlyArray<CoverDay>): number | null {
  if (row.amount === null || row.amount <= 0) return null;
  return unit === 'percent' ? row.amount : percentForHours(row.amount, days);
}

// The same quantity in the other unit, for when the toggle flips mid-edit.
export function convertAmount(
  amount: number | null,
  to: CoverUnit,
  days: ReadonlyArray<CoverDay> | null,
): number | null {
  if (amount === null || !days) return null;
  return to === 'hours' ? hoursForPercent(amount, days) : percentForHours(amount, days);
}

export type DemandBalance = { required: number | null; covered: number; adding: number; left: number; over: number };

// Required / already covered / this dialog / what stays open. Going over the
// request is legal and is shown, never clamped.
export function demandBalance(required: number | null, covered: number, adding: number): DemandBalance {
  const total = covered + adding;
  return {
    required,
    covered: round1(covered),
    adding: round1(adding),
    left: required === null ? 0 : round1(Math.max(0, required - total)),
    over: required === null ? 0 : round1(Math.max(0, total - required)),
  };
}

// Decimal hours → the .NET constant TimeSpan the envelope parses
// ("[d.]hh:mm:ss"). 160 h is "6.16:00:00": "160:00:00" is refused.
export function hoursToTimeSpan(hours: number): string {
  const totalMinutes = Math.max(0, Math.round(hours * 60));
  const days = Math.floor(totalMinutes / 1440);
  const pad = (n: number) => String(n).padStart(2, '0');
  const rest = `${pad(Math.floor((totalMinutes % 1440) / 60))}:${pad(totalMinutes % 60)}:00`;
  return days > 0 ? `${days}.${rest}` : rest;
}
