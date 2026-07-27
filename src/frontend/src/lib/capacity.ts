// Shared capacity helpers for the batch read GET /api/resources/capacity
// (api-roundtrip-consolidation.md P1) — used by both boards (Persone, Progetti).

import dayjs from 'dayjs';
import type { CapacitySegmentDto } from '@/api/generated/schemas';
import { parseDurationHours } from '@/lib/duration';

const ISO = 'YYYY-MM-DD';

// The batch capacity read rejects a range wider than this (LiveCapacityQueryService
// .MaxRangeDays) with a 400 — and a rejected read reads as "everybody has zero
// hours", not as an error, which is the nastiest way for this to fail.
export const MAX_CAPACITY_RANGE_DAYS = 366;

// Splits an inclusive range into contiguous chunks no wider than `maxDays`.
//
// A view's DOMAIN is clamped to the cap, but the range it actually reads is the
// span of its ALIGNED buckets, which can reach past the domain on both ends (a
// week bucket starts on Monday, a month bucket on the 1st). "Tutto il 2026" at
// week grain is 365 domain days but a 371-day read. Splitting keeps the axis the
// user asked for instead of truncating it or silently under-reporting its edges.
export function splitRange(
  fromISO: string,
  toISO: string,
  maxDays = MAX_CAPACITY_RANGE_DAYS,
): Array<{ from: string; to: string }> {
  if (!fromISO || !toISO || fromISO > toISO) return [];
  const out: Array<{ from: string; to: string }> = [];
  const end = dayjs(toISO);
  let cursor = dayjs(fromISO);
  while (!cursor.isAfter(end)) {
    const chunkEnd = cursor.add(maxDays - 1, 'day');
    const to = chunkEnd.isAfter(end) ? end : chunkEnd;
    out.push({ from: cursor.format(ISO), to: to.format(ISO) });
    cursor = to.add(1, 'day');
  }
  return out;
}

// Expands the run-length capacity segments into the per-day map the view models
// work with. Days outside every segment have zero capacity — weekends and
// closures arrive as gaps, so a missing key IS the zero, mirroring the wire
// contract.
export function capacityMapFromSegments(segments: CapacitySegmentDto[]): Map<string, number> {
  const map = new Map<string, number>();
  for (const s of segments) {
    if (!s.from || !s.to) continue;
    const hours = parseDurationHours(s.hoursPerDay);
    const end = dayjs(s.to);
    for (let d = dayjs(s.from); !d.isAfter(end); d = d.add(1, 'day')) {
      map.set(d.format(ISO), hours);
    }
  }
  return map;
}

// Hours a coverage block resolves to over [from, to] ∩ the days present in
// capacityByDay: Σ percent × daily capacity (ADR-0026). RANGE-SCOPED by
// construction — the map only covers the fetched window, so a block reaching
// beyond it counts its in-range hours only, consistent with the range-scoped
// coverage reconciliation shown alongside.
export function blockHoursInRange(
  from: string,
  to: string,
  percent: number,
  capacityByDay: ReadonlyMap<string, number>,
): number {
  if (!from || !to || from > to) return 0;
  let capH = 0;
  const end = dayjs(to);
  for (let d = dayjs(from); !d.isAfter(end); d = d.add(1, 'day')) {
    capH += capacityByDay.get(d.format(ISO)) ?? 0;
  }
  return (percent / 100) * capH;
}
