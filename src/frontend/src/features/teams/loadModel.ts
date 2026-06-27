// Load-specific model for the Team heatmap. No React, no network.
//
// Time-axis / bucket math now lives in the generic `@/components/timeline`
// (timeAxis). This file keeps only what's load-specific: hours parsing, the
// configurable load bands → color ramp, and per-bucket aggregation.
//
// A bucket's load% = Σ allocated / Σ capacity over its days. Summing hours (not
// averaging percentages) is what makes team / overall rows compose correctly and
// sidesteps the per-day `loadPercent` zero-capacity sentinel entirely.
//
// Thresholds (bands) and the cell grain come from the org configuration
// (/api/config/load-bands and /api/config/bucketing) — never hard-coded here.

import { BucketGrain, type LoadBandDto } from '@/api/generated/schemas';
import type { Bucket, Grain } from '@/components/timeline';

// ── Duration parsing ─────────────────────────────────────────────────────
// This backend serializes TimeSpan in the constant format ("09:00:00"); we also
// accept ISO-8601 ("PT8H") defensively.
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

// The last band is the open-ended overload band; its lower bound is the
// overload threshold. Kept config-driven — never hard-code 100 at call sites.
export function overloadFloor(bands: LoadBand[]): number {
  return bands.length ? bands[bands.length - 1]!.lowerBound : 100;
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
