import { useEffect, useState, type RefObject } from 'react';

const MIN_HEIGHT = 240;

// Bounds the board frame to the space left below it in the viewport, so the
// board scrolls INSIDE a viewport-sized frame instead of growing the document:
// the horizontal scrollbar stays reachable at the frame bottom and the sticky
// timeline header has a scrollport to stick to.
//
// Re-measures on window resize and on body size changes (toolbar chips
// wrapping, KPI reflow shift the frame top). Deliberately NOT on document
// scroll: content above the board is short and re-measuring on scroll would
// jitter the height. Unmeasurable layout (jsdom) → null → no constraint; the
// Y-windowing hook independently falls back to unbounded there, so correctness
// never depends on this hook.
export function useFrameMaxHeight(
  frameRef: RefObject<HTMLDivElement | null>,
  bottomGapPx = 16,
): number | null {
  const [maxHeight, setMaxHeight] = useState<number | null>(null);

  useEffect(() => {
    const el = frameRef.current;
    if (!el) return;
    let raf = 0;

    const update = () => {
      raf = 0;
      const rect = el.getBoundingClientRect();
      if (rect.top === 0 && rect.height === 0) {
        // jsdom / display:none — no layout to measure.
        setMaxHeight(null);
        return;
      }
      const next = Math.max(MIN_HEIGHT, Math.round(window.innerHeight - rect.top - bottomGapPx));
      setMaxHeight((prev) => (prev === next ? prev : next));
    };

    const schedule = () => {
      if (!raf) raf = requestAnimationFrame(update);
    };

    // First measure after layout settles.
    schedule();
    window.addEventListener('resize', schedule);
    const ro = new ResizeObserver(schedule);
    ro.observe(document.body);
    return () => {
      window.removeEventListener('resize', schedule);
      ro.disconnect();
      if (raf) cancelAnimationFrame(raf);
    };
  }, [frameRef, bottomGapPx]);

  return maxHeight;
}
