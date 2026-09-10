import { describe, expect, it } from 'vitest';
import { clampDomain, fetchRangeFor, MAX_DOMAIN_DAYS } from './boardDomain';

// The one bound every timed view obeys, in its two shapes. It used to be three
// declarations of 366 (here and once inside each board hook), so the edges are
// worth stating where they can only be stated once.
describe('boardDomain', () => {
  it('leaves a domain inside the cap untouched', () => {
    const d = { minISO: '2026-01-01', maxISO: '2026-03-31' };
    expect(clampDomain(d)).toBe(d);
  });

  it('clamps to exactly MAX_DOMAIN_DAYS inclusive days', () => {
    // Both endpoints count, so the last legal day is min + (MAX - 1).
    expect(clampDomain({ minISO: '2026-01-01', maxISO: '2030-01-01' })).toEqual({
      minISO: '2026-01-01',
      maxISO: '2027-01-01', // 2026 is not a leap year: 365 + 1 = 366 days
    });
    expect(MAX_DOMAIN_DAYS).toBe(366);
  });

  it('collapses an inverted domain to its first day', () => {
    expect(clampDomain({ minISO: '2026-05-10', maxISO: '2026-05-01' })).toEqual({
      minISO: '2026-05-10',
      maxISO: '2026-05-10',
    });
  });

  it('fetchRangeFor is the same bound in request shape', () => {
    expect(fetchRangeFor({ minISO: '2026-01-01', maxISO: '2026-03-31' })).toEqual({
      from: '2026-01-01',
      to: '2026-03-31',
    });
    expect(fetchRangeFor({ minISO: '2026-01-01', maxISO: '2030-01-01' })).toEqual({
      from: '2026-01-01',
      to: '2027-01-01',
    });
  });

  it('never asks the API for a backwards range', () => {
    // The copies this replaced returned {from: min, to: max} verbatim when the
    // diff was small, so an inverted domain became a request the API refuses
    // with "'from' must be on or before 'to'".
    expect(fetchRangeFor({ minISO: '2026-05-10', maxISO: '2026-05-01' })).toEqual({
      from: '2026-05-10',
      to: '2026-05-10',
    });
  });
});
