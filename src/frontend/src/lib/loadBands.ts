// Configurable load bands → colors, shared across features (teams heatmap,
// projects board). Thresholds come from /api/config/load-bands — never
// hard-coded at call sites; the last band's lower bound IS the overload
// threshold (half-open bands, ADR-0020).

import type { LoadBandDto } from '@/api/generated/schemas';

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

// Neutral cell for a bucket with ZERO capacity: utilization is undefined there
// (0h over 0h), so it must never take a band colour — least of all overload.
// Used by the people board's "fuori calendario" state (active blocks on
// zero-capacity days): the hatch/marker is the caller's concern, the palette
// stays here with its siblings.
export const NO_CAPACITY_CELL: CellColor = {
  bg: '#fbfbfb',
  fg: 'rgba(0,0,0,.45)',
  solid: '#f0f0f0',
  empty: true,
};

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

export function bandLabelFor(pct: number, bands: LoadBand[]): string {
  return bands[bandIndexFor(pct, bands)]?.label ?? '—';
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
