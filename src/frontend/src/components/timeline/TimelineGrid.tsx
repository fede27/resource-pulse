// Presentational shell for a virtualized timeline grid: a scroll container with
// a bounded-but-large spacer, a sticky two-level time header, a frozen first
// column, and an optional column-hover overlay. It owns only horizontal layout
// and positioning; the consumer renders the body rows (via TimelineRow /
// TimelineCell) using the same geometry. Domain-free and reusable.

import {
  createContext,
  useContext,
  useRef,
  type CSSProperties,
  type ReactNode,
} from 'react';
import type { Bucket, Group } from './timeAxis';
import type { TimelineViewport } from './useTimelineViewport';

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

const HEAD_BG = '#fafafa';

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

  const corner: CSSProperties = {
    position: 'sticky',
    left: 0,
    zIndex: 42,
    width: nameW,
    background: HEAD_BG,
    borderRight: '1px solid #e8e8e8',
    display: 'flex',
    alignItems: 'center',
  };

  return (
    <Ctx.Provider value={{ leftOf, cellW, nameW, spacerWidth }}>
      <div style={{ border: '1px solid #f0f0f0', borderRadius: 10, overflow: 'hidden', background: '#fff' }}>
        <div
          ref={setScrollEl}
          onScroll={onScroll}
          onMouseMove={onMove}
          onMouseLeave={onLeave}
          style={{ overflow: 'auto', maxHeight, position: 'relative' }}
        >
          <div style={{ position: 'relative', width: spacerWidth, minWidth: spacerWidth }}>
            {columnHover && (
              <div
                ref={overlayRef}
                style={{
                  position: 'absolute',
                  top: 0,
                  height: '100%',
                  display: 'none',
                  background: 'rgba(22,119,255,.06)',
                  pointerEvents: 'none',
                  zIndex: 6,
                }}
              />
            )}

            {/* ── Sticky two-level header ── */}
            <div style={{ position: 'sticky', top: 0, zIndex: 30 }}>
              <div style={{ position: 'relative', width: spacerWidth, height: groupH, background: HEAD_BG }}>
                <div style={{ ...corner, height: groupH, borderBottom: '1px solid #f0f0f0' }}>{cornerTop}</div>
                {groups.map((g) => (
                  <div
                    key={g.key}
                    style={{
                      position: 'absolute',
                      top: 0,
                      left: leftOf(g.startIdx),
                      width: g.count * cellW,
                      height: groupH,
                      background: HEAD_BG,
                      borderBottom: '1px solid #f0f0f0',
                      borderLeft: '1px solid #e8e8e8',
                      display: 'flex',
                      alignItems: 'center',
                      padding: '0 10px',
                      fontSize: 12,
                      fontWeight: 600,
                      color: 'rgba(0,0,0,.75)',
                    }}
                  >
                    {renderGroup(g)}
                  </div>
                ))}
              </div>
              <div style={{ position: 'relative', width: spacerWidth, height: bucketH, background: HEAD_BG }}>
                <div style={{ ...corner, height: bucketH, borderBottom: '1px solid #e8e8e8' }}>{cornerBottom}</div>
                {buckets.map((b) => (
                  <div
                    key={b.idx}
                    style={{ position: 'absolute', top: 0, left: leftOf(b.idx), width: cellW, height: bucketH }}
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
  background = '#fff',
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
  const { spacerWidth, nameW } = useTimelineCtx();
  return (
    <div style={{ position: 'relative', width: spacerWidth, height, background, ...(borderBottom ? { borderBottom } : {}) }}>
      <div
        style={{
          position: 'sticky',
          left: 0,
          zIndex: 20,
          width: nameW,
          height,
          background: nameBackground ?? background,
          borderRight: '1px solid #e8e8e8',
          display: 'flex',
          alignItems: 'center',
          padding: namePadding,
        }}
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
  style,
  children,
}: {
  k: number;
  height: number;
  width?: number;
  title?: string;
  style?: CSSProperties;
  children?: ReactNode;
}) {
  const { leftOf, cellW } = useTimelineCtx();
  return (
    <div
      title={title}
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
