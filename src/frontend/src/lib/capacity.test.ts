import { describe, expect, it } from 'vitest';
import dayjs from 'dayjs';
import { buildBuckets } from '@/features/roles-teams/availabilityModel';
import { capacityMapFromSegments, splitRange, MAX_CAPACITY_RANGE_DAYS } from './capacity';

const days = (from: string, to: string) => dayjs(to).diff(dayjs(from), 'day') + 1;

describe('splitRange', () => {
  it('leaves a range within the cap as a single chunk', () => {
    expect(splitRange('2026-01-01', '2026-12-31')).toEqual([
      { from: '2026-01-01', to: '2026-12-31' },
    ]);
  });

  it('splits into contiguous, disjoint chunks that cover the whole range', () => {
    const chunks = splitRange('2026-01-01', '2026-01-10', 4);
    expect(chunks).toEqual([
      { from: '2026-01-01', to: '2026-01-04' },
      { from: '2026-01-05', to: '2026-01-08' },
      { from: '2026-01-09', to: '2026-01-10' },
    ]);
  });

  it('returns nothing for an empty or inverted range', () => {
    expect(splitRange('2026-01-10', '2026-01-01')).toEqual([]);
  });

  // The bug this exists for: "tutto il 2026" at WEEK grain. The domain is 365
  // days (within the cap) but the aligned bucket span — Monday before Jan 1 to
  // Sunday after Dec 31 — is 371, and the capacity read 400s on it. A 400 reads
  // as "everyone has zero hours", so the grid looked empty at week grain only
  // (day and month grain don't expand a whole-year domain).
  it('covers the aligned bucket span of a full year at week grain', () => {
    const buckets = buildBuckets('2026-01-01', '2026-12-31', 'week');
    const from = buckets[0]!.from;
    const to = buckets[buckets.length - 1]!.to;
    expect(days(from, to)).toBeGreaterThan(MAX_CAPACITY_RANGE_DAYS);

    const chunks = splitRange(from, to);
    expect(chunks).toHaveLength(2);
    expect(chunks[0]!.from).toBe(from);
    expect(chunks[chunks.length - 1]!.to).toBe(to);
    for (const c of chunks) expect(days(c.from, c.to)).toBeLessThanOrEqual(MAX_CAPACITY_RANGE_DAYS);
    // Contiguous: the second chunk starts the day after the first ends.
    expect(chunks[1]!.from).toBe(dayjs(chunks[0]!.to).add(1, 'day').format('YYYY-MM-DD'));
  });

  it('two chunks always suffice for a capped domain at month grain', () => {
    // Widest case: a domain at the cap whose month alignment reaches out on both
    // sides. Still nowhere near needing a third chunk.
    const buckets = buildBuckets('2026-01-15', '2027-01-14', 'month');
    const chunks = splitRange(buckets[0]!.from, buckets[buckets.length - 1]!.to);
    expect(chunks.length).toBeLessThanOrEqual(2);
  });
});

describe('capacityMapFromSegments', () => {
  it('expands run-length segments and leaves gaps absent', () => {
    const map = capacityMapFromSegments([
      { from: '2026-07-06', to: '2026-07-08', hoursPerDay: '08:00:00' },
      { from: '2026-07-13', to: '2026-07-13', hoursPerDay: '04:00:00' },
    ]);
    expect(map.get('2026-07-06')).toBe(8);
    expect(map.get('2026-07-08')).toBe(8);
    expect(map.get('2026-07-13')).toBe(4);
    // A gap is the zero — weekends and closures arrive as missing keys.
    expect(map.has('2026-07-11')).toBe(false);
  });
});
