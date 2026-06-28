import { describe, it, expect } from 'vitest';
import { BucketGrain, type LoadBandDto } from '@/api/generated/schemas';
import type { Bucket } from '@/components/timeline';
import {
  bandIndexFor,
  bandRangeLabel,
  bandStop,
  grainOf,
  legendStops,
  loadColor,
  normalizeBands,
  overloadFloor,
  parseDurationHours,
  rowLoads,
  type DaySample,
  type LoadBand,
} from './loadModel';

// --- Canonical pure-logic test pattern -------------------------------------
// No providers, no network, no React: import the unit, assert behaviour.
// Each `it` makes a meaningful assertion about an observable output.

// A Bucket carries many fields; rowLoads only reads `dates`. Build a minimal one.
const bucketOf = (dates: string[]): Bucket => ({ dates }) as unknown as Bucket;

const BANDS: LoadBand[] = [
  { label: 'Under', lowerBound: 0 },
  { label: 'Healthy', lowerBound: 85 },
  { label: 'Full', lowerBound: 100 },
  { label: 'Overloaded', lowerBound: 110 },
];

describe('parseDurationHours', () => {
  it('parses the backend constant TimeSpan format HH:mm:ss', () => {
    expect(parseDurationHours('09:00:00')).toBe(9);
    expect(parseDurationHours('08:30:00')).toBe(8.5);
  });

  it('parses the "d.HH:mm:ss" form with a day component', () => {
    expect(parseDurationHours('1.12:00:00')).toBe(36);
  });

  it('parses ISO-8601 durations defensively', () => {
    expect(parseDurationHours('PT8H')).toBe(8);
    expect(parseDurationHours('PT30M')).toBe(0.5);
    expect(parseDurationHours('P1DT1H')).toBe(25);
  });

  it('honours a leading minus sign', () => {
    expect(parseDurationHours('-02:00:00')).toBe(-2);
    expect(parseDurationHours('-PT2H')).toBe(-2);
  });

  it('returns 0 for empty, null, or unparseable input', () => {
    expect(parseDurationHours(null)).toBe(0);
    expect(parseDurationHours(undefined)).toBe(0);
    expect(parseDurationHours('')).toBe(0);
    expect(parseDurationHours('not-a-duration')).toBe(0);
  });
});

describe('normalizeBands', () => {
  it('sorts bands ascending by lowerBound', () => {
    const input: LoadBandDto[] = [
      { label: 'b', lowerBound: 100 },
      { label: 'a', lowerBound: 0 },
    ];
    expect(normalizeBands(input).map((b) => b.label)).toEqual(['a', 'b']);
  });

  it('defaults missing label/lowerBound', () => {
    // label null + lowerBound omitted exercises both `?? '—'` and `?? 0`.
    expect(normalizeBands([{ label: null }])).toEqual([{ label: '—', lowerBound: 0 }]);
  });

  it('falls back to a single placeholder band when empty or nullish', () => {
    expect(normalizeBands([])).toEqual([{ label: '—', lowerBound: 0 }]);
    expect(normalizeBands(null)).toEqual([{ label: '—', lowerBound: 0 }]);
  });
});

describe('bandIndexFor', () => {
  it('selects the highest band whose lowerBound the pct meets', () => {
    expect(bandIndexFor(0, BANDS)).toBe(0);
    expect(bandIndexFor(90, BANDS)).toBe(1);
    expect(bandIndexFor(100, BANDS)).toBe(2);
    expect(bandIndexFor(500, BANDS)).toBe(3);
  });
});

describe('overloadFloor', () => {
  it('returns the last band lowerBound (the overload threshold)', () => {
    expect(overloadFloor(BANDS)).toBe(110);
  });

  it('defaults to 100 when there are no bands', () => {
    expect(overloadFloor([])).toBe(100);
  });
});

