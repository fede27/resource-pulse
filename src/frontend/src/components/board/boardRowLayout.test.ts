import { describe, expect, it } from 'vitest';
import { windowRows, type RowItem } from './boardRowLayout';

const items: RowItem[] = [
  { key: 'a', height: 100 },
  { key: 'b', height: 50 },
  { key: 'c', height: 200 },
  { key: 'd', height: 50 },
  { key: 'e', height: 100 },
];
// Offsets: a[0,100) b[100,150) c[150,350) d[350,400) e[400,500). Total 500.

const keysOf = (r: ReturnType<typeof windowRows<RowItem>>) =>
  r.segments.map((s) => (s.kind === 'row' ? s.item.key : `gap:${s.height}`));

describe('windowRows', () => {
  it('totals the heights and emits every row with no gaps on an unbounded range', () => {
    const r = windowRows(items, 0, Number.MAX_SAFE_INTEGER);
    expect(r.totalHeight).toBe(500);
    expect(keysOf(r)).toEqual(['a', 'b', 'c', 'd', 'e']);
  });

  it('handles an empty list', () => {
    const r = windowRows([], 0, 1000);
    expect(r.totalHeight).toBe(0);
    expect(r.segments).toEqual([]);
  });

  it('renders rows straddling the window bounds and coalesces hidden runs into gaps', () => {
    // Window [120, 360]: b straddles minY, c inside, d straddles maxY.
    const r = windowRows(items, 120, 360);
    expect(keysOf(r)).toEqual(['gap:100', 'b', 'c', 'd', 'gap:100']);
    expect(r.totalHeight).toBe(500);
  });

  it('treats the bounds inclusively (a row ending exactly at minY is kept)', () => {
    // a ends at offset 100 → offset+height >= minY holds at minY=100.
    const r = windowRows(items, 100, 140);
    expect(keysOf(r)).toEqual(['a', 'b', 'gap:350']);
  });

  it('splits a gap around a pinned row outside the window', () => {
    const r = windowRows(items, 0, 120, new Set(['d']));
    expect(keysOf(r)).toEqual(['a', 'b', 'gap:200', 'd', 'gap:100']);
  });

  it('emits only gaps when the window is past every row (except pins)', () => {
    const r = windowRows(items, 10_000, 20_000);
    expect(keysOf(r)).toEqual(['gap:500']);
  });

  it('gives gap segments stable keys derived from the first hidden index', () => {
    const r = windowRows(items, 120, 360);
    const gaps = r.segments.filter((s) => s.kind === 'gap');
    expect(gaps.map((g) => g.key)).toEqual(['gap-0', 'gap-4']);
  });
});
