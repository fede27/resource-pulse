import { useMemo, type RefObject } from 'react';
import { useVisibleRange } from './useVisibleRange';

// Vertical window of the board's scroll viewport, in content-space pixels.
// With realistic populations a board mounts dozens of row components while the
// viewport shows a handful: pages window their row list to this range (via
// `useWindowedRows`) so the render cost tracks the VIEWPORT, not the roster.
//
// Thin axis wrapper over `useVisibleRange` (shared mechanism with
// `useVisibleXRange`). Rows are 30–66px tall, hence the smaller default
// overscan/step than the X axis (~10–18 rows of buffer).
export type VisibleYRange = { minY: number; maxY: number };

export const UNBOUNDED_Y: VisibleYRange = { minY: 0, maxY: Number.MAX_SAFE_INTEGER };

export function useVisibleYRange(
  scrollRef: RefObject<HTMLDivElement | null>,
  overscanPx = 600,
  stepPx = 300,
): VisibleYRange {
  const range = useVisibleRange(scrollRef, 'y', overscanPx, stepPx);
  // The inner range is identity-stable; memoizing on it keeps the projected
  // object identity-stable too (memo consumers depend on this).
  return useMemo(
    () => (range.max === Number.MAX_SAFE_INTEGER ? UNBOUNDED_Y : { minY: range.min, maxY: range.max }),
    [range],
  );
}
