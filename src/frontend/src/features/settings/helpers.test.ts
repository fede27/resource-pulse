import { describe, it, expect } from 'vitest';
import dayjs from 'dayjs';
import type { TFunction } from 'i18next';
import { BucketGrain, DurationUnit } from '@/api/generated/schemas';
import {
  addDuration,
  bandColor,
  durationLabel,
  durationToDays,
  durationUnitKey,
  grainKey,
  horizonFitsTheReadCap,
  longestProjectedDays,
  MAX_HORIZON_DAYS,
  today,
} from './helpers';

// Identity TFunction stub: returns the key so we can assert on it.
const t = ((key: string) => key) as unknown as TFunction;

describe('durationToDays', () => {
  it('converts each unit to its approximate-day basis', () => {
    expect(durationToDays({ value: 3, unit: DurationUnit.Days })).toBe(3);
    expect(durationToDays({ value: 2, unit: DurationUnit.Weeks })).toBe(14);
    expect(durationToDays({ value: 1, unit: DurationUnit.Months })).toBe(30);
  });
});

describe('horizonFitsTheReadCap', () => {
  // Mirrors TimeFenceConfiguration.MaxHorizonDays: past it the detector cannot
  // read the horizon it polices and the triage queue empties without saying so.
  it('accepts the longest horizons that always fit', () => {
    expect(horizonFitsTheReadCap({ value: 365, unit: DurationUnit.Days })).toBe(true);
    expect(horizonFitsTheReadCap({ value: 52, unit: DurationUnit.Weeks })).toBe(true);
    expect(horizonFitsTheReadCap({ value: 11, unit: DurationUnit.Months })).toBe(true);
  });

  it('rejects twelve months, which the 30-day basis would wave through', () => {
    // 12 × 30 = 360 by durationToDays, but a leap crossing runs to 367 inclusive.
    expect(durationToDays({ value: 12, unit: DurationUnit.Months })).toBeLessThan(MAX_HORIZON_DAYS);
    expect(longestProjectedDays({ value: 12, unit: DurationUnit.Months })).toBe(372);
    expect(horizonFitsTheReadCap({ value: 12, unit: DurationUnit.Months })).toBe(false);
  });

  it('rejects the horizon that silently disabled the detectors', () => {
    expect(horizonFitsTheReadCap({ value: 18, unit: DurationUnit.Months })).toBe(false);
  });
});

describe('durationUnitKey', () => {
  it('maps the unit enum to an i18n key fragment', () => {
    expect(durationUnitKey(DurationUnit.Days)).toBe('days');
    expect(durationUnitKey(DurationUnit.Weeks)).toBe('weeks');
    expect(durationUnitKey(DurationUnit.Months)).toBe('months');
  });
});

describe('durationLabel', () => {
  it('composes value + localized unit', () => {
    expect(durationLabel({ value: 2, unit: DurationUnit.Weeks }, t)).toBe(
      '2 settings.fence.units.weeks',
    );
  });
});

describe('addDuration', () => {
  const from = dayjs('2026-01-01');
  it('projects the rolling horizon onto a concrete date per unit', () => {
    expect(addDuration(from, { value: 5, unit: DurationUnit.Days }).format('YYYY-MM-DD')).toBe(
      '2026-01-06',
    );
    expect(addDuration(from, { value: 2, unit: DurationUnit.Weeks }).format('YYYY-MM-DD')).toBe(
      '2026-01-15',
    );
    expect(
      addDuration(from, { value: 1, unit: DurationUnit.Months }).format('YYYY-MM-DD'),
    ).toBe('2026-02-01');
  });
});

describe('bandColor', () => {
  it('returns the mid (blue) stop when there is a single band', () => {
    expect(bandColor(0, 1)).toBe('#1677ff');
  });

  it('ramps the first band neutral and the last band red', () => {
    expect(bandColor(0, 5)).toBe('#8c8c8c');
    expect(bandColor(4, 5)).toBe('#ff4d4f');
  });
});

describe('grainKey', () => {
  it('maps the grain enum to a key fragment', () => {
    expect(grainKey(BucketGrain.Day)).toBe('day');
    expect(grainKey(BucketGrain.Week)).toBe('week');
    expect(grainKey(BucketGrain.Month)).toBe('month');
  });
});

describe('today', () => {
  it('returns the current calendar day', () => {
    expect(today().isSame(dayjs(), 'day')).toBe(true);
  });
});