describe('bandStop', () => {
  it('maps a single band to the overload (red) stop', () => {
    expect(bandStop(0, 1).solid).toBe('#ff4d4f');
  });

  it('maps the first band to neutral and the last to red', () => {
    expect(bandStop(0, 4).solid).toBe('#bfbfbf');
    expect(bandStop(3, 4).solid).toBe('#ff4d4f');
  });

  it('interpolates middle bands across the green→amber ramp', () => {
    // 4 bands → middles at index 1 and 2 land on distinct stops.
    expect(bandStop(1, 4).solid).not.toBe(bandStop(2, 4).solid);
  });
});

describe('loadColor', () => {
  it('renders an empty cell for ~zero load', () => {
    expect(loadColor(0, BANDS).empty).toBe(true);
    expect(loadColor(0.4, BANDS).empty).toBe(true);
  });

  it('renders the overload (red) colour for a non-finite pct sentinel', () => {
    const c = loadColor(Infinity, BANDS);
    expect(c.empty).toBe(false);
    expect(c.solid).toBe('#ff4d4f');
  });

  it('renders a non-empty banded colour for real load', () => {
    const c = loadColor(95, BANDS);
    expect(c.empty).toBe(false);
    expect(c.solid).toBeTruthy();
  });
});

describe('bandRangeLabel & legendStops', () => {
  it('formats a closed range for middle bands and open-ended for the last', () => {
    expect(bandRangeLabel(BANDS, 0)).toBe('0–85%');
    expect(bandRangeLabel(BANDS, 3)).toBe('110%+');
  });

  it('builds one legend stop per band with range + colour', () => {
    const stops = legendStops(BANDS);
    expect(stops).toHaveLength(BANDS.length);
    expect(stops[0]).toMatchObject({ label: 'Under', range: '0–85%' });
    expect(stops[0]!.solid).toBeTruthy();
  });
});

describe('grainOf', () => {
  it('maps backend grain enum to the timeline grain string', () => {
    expect(grainOf(BucketGrain.Day)).toBe('day');
    expect(grainOf(BucketGrain.Month)).toBe('month');
    expect(grainOf(BucketGrain.Week)).toBe('week');
    expect(grainOf(undefined)).toBe('week');
  });
});

describe('rowLoads', () => {
  const sampleFrom =
    (table: Record<string, DaySample>) =>
    (date: string): DaySample =>
      table[date] ?? { alloc: 0, cap: 0 };

  it('computes load% as Σalloc / Σcap over a bucket', () => {
    const buckets = [bucketOf(['2026-01-01', '2026-01-02'])];
    const [load] = rowLoads(
      buckets,
      sampleFrom({
        '2026-01-01': { alloc: 4, cap: 8 },
        '2026-01-02': { alloc: 4, cap: 8 },
      }),
    );
    expect(load!.capH).toBe(16);
    expect(load!.allocH).toBe(8);
    expect(load!.pct).toBe(50);
    expect(load!.empty).toBe(false);
  });

  it('flags an empty bucket when there is neither capacity nor allocation', () => {
    const [load] = rowLoads([bucketOf(['2026-01-01'])], () => ({ alloc: 0, cap: 0 }));
    expect(load!.empty).toBe(true);
    expect(load!.pct).toBe(0);
  });

  it('yields Infinity when there is allocation on zero-capacity days', () => {
    const [load] = rowLoads(
      [bucketOf(['2026-01-01'])],
      sampleFrom({ '2026-01-01': { alloc: 5, cap: 0 } }),
    );
    expect(load!.pct).toBe(Infinity);
    expect(load!.empty).toBe(false);
  });

  it('marks a bucket "reduced" when its capacity is well below the row median', () => {
    const buckets = [
      bucketOf(['d1']),
      bucketOf(['d2']),
      bucketOf(['d3']),
      bucketOf(['d4']), // the reduced one
    ];
    const [, , , reduced] = rowLoads(
      buckets,
      sampleFrom({
        d1: { alloc: 0, cap: 40 },
        d2: { alloc: 0, cap: 40 },
        d3: { alloc: 0, cap: 40 },
        d4: { alloc: 0, cap: 8 }, // < 60% of the 40h median
      }),
    );
    expect(reduced!.reduced).toBe(true);
  });
});
