// Load-specific model for the Team heatmap. No React, no network.
//
// Time-axis / bucket math lives in the generic `@/components/timeline`
// (timeAxis). Duration parsing and the configurable load bands → color ramp
// were promoted to `@/lib/duration` and `@/lib/loadBands` when the projects
// board became their second consumer; they are re-exported here so existing
// imports keep working. This file keeps only per-bucket load aggregation.
//
// A bucket's load% = Σ allocated / Σ capacity over its days. Summing hours (not
// averaging percentages) is what makes team / overall rows compose correctly and
// sidesteps the per-day `loadPercent` zero-capacity sentinel entirely.

import { BucketGrain } from '@/api/generated/schemas';
import type { Bucket, Grain } from '@/components/timeline';

export { parseDurationHours } from '@/lib/duration';
export {
  bandIndexFor,
  bandRangeLabel,
  bandStop,
  legendStops,
  loadColor,
  normalizeBands,
  overloadFloor,
  type CellColor,
  type LegendStop,
  type LoadBand,
} from '@/lib/loadBands';

export const grainOf = (g: BucketGrain | undefined): Grain =>
  g === BucketGrain.Day ? 'day' : g === BucketGrain.Month ? 'month' : 'week';

// ── Load aggregation ─────────────────────────────────────────────────────
export type DaySample = { alloc: number; cap: number };

export type BucketLoad = {
  capH: number;
  allocH: number;
  pct: number; // Infinity = load on zero-capacity days
  empty: boolean;
  reduced: boolean; // capacity notably below the row's own median (closure/holiday)
};

export const EMPTY_LOAD: BucketLoad = {
  capH: 0,
  allocH: 0,
  pct: 0,
  empty: true,
  reduced: false,
};

// Compute a row's per-bucket load from a sampling function. `sample(date)` is the
// resource's own series, or the per-date sum across a team's members.
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
