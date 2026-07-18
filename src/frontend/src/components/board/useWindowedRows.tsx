import { useMemo, type RefObject } from 'react';
import { useVisibleYRange } from './useVisibleYRange';
import { windowRows, type RowItem, type WindowedRows } from './boardRowLayout';

// The single wiring point for vertical row windowing, shared by the boards:
// observes the board scroller's vertical window and slices the page's row list
// to it. Pages build `items` (key + derived height + row payload) and render
// the returned segments — `RowGap` for gaps, their row component otherwise.
export function useWindowedRows<T extends RowItem>(
  scrollRef: RefObject<HTMLDivElement | null>,
  items: readonly T[],
  pinnedKeys?: ReadonlySet<string>,
): WindowedRows<T> {
  const y = useVisibleYRange(scrollRef);
  return useMemo(() => windowRows(items, y.minY, y.maxY, pinnedKeys), [items, y, pinnedKeys]);
}

/** Fixed-height placeholder standing in for a run of windowed-out rows. */
export function RowGap({ height }: { height: number }) {
  // dynamic: gap height = sum of the hidden rows' derived heights.
  return <div style={{ height }} aria-hidden />;
}
