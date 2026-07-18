// Shared capacity helpers for the batch read GET /api/resources/capacity
// (api-roundtrip-consolidation.md P1) — used by both boards (Persone, Progetti).

import dayjs from 'dayjs';
import type { CapacitySegmentDto } from '@/api/generated/schemas';
import { parseDurationHours } from '@/lib/duration';

const ISO = 'YYYY-MM-DD';

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
