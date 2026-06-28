import { describe, it, expect } from 'vitest';
import dayjs from 'dayjs';
import {
  bucketStart,
  bucketEnd,
  buildBuckets,
  bucketTooltip,
  dateRangeForIndices,
  groupRuns,
} from './timeAxis';

const epoch = dayjs('2026-06-15'); // mid-month anchor

describe('bucketStart / bucketEnd', () => {
  it('addresses day buckets by integer offset from the epoch', () => {
    expect(bucketStart('day', epoch, 0).format('YYYY-MM-DD')).toBe('2026-06-15');
    expect(bucketStart('day', epoch, 3).format('YYYY-MM-DD')).toBe('2026-06-18');
    // A day bucket is a single day.
    expect(bucketEnd('day', epoch, 0).format('YYYY-MM-DD')).toBe('2026-06-15');
  });

  it('snaps month buckets to calendar-month boundaries', () => {
    expect(bucketStart('month', epoch, 0).format('YYYY-MM-DD')).toBe('2026-06-01');
    expect(bucketEnd('month', epoch, 0).format('YYYY-MM-DD')).toBe('2026-06-30');
    expect(bucketStart('month', epoch, 1).format('YYYY-MM-DD')).toBe('2026-07-01');
  });

  it('snaps week buckets to Monday and spans 7 days', () => {
    const start = bucketStart('week', epoch, 0);
    const end = bucketEnd('week', epoch, 0);
    expect(start.day()).toBe(1); // Monday
    expect(end.diff(start, 'day')).toBe(6);
  });
});

describe('buildBuckets', () => {
  it('builds one bucket per index in the inclusive range', () => {
    const buckets = buildBuckets('day', epoch, 0, 2, epoch);
    expect(buckets.map((b) => b.idx)).toEqual([0, 1, 2]);
    expect(buckets[0]!.dates).toEqual(['2026-06-15']);
  });

  it('marks the bucket containing "today" as isToday', () => {
    const buckets = buildBuckets('day', epoch, 0, 2, epoch);
    expect(buckets[0]!.isToday).toBe(true);
    expect(buckets[1]!.isToday).toBe(false);
  });

  it('enumerates every covered date for a week bucket', () => {
    const [week] = buildBuckets('week', epoch, 0, 0, epoch);
    expect(week!.dates).toHaveLength(7);
  });

  it('labels week buckets with a zero-padded ISO week number', () => {
    const [week] = buildBuckets('week', epoch, 0, 0, epoch);
    expect(week!.label).toMatch(/^W\d{2}$/);
  });
});

describe('groupRuns', () => {
  it('collapses consecutive equal group keys into spans', () => {
    // Day buckets straddling the June→July boundary form two month groups.
    const buckets = buildBuckets('day', epoch, 14, 17, epoch); // Jun 29 .. Jul 2
    const groups = groupRuns(buckets);
    expect(groups).toHaveLength(2);
    expect(groups[0]!.count + groups[1]!.count).toBe(buckets.length);
  });
});

describe('dateRangeForIndices', () => {
  it('returns the span from the first bucket start to the last bucket end', () => {
    const { from, to } = dateRangeForIndices('day', epoch, 0, 2);
    expect(from.format('YYYY-MM-DD')).toBe('2026-06-15');
    expect(to.format('YYYY-MM-DD')).toBe('2026-06-17');
  });
});

describe('bucketTooltip', () => {
  it('chooses a per-grain tooltip format', () => {
    const [day] = buildBuckets('day', epoch, 0, 0, epoch);
    const [week] = buildBuckets('week', epoch, 0, 0, epoch);
    const [month] = buildBuckets('month', epoch, 0, 0, epoch);
    expect(bucketTooltip(month!)).toBe(month!.start.format('MMMM YYYY'));
    expect(bucketTooltip(day!)).toBe(day!.start.format('dddd D MMMM YYYY'));
    expect(bucketTooltip(week!)).toContain('–'); // a start–end range
  });
});
