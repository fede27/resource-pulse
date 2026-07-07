import { useMemo, useRef, useState } from 'react';
import { RightOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import { useTranslation } from 'react-i18next';
import { InitialsAvatar } from '@/components/domain/InitialsAvatar';
import type { BoardGeo } from '@/components/board';
import { bandLabelFor, loadColor, type LoadBand } from '@/lib/loadBands';
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
import { useStyles } from './PersonBoardRow.styles';

export type InspectTarget =
  | { kind: 'range'; personId: string }
  | { kind: 'cell'; personId: string; bucket: BoardBucket }
  | { kind: 'block'; personId: string; block: PersonBlock };

export type PersonBoardRowProps = {
  data: PersonData;
  geo: BoardGeo;
  buckets: BoardBucket[];
  metric: Metric;
  countTentative: boolean;
  bands: LoadBand[];
  stats: PersonStats;
  expanded: boolean;
  alt: boolean;
  onToggle: () => void;
  onInspect: (target: InspectTarget) => void;
  rootProjects: RootProjectOption[];
};

const fmtPeak = (n: number) => (Number.isFinite(n) ? `${Math.round(n)}%` : '∞');

// One person on the board: the collapsed heatmap row (bucket-average bands)
// plus, when expanded, one lane per root project and the drag-to-cover
// free-capacity lane.
export function PersonBoardRow(props: PersonBoardRowProps) {
  const { data, geo, buckets, expanded, alt } = props;
  const { t } = useTranslation();
  const { styles, cx } = useStyles();
  const person = data.person;

  const lanes = useMemo(() => personLanes(data.blocks), [data.blocks]);
  const peakColor = loadColor(props.stats.peak, props.bands);
  const peakBand = Number.isFinite(props.stats.peak)
    ? bandLabelFor(props.stats.peak, props.bands)
    : (props.bands[props.bands.length - 1]?.label ?? '—');

  const subParts = [person.roleName, person.teamName, t('peopleBoard.row.capPerWeek', { hours: Math.round(data.weeklyCapH) })];

  return (
    <div className={styles.block}>
      <div className={cx(styles.row, alt && styles.rowAlt)}>
        <div className={styles.labelCell}>
          <span
            className={cx(styles.chevron, expanded && styles.chevronOpen)}
            role="button"
            aria-label={t('peopleBoard.row.toggle')}
            onClick={props.onToggle}
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
          {buckets.map((bk, i) => (
            <HeatCell key={i} {...props} bucket={bk} />
          ))}
        </div>
      </div>

      {expanded && (
        <div className={styles.lanes}>
          {lanes.map((lane) => {
            const hue = projectHue(lane.rootProjectId);
            return (
              <div key={lane.rootProjectId} className={styles.lane}>
                <div className={styles.laneLabel}>
                  {/* dynamic: per-project accent. */}
                  <span className={styles.laneDot} style={{ background: hue.accent }} />
                  <span className={styles.laneName}>{lane.projectName}</span>
                </div>
                {/* dynamic: axis width computed from the domain. */}
                <div className={styles.axisCell} style={{ width: geo.contentW }}>
                  {lane.blocks.map((b) => (
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
              <FreeLane data={data} geo={geo} rootProjects={props.rootProjects} />
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

// ── Heatmap cell (bucket-average utilization, config-driven bands) ────────

function HeatCell({
  data,
  bucket,
  metric,
  countTentative,
  bands,
  onInspect,
}: PersonBoardRowProps & { bucket: BoardBucket }) {
  const { t } = useTranslation();
  const { styles } = useStyles();
  const stat = useMemo(() => bucketStat(data, bucket, countTentative), [data, bucket, countTentative]);
  const c = loadColor(stat.pct, bands);
  const label =
    metric === 'hours'
      ? String(Math.round(stat.allocH))
      : Number.isFinite(stat.pct)
        ? String(Math.round(stat.pct))
        : '∞';
  const title =
    t('peopleBoard.cell.title', {
      from: bucket.from,
      pct: Number.isFinite(stat.pct) ? Math.round(stat.pct) : '∞',
      alloc: Math.round(stat.allocH),
      cap: Math.round(stat.capH),
    }) + (countTentative ? t('peopleBoard.cell.titleTent') : '');

  return (
    // dynamic: cell geometry + band colours resolved from live data.
    <div
      className={styles.heatCell}
      style={{
        left: bucket.x + 1,
        width: Math.max(2, bucket.w - 2),
        background: c.bg,
        border: `1px solid ${c.empty ? 'transparent' : c.solid}`,
      }}
      title={title}
      onClick={() => onInspect({ kind: 'cell', personId: data.person.id, bucket })}
    >
      {/* dynamic: band text colour. */}
      {bucket.w >= 30 && !c.empty && <span style={{ color: c.fg }}>{label}</span>}
    </div>
  );
}

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
}: {
  data: PersonData;
  geo: BoardGeo;
  rootProjects: RootProjectOption[];
}) {
  const { t } = useTranslation();
  const { styles } = useStyles();
  const ref = useRef<HTMLDivElement | null>(null);
  const [drag, setDrag] = useState<{ x0: number; x1: number } | null>(null);
  const [pending, setPending] = useState<PendingRange | null>(null);

  const snappedISO = (x: number) => snapISO(isoAtX(geo, x), geo.bucket);

  const onDown = (e: React.MouseEvent) => {
    if (pending) return;
    const rect = ref.current?.getBoundingClientRect();
    if (!rect) return;
    const x = e.clientX - rect.left;
    setDrag({ x0: x, x1: x });
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
    if (b - a > 10) {
      const from = snappedISO(a);
      const toExcl = snappedISO(b);
      if (from < toExcl) setPending({ from, toExcl, anchorX: geo.xPx(toExcl) });
    }
    setDrag(null);
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
            onClose={() => setPending(null)}
          />
        </div>
      )}
    </div>
  );
}
