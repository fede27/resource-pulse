import dayjs from 'dayjs';
import { describe, expect, it } from 'vitest';
import { BUCKET_DAYPX, buildGeo, fenceEnd, isoWeek, mondayOf } from './timelineGeo';

const FENCE = {
  todayISO: '2026-07-05',
  frozenEndISO: '2026-07-19',
  slushyEndISO: '2026-09-05',
};

describe('buildGeo', () => {
  it('sizes the content from days × bucket density', () => {
    const geo = buildGeo('2026-07-01', '2026-07-31', 'day', FENCE);
    expect(geo.totalDays).toBe(30);
    expect(geo.contentW).toBe(Math.round(30 * BUCKET_DAYPX.day));
  });

  it('clamps x positions to the domain', () => {
    const geo = buildGeo('2026-07-01', '2026-07-31', 'week', FENCE);
    expect(geo.xPx('2026-06-01')).toBe(0);
    expect(geo.xPx('2026-12-01')).toBe(geo.contentW);
    expect(geo.xPx('2026-07-01')).toBe(0);
  });

  it('renders inclusive spans one day wider than exclusive ones', () => {
    const geo = buildGeo('2026-07-01', '2026-07-31', 'day', FENCE);
    const exclusive = geo.wPx('2026-07-06', '2026-07-10');
    const inclusive = geo.wPxInclusive('2026-07-06', '2026-07-10');
    expect(inclusive - exclusive).toBeCloseTo(BUCKET_DAYPX.day, 5);
  });

  it('places today and the fence boundaries', () => {
    const geo = buildGeo('2026-07-01', '2026-07-31', 'week', FENCE);
    expect(geo.todayIn).toBe(true);
    expect(geo.todayX).toBeCloseTo(4 * BUCKET_DAYPX.week, 5);
    expect(geo.frozenX).toBeGreaterThan(geo.todayX);
    expect(geo.slushyX).toBe(geo.contentW); // beyond the domain → clamped
  });

  it('emits day ticks with weekend metadata', () => {
    const geo = buildGeo('2026-07-06', '2026-07-12', 'day', FENCE); // Mon..Sun
    expect(geo.unitTicks).toHaveLength(7);
    expect(geo.unitTicks[0]!.isMonday).toBe(true);
    expect(geo.unitTicks[5]!.isWeekend).toBe(true);
    expect(geo.unitTicks[6]!.isWeekend).toBe(true);
  });

  it('emits week ticks labelled with ISO week numbers', () => {
    const geo = buildGeo('2026-07-01', '2026-07-31', 'week', FENCE);
    expect(geo.unitTicks.length).toBeGreaterThanOrEqual(5);
    expect(geo.unitTicks.map((t) => t.label)).toContain(String(isoWeek(dayjs('2026-07-06'))));
  });

  it('uses year bands as the major axis in month bucket', () => {
    const geo = buildGeo('2025-11-01', '2026-02-28', 'month', FENCE);
    expect(geo.majorBands.map((b) => b.label)).toEqual(['2025', '2026']);
    expect(geo.unitTicks).toHaveLength(4); // nov, dic, gen, feb
  });
});

describe('helpers', () => {
  it('isoWeek follows the first-Thursday rule', () => {
    expect(isoWeek(dayjs('2026-01-01'))).toBe(1); // Thu 2026-01-01
    expect(isoWeek(dayjs('2025-12-29'))).toBe(1); // Monday of week 1/2026
    expect(isoWeek(dayjs('2026-07-06'))).toBe(28);
  });

  it('mondayOf returns the Monday on or before the date', () => {
    expect(mondayOf('2026-07-05').format('YYYY-MM-DD')).toBe('2026-06-29'); // Sunday → prev Monday
    expect(mondayOf('2026-07-06').format('YYYY-MM-DD')).toBe('2026-07-06'); // Monday → itself
  });

  it('fenceEnd rolls forward by the configured duration unit', () => {
    expect(fenceEnd('2026-07-05', 2, 1)).toBe('2026-07-07'); // days
    expect(fenceEnd('2026-07-05', 2, 2)).toBe('2026-07-19'); // weeks
    expect(fenceEnd('2026-07-05', 2, 3)).toBe('2026-09-05'); // months
    expect(fenceEnd('2026-07-05', undefined, undefined)).toBe('2026-07-05');
  });
});
