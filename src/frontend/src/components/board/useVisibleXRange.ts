import { useMemo, type RefObject } from 'react';
import { useVisibleRange } from './useVisibleRange';

// Horizontal window of the board's scroll viewport, in content-space pixels.
// At day grain a board mounts hundreds of columns per row while the viewport
// shows a few dozen: rows filter their cells/bars to this range so the render
// cost tracks the VIEWPORT, not the domain width.
//
// Thin axis wrapper over `useVisibleRange` (shared mechanism with
// `useVisibleYRange`) — quantization, overscan, rAF throttling and the
// unbounded jsdom fallback all live there.
export type VisibleXRange = { minX: number; maxX: number };

export const UNBOUNDED_X: VisibleXRange = { minX: 0, maxX: Number.MAX_SAFE_INTEGER };

export function useVisibleXRange(
  scrollRef: RefObject<HTMLDivElement | null>,
  overscanPx = 1000,
  stepPx = 400,
): VisibleXRange {
  const range = useVisibleRange(scrollRef, 'x', overscanPx, stepPx);
  // The inner range is identity-stable; memoizing on it keeps the projected
  // object identity-stable too (memo consumers depend on this).
  return useMemo(
    () => (range.max === Number.MAX_SAFE_INTEGER ? UNBOUNDED_X : { minX: range.min, maxX: range.max }),
    [range],
  );
}
