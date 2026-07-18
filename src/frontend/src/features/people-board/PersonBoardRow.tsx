import { memo, useMemo, useRef, useState, type MouseEvent } from 'react';
import { RightOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import { useTranslation } from 'react-i18next';
import { InitialsAvatar } from '@/components/domain/InitialsAvatar';
import type { BoardGeo, VisibleXRange } from '@/components/board';
import { bandLabelFor, loadColor, NO_CAPACITY_CELL, type LoadBand } from '@/lib/loadBands';
import {
  bucketStat,
  isoAtX,
  personLanes,
  snapISO,
  type BoardBucket,
  type PersonBlock,
  type PersonData,
  type PersonStats,
} from './peopleBoardModel';
import { hueAlpha, projectHue } from './projectHue';
import { CoverPopover, type PendingRange } from './CoverPopover';
import type { RootProjectOption } from './usePeopleBoard';
import type { Metric } from './PeopleBoardToolbar';
import { OFF_CALENDAR_HATCH, useStyles } from './PersonBoardRow.styles';

export type InspectTarget =
  | { kind: 'range'; personId: string }
  | { kind: 'cell'; personId: string; bucket: BoardBucket }
  | { kind: 'block'; personId: string; block: PersonBlock };

export type PersonBoardRowProps = {
  data: PersonData;
  geo: BoardGeo;
  // Pre-filtered to the visible window by the page (horizontal windowing) —
  // rendering only; stats/filters upstream use the full bucket list.
  buckets: BoardBucket[];
  visibleX: VisibleXRange;
  metric: Metric;
  countTentative: boolean;
  bands: LoadBand[];
  stats: PersonStats;
  expanded: boolean;
  alt: boolean;
  // Keyed by person id so the page can pass ONE stable callback to every row
  // (React.memo needs referentially stable props to skip re-renders).
  onToggle: (personId: string) => void;
  onInspect: (target: InspectTarget) => void;
  // Pin/unpin this row against the vertical windowing while an interaction
  // with row-local state is live (free-lane drag, open CoverPopover): a
  // windowed-out unmount would silently kill it. Stable, id-keyed.
  onPinChange?: (personId: string, pinned: boolean) => void;
  rootProjects: RootProjectOption[];
};

const fmtPeak = (n: number) => `${Math.round(n)}%`;

// One person on the board: the collapsed heatmap row (bucket-average bands)
// plus, when expanded, one lane per root project and the drag-to-cover
// free-capacity lane. Memoized: at day grain the board mounts hundreds of
// cells per row — unrelated page state (query, inspector, other rows'
// expansion) must not re-render them.
export const PersonBoardRow = memo(function PersonBoardRow(props: PersonBoardRowProps) {
  const { data, geo, buckets, expanded, alt } = props;
  const { t } = useTranslation();
  const { styles, cx } = useStyles();
  const person = data.person;

  const lanes = useMemo(() => personLanes(data.blocks), [data.blocks]);
  // Peak is always finite: off-calendar buckets (pct null) don't participate.
  const peakColor = loadColor(props.stats.peak, props.bands);
  const peakBand = bandLabelFor(props.stats.peak, props.bands);

  const subParts = [person.roleName, person.teamName, t('peopleBoard.row.capPerWeek', { hours: Math.round(data.weeklyCapH) })];

  return (
    <div className={styles.block}>
      <div className={cx(styles.row, alt && styles.rowAlt)}>
        <div className={styles.labelCell}>
          <span
            className={cx(styles.chevron, expanded && styles.chevronOpen)}
            role="button"
            aria-label={t('peopleBoard.row.toggle')}
            onClick={() => props.onToggle(person.id)}
          >
            <RightOutlined style={{ fontSize: 12 }} />
          </span>
          <InitialsAvatar name={person.name} size={28} />
          <div className={styles.labelMain} onClick={() => props.onInspect({ kind: 'range', personId: person.id })}>
            <div className={styles.personName}>{person.name}</div>
            <div className={styles.personSub}>{subParts.filter(Boolean).join(' · ')}</div>
          </div>
          {/* dynamic: peak pill colours follow the configured band of the peak. */}
          <span
            className={styles.peakPill}
            style={{ background: peakColor.bg, color: peakColor.fg, border: `1px solid ${peakColor.solid}` }}
            title={t('peopleBoard.row.peakTitle', {
              peak: fmtPeak(props.stats.peak),
              min: fmtPeak(props.stats.min),
            })}
          >
            {peakBand} {fmtPeak(props.stats.peak)}
          </span>
        </div>
        {/* dynamic: axis width computed from the domain. */}
        <div className={styles.axisCell} style={{ width: geo.contentW }}>
          {/* Explicit minimal props (no spread): with hundreds of cells per
              row, per-cell object churn is the render bottleneck. */}
          {buckets.map((bk, i) => (
            <HeatCell
              key={i}
              data={data}
              bucket={bk}
              metric={props.metric}
              countTentative={props.countTentative}
              bands={props.bands}
              styles={styles}
              t={t}
              onInspect={props.onInspect}
            />
          ))}
        </div>
      </div>

      {expanded && (
        <div className={styles.lanes}>
          {lanes.map((lane) => {
            const hue = projectHue(lane.rootProjectId);
            // Same horizontal windowing as the cells: only bars intersecting
            // the visible range are mounted.
            const visibleBlocks = lane.blocks.filter((b) => {
              const left = geo.xPx(b.from);
              return left <= props.visibleX.maxX && left + geo.wPxInclusive(b.from, b.to) >= props.visibleX.minX;
            });
            return (
              <div key={lane.rootProjectId} className={styles.lane}>
                <div className={styles.laneLabel}>
                  {/* dynamic: per-project accent. */}
                  <span className={styles.laneDot} style={{ background: hue.accent }} />
                  <span className={styles.laneName}>{lane.projectName}</span>
                </div>
                {/* dynamic: axis width computed from the domain. */}
                <div className={styles.axisCell} style={{ width: geo.contentW }}>
                  {visibleBlocks.map((b) => (
                    <BlockBar key={b.id} block={b} geo={geo} metric={props.metric} data={data} onInspect={props.onInspect} />
                  ))}
                </div>
              </div>
            );
          })}
          <div className={styles.lane}>
            <div className={styles.laneLabel}>
              <span className={styles.laneGhostDot} />
              <span className={styles.laneFreeName}>{t('peopleBoard.row.freeLane')}</span>
            </div>
            {/* dynamic: axis width computed from the domain. */}
            <div className={styles.axisCell} style={{ width: geo.contentW }}>
              <FreeLane data={data} geo={geo} rootProjects={props.rootProjects} onPinChange={props.onPinChange} />
            </div>
          </div>
        </div>
      )}
    </div>
  );
});

// ── Heatmap cell (bucket-average utilization, config-driven bands) ────────

type HeatCellProps = {
  data: PersonData;
  bucket: BoardBucket;
  metric: Metric;
  countTentative: boolean;
  bands: LoadBand[];
  // Hoisted from the row: at day grain there are hundreds of cells per row —
  // per-cell useStyles/useTranslation hooks were the profiled hot path.
  styles: ReturnType<typeof useStyles>['styles'];
  t: ReturnType<typeof useTranslation>['t'];
  onInspect: (target: InspectTarget) => void;
};

const HeatCell = memo(function HeatCell({
  data,
  bucket,
  metric,
  countTentative,
  bands,
  styles,
  t,
  onInspect,
}: HeatCellProps) {
  const stat = bucketStat(data, bucket, countTentative);
  // Zero-capacity bucket: utilization is undefined — neutral cell, never a
  // band colour. With active blocks it's the "fuori calendario" state: a
  // discreet hatch + explanatory tooltip, no number (0h counted).
  const c = stat.pct !== null ? loadColor(stat.pct, bands) : NO_CAPACITY_CELL;
  const label =
    metric === 'hours'
      ? String(Math.round(stat.allocH))
      : stat.pct !== null
        ? String(Math.round(stat.pct))
        : '';

  // Tooltip computed LAZILY on hover: eager i18next interpolation per cell was
  // a measurable slice of the day-grain render (cells × 1-2 t() calls).
  const setTitle = (e: MouseEvent<HTMLDivElement>) => {
    e.currentTarget.title = stat.offCalendar
      ? t('peopleBoard.cell.titleOffCalendar', { from: bucket.from })
      : t('peopleBoard.cell.title', {
          from: bucket.from,
          pct: stat.pct !== null ? Math.round(stat.pct) : '—',
          alloc: Math.round(stat.allocH),
          cap: Math.round(stat.capH),
        }) + (countTentative ? t('peopleBoard.cell.titleTent') : '');
  };

  return (
    // dynamic: cell geometry + band colours resolved from live data.
    <div
      className={styles.heatCell}
      style={{
        left: bucket.x + 1,
        width: Math.max(2, bucket.w - 2),
        background: stat.offCalendar ? `${OFF_CALENDAR_HATCH}, ${c.bg}` : c.bg,
        border: `1px solid ${c.empty ? 'transparent' : c.solid}`,
      }}
      onMouseEnter={setTitle}
      onClick={() => onInspect({ kind: 'cell', personId: data.person.id, bucket })}
    >
      {/* dynamic: band text colour. */}
      {bucket.w >= 30 && !c.empty && <span style={{ color: c.fg }}>{label}</span>}
    </div>
  );
});

// ── Coverage block bar (expanded lane) ────────────────────────────────────

function BlockBar({
  block,
  geo,
  metric,
  data,
  onInspect,
}: {
  block: PersonBlock;
  geo: BoardGeo;
  metric: Metric;
  data: PersonData;
  onInspect: (target: InspectTarget) => void;
}) {
  const { t } = useTranslation();
  const { styles } = useStyles();
  const hue = projectHue(block.rootProjectId);
  const left = geo.xPx(block.from);
  const width = geo.wPxInclusive(block.from, block.to);
  // Hours label = % × capacity over the block∩domain window (client bridge —
  // list reads carry null ResolvedHours by design, ADR-0013).
  const hours = useMemo(() => {
    if (metric !== 'hours') return null;
    let total = 0;
    let d = dayjs(block.from < geo.minISO ? geo.minISO : block.from);
    const end = dayjs(block.to > geo.maxISO ? geo.maxISO : block.to);
    while (!d.isAfter(end)) {
      total += (block.percent / 100) * (data.capacityByDay.get(d.format('YYYY-MM-DD')) ?? 0);
      d = d.add(1, 'day');
    }
    return Math.round(total);
  }, [metric, block, geo.minISO, geo.maxISO, data.capacityByDay]);

  return (
    // dynamic: bar geometry + per-project hard/tentative styling.
    <div
      className={styles.bar}
      style={{
        left,
        width,
        background: block.hard
          ? hueAlpha(hue.accent, 0.16)
          : `repeating-linear-gradient(135deg, ${hueAlpha(hue.accent, 0.14)} 0 5px, ${hueAlpha(hue.accent, 0.03)} 5px 10px)`,
        border: `1px ${block.hard ? 'solid' : 'dashed'} ${hueAlpha(hue.accent, 0.55)}`,
        borderLeft: `3px solid ${hue.accent}`,
      }}
      title={`${block.projectName} · ${block.percent}% · ${t(block.hard ? 'peopleBoard.row.hard' : 'peopleBoard.row.tentative')}`}
      onClick={() => onInspect({ kind: 'block', personId: data.person.id, block })}
    >
      {/* dynamic: text colour follows the project hue. */}
      {width > 42 && (
        <span style={{ color: hue.text }}>{metric === 'hours' ? `${hours}h` : `${block.percent}%`}</span>
      )}
      {width > 96 && !block.hard && (
        <span style={{ color: hue.text, opacity: 0.7, fontWeight: 400 }}>
          {t('peopleBoard.row.tentative')}
        </span>
      )}
    </div>
  );
}

// ── Free-capacity lane: drag to cover an open demand ─────────────────────

function FreeLane({
  data,
  geo,
  rootProjects,
  onPinChange,
}: {
  data: PersonData;
  geo: BoardGeo;
  rootProjects: RootProjectOption[];
  onPinChange?: ((personId: string, pinned: boolean) => void) | undefined;
}) {
  const { t } = useTranslation();
  const { styles } = useStyles();
  const ref = useRef<HTMLDivElement | null>(null);
  const [drag, setDrag] = useState<{ x0: number; x1: number } | null>(null);
  const [pending, setPending] = useState<PendingRange | null>(null);

  // Drag state and the pending popover live HERE: the row must stay mounted
  // (pinned against the vertical windowing) for as long as either is active.
  const setPin = (on: boolean) => onPinChange?.(data.person.id, on);

  const snappedISO = (x: number) => snapISO(isoAtX(geo, x), geo.bucket);

  const onDown = (e: React.MouseEvent) => {
    if (pending) return;
    const rect = ref.current?.getBoundingClientRect();
    if (!rect) return;
    const x = e.clientX - rect.left;
    setDrag({ x0: x, x1: x });
    setPin(true);
    e.preventDefault();
  };
  const onMove = (e: React.MouseEvent) => {
    if (!drag) return;
    const rect = ref.current?.getBoundingClientRect();
    if (!rect) return;
    setDrag((d) => (d ? { ...d, x1: e.clientX - rect.left } : d));
  };
  const onUp = () => {
    if (!drag) return;
    const a = Math.min(drag.x0, drag.x1);
    const b = Math.max(drag.x0, drag.x1);
    let popoverOpened = false;
    if (b - a > 10) {
      const from = snappedISO(a);
      const toExcl = snappedISO(b);
      if (from < toExcl) {
        setPending({ from, toExcl, anchorX: geo.xPx(toExcl) });
        popoverOpened = true;
      }
    }
    setDrag(null);
    if (!popoverOpened) setPin(false);
  };

  // Live ghost, snapped to the grain boundaries.
  const ghost = drag
    ? (() => {
        const from = snappedISO(Math.min(drag.x0, drag.x1));
        const toExcl = snappedISO(Math.max(drag.x0, drag.x1));
        const left = geo.xPx(from);
        const width = Math.max(0, geo.xPx(toExcl) - left);
        const weeks = dayjs(toExcl).diff(dayjs(from), 'day') / 7;
        return { left, width, weeks };
      })()
    : null;

  return (
    <div
      ref={ref}
      className={styles.freeLane}
      onMouseDown={onDown}
      onMouseMove={onMove}
      onMouseUp={onUp}
      onMouseLeave={() => drag && onUp()}
    >
      {!drag && !pending && <div className={styles.freeHint}>{t('peopleBoard.row.freeHint')}</div>}
      {ghost && ghost.width > 4 && (
        // dynamic: ghost geometry follows the drag.
        <div className={styles.dragGhost} style={{ left: ghost.left, width: ghost.width }}>
          <span>{t('peopleBoard.row.dragWeeks', { weeks: ghost.weeks.toFixed(1) })}</span>
        </div>
      )}
      {pending && (
        // dynamic: popover anchor at the drag release point; the popup itself
        // is portaled + auto-flipped by AntD.
        <div className={styles.popoverAnchor} style={{ left: Math.min(pending.anchorX, geo.contentW - 8) }}>
          <CoverPopover
            person={data}
            pending={pending}
            rootProjects={rootProjects}
            onClose={() => {
              setPending(null);
              setPin(false);
            }}
          />
        </div>
      )}
    </div>
  );
}
