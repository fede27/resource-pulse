import { useEffect, useState, type RefObject } from 'react';

// Axis-generic visible window of the board's scroll viewport, in content-space
// pixels. One mechanism for both axes: `useVisibleXRange` and `useVisibleYRange`
// are thin wrappers over this hook — do not fork the logic per axis.
//
// - Quantized to `stepPx` so scrolling re-renders in coarse steps, not per
//   scroll frame; `overscanPx` keeps a buffer rendered on each side so normal
//   scrolling doesn't pop content in at the edge.
// - When the viewport is not measurable (client size 0 — jsdom, display:none,
//   pre-layout) the range is UNBOUNDED and no windowing happens: tests keep
//   exercising the full board.
export type VisibleRange = { min: number; max: number };

export const UNBOUNDED_RANGE: VisibleRange = { min: 0, max: Number.MAX_SAFE_INTEGER };

export type ScrollAxis = 'x' | 'y';

export function useVisibleRange(
  scrollRef: RefObject<HTMLDivElement | null>,
  axis: ScrollAxis,
  overscanPx: number,
  stepPx: number,
): VisibleRange {
  const [range, setRange] = useState<VisibleRange>(UNBOUNDED_RANGE);

  useEffect(() => {
    const el = scrollRef.current;
    if (!el) return;
    let raf = 0;

    const update = () => {
      raf = 0;
      const size = axis === 'x' ? el.clientWidth : el.clientHeight;
      if (size <= 0) {
        setRange((r) => (r.max === UNBOUNDED_RANGE.max ? r : UNBOUNDED_RANGE));
        return;
      }
      const pos = axis === 'x' ? el.scrollLeft : el.scrollTop;
      const min = Math.max(0, Math.floor((pos - overscanPx) / stepPx) * stepPx);
      const max = Math.ceil((pos + size + overscanPx) / stepPx) * stepPx;
      setRange((r) => (r.min === min && r.max === max ? r : { min, max }));
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
  }, [scrollRef, axis, overscanPx, stepPx]);

  return range;
}
