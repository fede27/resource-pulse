import { useEffect, useState, type RefObject } from 'react';

// Horizontal window of the board's scroll viewport, in content-space pixels.
// At day grain a board mounts hundreds of columns per row while the viewport
// shows a few dozen: rows filter their cells/bars to this range so the render
// cost tracks the VIEWPORT, not the domain width.
//
// - Quantized to `stepPx` so scrolling re-renders in coarse steps, not per
//   scroll frame; `overscanPx` keeps a buffer rendered on each side so normal
//   scrolling doesn't pop cells in at the edge.
// - When the viewport is not measurable (clientWidth 0 — jsdom, display:none,
//   pre-layout) the range is UNBOUNDED and no windowing happens: tests keep
//   exercising the full board.
export type VisibleXRange = { minX: number; maxX: number };

export const UNBOUNDED_X: VisibleXRange = { minX: 0, maxX: Number.MAX_SAFE_INTEGER };

export function useVisibleXRange(
  scrollRef: RefObject<HTMLDivElement | null>,
  overscanPx = 1000,
  stepPx = 400,
): VisibleXRange {
  const [range, setRange] = useState<VisibleXRange>(UNBOUNDED_X);

  useEffect(() => {
    const el = scrollRef.current;
    if (!el) return;
    let raf = 0;

    const update = () => {
      raf = 0;
      const width = el.clientWidth;
      if (width <= 0) {
        setRange((r) => (r.maxX === UNBOUNDED_X.maxX ? r : UNBOUNDED_X));
        return;
      }
      const minX = Math.max(0, Math.floor((el.scrollLeft - overscanPx) / stepPx) * stepPx);
      const maxX = Math.ceil((el.scrollLeft + width + overscanPx) / stepPx) * stepPx;
      setRange((r) => (r.minX === minX && r.maxX === maxX ? r : { minX, maxX }));
    };

    const schedule = () => {
      if (!raf) raf = requestAnimationFrame(update);
    };

    update();
    el.addEventListener('scroll', schedule, { passive: true });
    const ro = new ResizeObserver(schedule);
    ro.observe(el);
    return () => {
      el.removeEventListener('scroll', schedule);
      ro.disconnect();
      if (raf) cancelAnimationFrame(raf);
    };
  }, [scrollRef, overscanPx, stepPx]);

  return range;
}
