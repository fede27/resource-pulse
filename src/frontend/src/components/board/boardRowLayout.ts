// Pure vertical row layout for the boards — the Y-axis sibling of boardGeo.
// Row heights are DERIVED FROM STATE (px tokens + lane counts + expanded set),
// never measured from the DOM, so the whole layout is a pure, unit-testable
// walk. Windowing is presentation-only: pages keep computing stats/KPIs on the
// full dataset and only the rendered children go through `windowRows`.
//
// Rendering model: rows stay in normal document flow; runs of hidden rows are
// replaced by fixed-height gap segments. This preserves sticky-left labels,
// per-row borders and the alt striping without absolute positioning.

export type RowItem = { key: string; height: number };

export type RowSegment<T extends RowItem> =
  | { kind: 'gap'; key: string; height: number }
  | { kind: 'row'; item: T; index: number };

export type WindowedRows<T extends RowItem> = {
  segments: Array<RowSegment<T>>;
  totalHeight: number;
};

/**
 * Slices an ordered row list to the visible vertical window `[minY, maxY]`
 * (content-space px, as produced by `useVisibleYRange`). A row is emitted when
 * it intersects the window OR its key is pinned (rows with live interactions —
 * open popover, active drag — must never unmount); consecutive hidden rows
 * coalesce into a single gap segment. An unbounded range yields zero gaps, so
 * the jsdom/unmeasurable fallback renders the full board.
 */
export function windowRows<T extends RowItem>(
  items: readonly T[],
  minY: number,
  maxY: number,
  pinnedKeys?: ReadonlySet<string>,
): WindowedRows<T> {
  const segments: Array<RowSegment<T>> = [];
  let offset = 0;
  let gapStart = -1; // index of the first row in the pending hidden run
  let gapHeight = 0;

  const flushGap = () => {
    if (gapHeight > 0) {
      segments.push({ kind: 'gap', key: `gap-${gapStart}`, height: gapHeight });
      gapStart = -1;
      gapHeight = 0;
    }
  };

  for (let i = 0; i < items.length; i++) {
    const item = items[i]!;
    const visible = offset + item.height >= minY && offset <= maxY;
    if (visible || pinnedKeys?.has(item.key)) {
      flushGap();
      segments.push({ kind: 'row', item, index: i });
    } else {
      if (gapStart < 0) gapStart = i;
      gapHeight += item.height;
    }
    offset += item.height;
  }
  flushGap();

  return { segments, totalHeight: offset };
}
