// Owns the horizontal scroll plumbing for a virtualized timeline: maps the
// container's scrollLeft → the visible logical index range, sizes the scroll
// spacer, and recenters on the epoch (today) when the grain changes.
//
// Exposes a *callback ref* (`setScrollEl`) rather than a ref object so consumers
// never read a ref during render (react-hooks/refs). The hook holds DOM-derived
// state (scrollLeft, width); the consumer binds `setScrollEl`/`onScroll` to its
// scroll container and derives buckets from `visible`. Domain-free, reusable.

import { useCallback, useLayoutEffect, useMemo, useRef, useState } from 'react';

export type TimelineViewport = {
  setScrollEl: (el: HTMLDivElement | null) => void;
  onScroll: () => void;
  visible: { kStart: number; kEnd: number };
  spacerWidth: number;
  leftOf: (k: number) => number;
  /** Imperatively center the viewport on bucket index `k` (k=0 = today). */
  scrollToIndex: (k: number) => void;
  scrollLeft: number;
  width: number;
  ready: boolean;
};

export function useTimelineViewport(params: {
  cellW: number;
  nameW: number;
  kMin: number;
  kMax: number;
  buffer?: number;
  /** Changing this recenters on k=0 (e.g. the grain key). */
  recenterKey: string | number;
}): TimelineViewport {
  const { cellW, nameW, kMin, kMax, buffer = 6, recenterKey } = params;
  const elRef = useRef<HTMLDivElement | null>(null);
  const roRef = useRef<ResizeObserver | null>(null);
  const rafRef = useRef(0);
  const [scrollLeft, setScrollLeft] = useState(0);
  const [width, setWidth] = useState(0);

  const setScrollEl = useCallback((el: HTMLDivElement | null) => {
    elRef.current = el;
    roRef.current?.disconnect();
    if (el) {
      setWidth(el.clientWidth);
      const ro = new ResizeObserver(() => setWidth(el.clientWidth));
      ro.observe(el);
      roRef.current = ro;
    } else {
      roRef.current = null;
    }
  }, []);

  const onScroll = useCallback(() => {
    if (rafRef.current) return;
    rafRef.current = requestAnimationFrame(() => {
      rafRef.current = 0;
      const el = elRef.current;
      if (el) setScrollLeft(el.scrollLeft);
    });
  }, []);

  const spacerWidth = nameW + (kMax - kMin + 1) * cellW;

  // Center the viewport's column area on bucket `k`.
  const scrollToIndex = useCallback(
    (k: number) => {
      const el = elRef.current;
      if (!el) return;
      const w = el.clientWidth;
      const maxLeft = Math.max(0, spacerWidth - w);
      const target = Math.min(
        maxLeft,
        Math.max(0, (k - kMin) * cellW + cellW / 2 - (w - nameW) / 2),
      );
      el.scrollLeft = target;
      setScrollLeft(target);
      setWidth(w);
    },
    [cellW, kMin, nameW, spacerWidth],
  );

  // Default the scroll to today's bucket once the container is actually mounted
  // and measured (the grid mounts after its data loads, so this can't run on the
  // first render), and recenter whenever the grain changes. Guarded by a ref so a
  // resize (width change) does NOT yank the scroll back to today.
  const centeredForRef = useRef<string | number | null>(null);
  useLayoutEffect(() => {
    if (!elRef.current || width <= 0) return;
    if (centeredForRef.current === recenterKey) return;
    centeredForRef.current = recenterKey;
    scrollToIndex(0);
  }, [recenterKey, width, scrollToIndex]);

  const leftOf = useCallback((k: number) => nameW + (k - kMin) * cellW, [nameW, kMin, cellW]);

  const visible = useMemo(() => {
    if (width <= 0) return { kStart: kMin, kEnd: kMin };
    const relFirst = Math.max(0, Math.floor(scrollLeft / cellW) - buffer);
    const relLast = Math.ceil((scrollLeft + width - nameW) / cellW) + buffer;
    return {
      kStart: Math.max(kMin, kMin + relFirst),
      kEnd: Math.min(kMax, kMin + relLast),
    };
  }, [scrollLeft, width, cellW, nameW, kMin, kMax, buffer]);

  return {
    setScrollEl,
    onScroll,
    visible,
    spacerWidth,
    leftOf,
    scrollToIndex,
    scrollLeft,
    width,
    ready: width > 0,
  };
}
