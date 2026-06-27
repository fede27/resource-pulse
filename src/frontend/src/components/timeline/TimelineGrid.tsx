// Presentational shell for a virtualized timeline grid: a scroll container with
// a bounded-but-large spacer, a sticky two-level time header, a frozen first
// column, and an optional column-hover overlay. It owns only horizontal layout
// and positioning; the consumer renders the body rows (via TimelineRow /
// TimelineCell) using the same geometry. Domain-free and reusable.
//
// Almost every dimension here (left offsets, widths, row heights) is derived
// from the viewport/axis at runtime, so those stay inline as documented
// `// dynamic:` values; the structural colours/borders come from tokens via
// createStyles.

import {
  createContext,
  useContext,
  useRef,
  type CSSProperties,
  type ReactNode,
} from 'react';
import { theme } from 'antd';
import type { Bucket, Group } from './timeAxis';
import type { TimelineViewport } from './useTimelineViewport';
import { useStyles } from './TimelineGrid.styles';

type TimelineCtx = {
  leftOf: (k: number) => number;
  cellW: number;
  nameW: number;
  spacerWidth: number;
};

const Ctx = createContext<TimelineCtx | null>(null);

function useTimelineCtx(): TimelineCtx {
  const c = useContext(Ctx);
  if (!c) throw new Error('TimelineRow/TimelineCell must be used inside <TimelineGrid>');
  return c;
}

export type TimelineGridProps = {
  viewport: TimelineViewport;
  buckets: Bucket[];
  groups: Group[];
  cellW: number;
  nameW: number;
  headerHeights?: { group: number; bucket: number };
  maxHeight?: number | string;
  cornerTop: ReactNode;
  cornerBottom: ReactNode;
  renderGroup: (g: Group) => ReactNode;
  renderBucketHeader: (b: Bucket) => ReactNode;
  /** Body rows — built with TimelineRow / TimelineCell. */
  children: ReactNode;
  /** Highlight the hovered column. Default true. */
  columnHover?: boolean;
};

export function TimelineGrid({
  viewport,
  buckets,
  groups,
  cellW,
  nameW,
  headerHeights = { group: 26, bucket: 46 },
  maxHeight = 'min(640px, calc(100vh - 320px))',
  cornerTop,
  cornerBottom,
  renderGroup,
  renderBucketHeader,
  children,
  columnHover = true,
}: TimelineGridProps) {
  const { styles, cx } = useStyles();
  const { setScrollEl, onScroll, spacerWidth, leftOf } = viewport;
  const groupH = headerHeights.group;
  const bucketH = headerHeights.bucket;

  const overlayRef = useRef<HTMLDivElement>(null);
  const rafRef = useRef(0);
  const onMove = (e: React.MouseEvent<HTMLDivElement>) => {
    if (!columnHover) return;
    const sc = e.currentTarget;
    const ov = overlayRef.current;
    if (!ov) return;
    const contentX = e.clientX - sc.getBoundingClientRect().left + sc.scrollLeft;
    if (rafRef.current) return;
    rafRef.current = requestAnimationFrame(() => {
      rafRef.current = 0;
      if (contentX < nameW) {
        ov.style.display = 'none';
        return;
      }
      const left = nameW + Math.floor((contentX - nameW) / cellW) * cellW;
      ov.style.display = 'block';
      ov.style.left = `${left}px`;
      ov.style.width = `${cellW}px`;
    });
  };
  const onLeave = () => {
    if (overlayRef.current) overlayRef.current.style.display = 'none';
  };

  return (
    <Ctx.Provider value={{ leftOf, cellW, nameW, spacerWidth }}>
      <div className={styles.frame}>
        <div
          ref={setScrollEl}
          onScroll={onScroll}
          onMouseMove={onMove}
          onMouseLeave={onLeave}
          className={styles.scroll}
          // dynamic: caller-provided viewport cap.
          style={{ maxHeight }}
        >
          {/* dynamic: spacer spans the full logical timeline width. */}
          <div style={{ position: 'relative', width: spacerWidth, minWidth: spacerWidth }}>
            {columnHover && (
              // left/width/display are driven imperatively in onMove (rAF) for
              // per-frame hover tracking — runtime values, not author-time.
              <div ref={overlayRef} className={styles.overlay} />
            )}

            {/* ── Sticky two-level header ── */}
            <div className={styles.stickyHead}>
              {/* dynamic: band spans the timeline and is groupH tall. */}
              <div className={styles.headBand} style={{ width: spacerWidth, height: groupH }}>
                <div
                  className={cx(styles.corner, styles.cornerBorderLight)}
                  // dynamic: frozen-column width + header-band height.
                  style={{ width: nameW, height: groupH }}
                >
                  {cornerTop}
                </div>
                {groups.map((g) => (
                  <div
                    key={g.key}
                    className={styles.groupCell}
                    // dynamic: group spans `count` cells starting at its index.
                    style={{ left: leftOf(g.startIdx), width: g.count * cellW, height: groupH }}
                  >
                    {renderGroup(g)}
                  </div>
                ))}
              </div>
              <div className={styles.headBand} style={{ width: spacerWidth, height: bucketH }}>
                <div
                  className={cx(styles.corner, styles.cornerBorderDark)}
                  style={{ width: nameW, height: bucketH }}
                >
                  {cornerBottom}
                </div>
                {buckets.map((b) => (
                  <div
                    key={b.idx}
                    className={styles.bucketCell}
                    // dynamic: each bucket sits at its computed left offset.
                    style={{ left: leftOf(b.idx), width: cellW, height: bucketH }}
                  >
                    {renderBucketHeader(b)}
                  </div>
                ))}
              </div>
            </div>

            {/* ── Body ── */}
            {children}
          </div>
        </div>
      </div>
    </Ctx.Provider>
  );
}

// ── Body building blocks ───────────────────────────────────────────────────

export function TimelineRow({
  height,
  name,
  background,
  nameBackground,
  borderBottom,
  namePadding = '0 10px',
  children,
}: {
  height: number;
  name: ReactNode;
  background?: string;
  nameBackground?: string;
  borderBottom?: string;
  namePadding?: string;
  children?: ReactNode;
}) {
  const { styles } = useStyles();
  const { token } = theme.useToken();
  const { spacerWidth, nameW } = useTimelineCtx();
  const rowBg = background ?? token.colorBgContainer;
  return (
    // dynamic: row geometry + caller-chosen background/divider.
    <div style={{ position: 'relative', width: spacerWidth, height, background: rowBg, ...(borderBottom ? { borderBottom } : {}) }}>
      <div
        className={styles.rowName}
        // dynamic: frozen-column width/height, caller background + padding.
        style={{ width: nameW, height, background: nameBackground ?? rowBg, padding: namePadding }}
      >
        {name}
      </div>
      {children}
    </div>
  );
}

export function TimelineCell({
  k,
  height,
  width,
  title,
  className,
  style,
  children,
}: {
  k: number;
  height: number;
  width?: number;
  title?: string;
  className?: string;
  style?: CSSProperties;
  children?: ReactNode;
}) {
  const { leftOf, cellW } = useTimelineCtx();
  return (
    <div
      title={title}
      className={className}
      // dynamic: cell position/size from the axis; `style` is caller content.
      style={{ position: 'absolute', top: 0, left: leftOf(k), width: width ?? cellW, height, ...style }}
    >
      {children}
    </div>
  );
}

// A full-width filler that starts after the frozen column (e.g. hatched
// "add row" backgrounds). Spans the whole logical extent.
export function TimelineRowFiller({ height, background }: { height: number; background: string }) {
  const { spacerWidth, nameW } = useTimelineCtx();
  return (
    <div
      // dynamic: spans from the frozen column to the end of the timeline.
      style={{
        position: 'absolute',
        top: 0,
        left: nameW,
        width: spacerWidth - nameW,
        height,
        background,
      }}
    />
  );
}
