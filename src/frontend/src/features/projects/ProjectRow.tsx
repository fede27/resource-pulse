import { memo } from 'react';
import { CheckOutlined, RightOutlined, SwapOutlined, UserOutlined, WarningOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { alpha, blue, neutral, red, text } from '@/app/palette';
import { InitialsAvatar } from '@/components/domain/InitialsAvatar';
import {
  statusChipKey,
  type AtRiskReason,
  type BoardProject,
  type CoverageBlock,
  type DemandRow,
  type InspectTarget,
  type LaneAction,
  type ProjectAction,
  type Verdict,
} from './boardModel';
import { ProjectActionsMenu } from './ProjectActionsMenu';
import { LaneActionsMenu } from './LaneActionsMenu';
import {
  BLOCK_ACCENT,
  BLOCK_ACCENT_SOFT,
  BLOCK_BORDER,
  BLOCK_HARD_BG,
  BLOCK_TEXT,
  CRITICAL_DOT,
  HOLE_ACCENT,
  HOLE_BG,
  HOLE_TEXT,
  MISMATCH_BG,
  MISMATCH_BORDER,
  MISMATCH_TEXT,
  OVER_ALLOC_TEXT,
  STATUS_CHIP_COLORS,
  VERDICT_COLORS,
} from './boardColors';
import type { BoardGeo } from '@/components/board';
import type { Metric } from './BoardToolbar';
import { PROPOSED_HATCH, TENTATIVE_HATCH, useStyles } from './ProjectRow.styles';

export type ProjectRowProps = {
  project: BoardProject;
  geo: BoardGeo;
  metric: Metric;
  verdict: { verdict: Verdict; reason: AtRiskReason };
  expanded: boolean;
  alt: boolean;
  // Keyed by project id so the page can pass ONE stable callback to every row
  // (React.memo needs referentially stable props to skip re-renders).
  onToggle: (projectId: string) => void;
  onInspect: (target: InspectTarget) => void;
  onAction: (project: BoardProject, action: ProjectAction) => void;
  onLaneAction: (action: LaneAction) => void;
  peakByPerson: (resourceId: string) => number;
  overloadThreshold: number;
  // Range-scoped hours of a block (% × capacity, consolidation P3); null while
  // the batch capacity read is still streaming in.
  blockHoursOf: (block: CoverageBlock) => number | null;
};

const round = (n: number) => Math.round(n);

// One project on the board: the envelope row (aggregated) plus, when expanded,
// one lane per coverage block and one lane per uncovered demand. Memoized:
// with vertical windowing the page re-renders on every scroll step — unrelated
// state (query, inspector, other rows' expansion) must not re-render rows.
export const ProjectRow = memo(function ProjectRow(props: ProjectRowProps) {
  const { project, geo, expanded, alt } = props;
  const { t } = useTranslation();
  const { styles, cx } = useStyles();
  const vc = VERDICT_COLORS[props.verdict.verdict];

  const lanes: Array<{ type: 'block'; block: CoverageBlock } | { type: 'hole'; demand: DemandRow }> = [
    ...project.demands.flatMap((d) => d.coverage.map((block) => ({ type: 'block' as const, block }))),
    ...project.holes.map((demand) => ({ type: 'hole' as const, demand })),
  ];

  const holesLabel =
    project.holes.length === 1
      ? t('projects.row.holeOne')
      : t('projects.row.holesMany', { count: project.holes.length });
  const peopleLabel =
    project.people.length === 1
      ? t('projects.row.personOne')
      : t('projects.row.peopleMany', { count: project.people.length });
  const verdictNote =
    props.verdict.verdict === 'uncovered'
      ? holesLabel
      : props.verdict.verdict === 'atRisk'
        ? t(`projects.reason.${props.verdict.reason ?? 'overload'}`)
        : peopleLabel;

  const VerdictIcon =
    props.verdict.verdict === 'uncovered'
      ? UserOutlined
      : props.verdict.verdict === 'atRisk'
        ? WarningOutlined
        : CheckOutlined;

  const chip = statusChipKey(project.status);

  return (
    <div className={styles.block}>
      <div className={cx(styles.envelopeRow, alt && styles.envelopeRowAlt)}>
        <div className={styles.labelCell}>
          {/* dynamic: verdict stripe colour. */}
          <div className={styles.stripe} style={{ background: vc.stripe }} />
          <div className={styles.labelBody}>
            <span
              className={cx(styles.chevron, expanded && styles.chevronOpen)}
              role="button"
              aria-label={t('projects.row.toggle')}
              onClick={() => props.onToggle(project.id)}
            >
              <RightOutlined style={{ fontSize: 12 }} />
            </span>
            <div className={styles.labelMain} onClick={() => props.onInspect({ kind: 'project', project })}>
              <div className={styles.nameLine}>
                <span className={styles.projectName}>{project.name}</span>
                <span className={cx(styles.provChip, project.proposed && styles.provChipProposed)}>
                  {t(`projects.provenance.${project.proposed ? 'proposed' : 'committed'}`)}
                </span>
                {chip && (
                  // dynamic: status chip colours from the semantic palette.
                  <span
                    className={styles.provChip}
                    style={{
                      color: STATUS_CHIP_COLORS[chip].color,
                      borderColor: STATUS_CHIP_COLORS[chip].border,
                      background: STATUS_CHIP_COLORS[chip].bg,
                    }}
                  >
                    {t(`projects.status.${chip}`)}
                  </span>
                )}
                {project.critical && (
                  // dynamic: critical marker colour.
                  <span title={t('projects.row.critical')} className={styles.criticalDot} style={{ background: CRITICAL_DOT }} />
                )}
              </div>
              <div className={styles.verdictLine}>
                {/* dynamic: verdict badge colours. */}
                <span
                  className={styles.verdictBadge}
                  style={{ background: vc.bg, border: `1px solid ${vc.border}`, color: vc.color }}
                >
                  <VerdictIcon style={{ fontSize: 10 }} />
                  {t(`projects.verdict.${props.verdict.verdict}`)}
                </span>
                <span className={styles.verdictNote}>{verdictNote}</span>
              </div>
            </div>
            <ProjectActionsMenu project={project} onAction={props.onAction} />
          </div>
        </div>
        {/* dynamic: axis width computed from the domain. */}
        <div className={styles.axisCell} style={{ width: geo.contentW }}>
          <Envelope {...props} />
        </div>
      </div>

      {expanded && lanes.length > 0 && (
        <div className={styles.lanes}>
          {lanes.map((lane, i) =>
            lane.type === 'block' ? (
              <BlockLane key={`b-${lane.block.id}-${i}`} {...props} block={lane.block} />
            ) : (
              <HoleLane key={`h-${lane.demand.demandId}-${i}`} {...props} demand={lane.demand} />
            ),
          )}
        </div>
      )}
    </div>
  );
});

// ── Envelope (aggregated bar with phases / totals) ────────────────────────

function Envelope({ project, geo, metric, onInspect }: ProjectRowProps) {
  const { t } = useTranslation();
  const { styles } = useStyles();
  if (!project.from || !project.to) return null;

  const left = geo.xPx(project.from);
  const width = geo.wPxInclusive(project.from, project.to);
  const proposed = project.proposed;
  const { requiredH, usefulH, gapH } = project.totals;

  return (
    // dynamic: bar geometry + status-driven colours.
    <div
      className={styles.envelope}
      style={{
        left,
        width,
        background: proposed ? PROPOSED_HATCH : alpha(blue[5], 0.07),
        border: proposed
          ? `1px dashed ${neutral.disabled}`
          : `1px solid ${project.critical ? red[1] : BLOCK_BORDER}`,
        borderLeft: `3px solid ${proposed ? neutral.icon : project.critical ? CRITICAL_DOT : BLOCK_ACCENT}`,
      }}
      onClick={() => onInspect({ kind: 'project', project })}
      title={project.client ? `${project.name} · ${project.client}` : project.name}
    >
      {project.phases.map((ph, i) => {
        const phL = geo.xPx(ph.from) - left;
        const phW = geo.wPxInclusive(ph.from, ph.to);
        return (
          // dynamic: phase segment geometry.
          <div
            key={ph.id || i}
            className={styles.phaseSeg}
            style={{
              left: phL,
              width: phW,
              borderRight: i < project.phases.length - 1 ? `1px dashed ${alpha(blue[5], 0.35)}` : 'none',
            }}
          >
            {/* dynamic: phase label colour follows the block axis. */}
            <span style={{ color: BLOCK_TEXT }}>{ph.label}</span>
          </div>
        );
      })}
      {project.phases.length === 0 && (
        <div className={styles.envelopeMeta}>
          {metric === 'hours' ? (
            // dynamic: text colour follows provenance.
            <span style={{ color: proposed ? text.tertiary : BLOCK_TEXT }}>
              {requiredH ? (
                <>
                  {round(usefulH)}h / {round(requiredH)}h
                  {gapH > 0 && (
                    <span style={{ color: HOLE_TEXT, fontWeight: 600 }}>
                      {' '}
                      · {t('projects.row.hoursGap', { gap: round(gapH) })}
                    </span>
                  )}
                </>
              ) : (
                t('projects.row.bestEffort')
              )}
            </span>
          ) : (
            <span style={{ color: proposed ? text.tertiary : BLOCK_TEXT }}>
              {proposed
                ? t('projects.provenance.proposed')
                : project.people.length === 1
                  ? t('projects.row.personOne')
                  : t('projects.row.peopleMany', { count: project.people.length })}
            </span>
          )}
        </div>
      )}
    </div>
  );
}

// ── Person lane (label + coverage bar) ────────────────────────────────────

function BlockLane(props: ProjectRowProps & { block: CoverageBlock }) {
  const { block, geo, project } = props;
  const { t } = useTranslation();
  const { styles, cx } = useStyles();
  const vc = VERDICT_COLORS[props.verdict.verdict];
  const peak = props.peakByPerson(block.resourceId);
  const conflict = peak >= props.overloadThreshold;
  const inspect = () => props.onInspect({ kind: 'person', project, resourceId: block.resourceId, block });

  return (
    <div className={styles.lane}>
      <div className={cx(styles.labelCell, styles.labelCellLane)}>
        {/* dynamic: verdict stripe colour, softened on lanes. */}
        <div className={cx(styles.stripe, styles.stripeSoft)} style={{ background: vc.stripe }} />
        <div className={cx(styles.laneBody, styles.laneClickable)} onClick={inspect}>
          <InitialsAvatar name={block.resourceName} size={26} />
          <div className={styles.laneText}>
            <div className={styles.laneName}>{block.resourceName}</div>
            <div className={styles.laneSub}>
              <span>{block.demandRoleName || block.resourceRoleName || '—'}</span>
              {block.mismatch && (
                // dynamic: mismatch tag colours.
                <span
                  className={styles.mismatchTag}
                  style={{ color: MISMATCH_TEXT, background: MISMATCH_BG, border: `1px solid ${MISMATCH_BORDER}` }}
                  title={t('projects.lane.mismatchTitle', {
                    personRole: block.resourceRoleName ?? '—',
                    demandRole: block.demandRoleName,
                  })}
                >
                  <SwapOutlined style={{ fontSize: 8 }} /> {t('projects.lane.mismatchTag')}
                </span>
              )}
            </div>
          </div>
          {conflict && (
            // dynamic: conflict flag colour.
            <div className={styles.conflictFlag} style={{ color: OVER_ALLOC_TEXT }}>
              <span>
                <WarningOutlined style={{ fontSize: 10 }} /> {t('projects.lane.peak', { peak: round(peak) })}
              </span>
              <span>{t('projects.lane.contends')}</span>
            </div>
          )}
        </div>
        <div className={styles.laneKebab}>
          <LaneActionsMenu target={{ kind: 'person', block, project }} onAction={props.onLaneAction} />
        </div>
      </div>
      {/* dynamic: axis width computed from the domain. */}
      <div className={styles.axisCell} style={{ width: geo.contentW }}>
        <CoverageBar {...props} onClick={inspect} />
      </div>
    </div>
  );
}

function CoverageBar({
  block,
  geo,
  metric,
  blockHoursOf,
  onClick,
}: ProjectRowProps & { block: CoverageBlock; onClick: () => void }) {
  const { t } = useTranslation();
  const { styles } = useStyles();
  const left = geo.xPx(block.from);
  const width = geo.wPxInclusive(block.from, block.to);
  const tentative = !block.hard;

  // Per-block hours are derived client-side (% × capacity over the fetched
  // range, ADR-0026 / consolidation P3) — range-scoped like the coverage
  // reconciliation, no per-block sidecar call.
  const rawHours = metric === 'hours' ? blockHoursOf(block) : null;
  const hours = rawHours !== null ? round(rawHours) : null;

  return (
    // dynamic: bar geometry + hard/tentative styling.
    <div
      className={styles.bar}
      style={{
        left,
        width,
        background: tentative ? TENTATIVE_HATCH : BLOCK_HARD_BG,
        border: tentative ? `1px dashed ${BLOCK_BORDER}` : `1px solid ${BLOCK_BORDER}`,
        borderLeft: `3px solid ${tentative ? BLOCK_ACCENT_SOFT : BLOCK_ACCENT}`,
        opacity: tentative ? 0.92 : 1,
      }}
      onClick={onClick}
      title={`${block.resourceName} · ${block.percent}% · ${t(
        tentative ? 'projects.bar.tentative' : 'projects.bar.hard',
      )}`}
    >
      {/* dynamic: text colour follows the block axis. */}
      <span className={styles.barPct} style={{ color: BLOCK_TEXT }}>
        {metric === 'hours' ? (hours !== null ? `${hours}h` : '…') : `${block.percent}%`}
      </span>
      {tentative && width > 78 && (
        <span className={styles.barNote} style={{ color: BLOCK_TEXT }}>
          {t('projects.bar.tentative')}
        </span>
      )}
    </div>
  );
}

// ── Uncovered-demand lane (the HERO state: declared open role) ────────────
// The uncovered demand is SCALAR over the queried range (backend Decision 4):
// it has no span of its own, so the indicator stretches over the project's
// window and the hours label reads "uncovered in range" — never a
// reconstructed hole span.

function HoleLane(props: ProjectRowProps & { demand: DemandRow }) {
  const { demand, geo, project } = props;
  const { t } = useTranslation();
  const { styles, cx } = useStyles();
  const vc = VERDICT_COLORS[props.verdict.verdict];
  const inspect = () => props.onInspect({ kind: 'hole', project, demand });

  return (
    <div className={styles.lane}>
      <div className={cx(styles.labelCell, styles.labelCellLane)}>
        {/* dynamic: verdict stripe colour, softened on lanes. */}
        <div className={cx(styles.stripe, styles.stripeSoft)} style={{ background: vc.stripe }} />
        <div className={cx(styles.laneBody, styles.laneClickable)} onClick={inspect}>
          {/* dynamic: ghost avatar carries the hole accent. */}
          <span className={styles.ghostAvatar} style={{ border: `1.5px dashed ${HOLE_ACCENT}`, color: HOLE_ACCENT }}>
            <UserOutlined style={{ fontSize: 13 }} />
          </span>
          <div className={styles.laneText}>
            {/* dynamic: hole text colour. */}
            <div className={styles.laneName} style={{ color: HOLE_TEXT, fontWeight: 600 }}>
              {demand.roleName}
            </div>
            <div className={styles.laneSub} style={{ color: HOLE_ACCENT }}>
              <span>
                {t('projects.lane.holeOwner', { owner: demand.ownerName ?? t('projects.lane.ownerFallback') })}
              </span>
            </div>
          </div>
        </div>
        <div className={styles.laneKebab}>
          <LaneActionsMenu target={{ kind: 'hole', demand, project }} onAction={props.onLaneAction} />
        </div>
      </div>
      {/* dynamic: axis width computed from the domain. */}
      <div className={styles.axisCell} style={{ width: geo.contentW }}>
        {project.from && project.to && (
          // dynamic: indicator spans the project window (scalar hole, no own span).
          <div
            className={styles.bar}
            style={{
              left: geo.xPx(project.from),
              width: geo.wPxInclusive(project.from, project.to),
              background: `repeating-linear-gradient(135deg, ${HOLE_BG} 0 6px, ${neutral.white} 6px 12px)`,
              border: `1.5px dashed ${HOLE_ACCENT}`,
            }}
            onClick={inspect}
            title={
              demand.gapH !== null
                ? t('projects.bar.holeHours', { hours: round(demand.gapH) })
                : t('projects.bar.holeTail')
            }
          >
            <UserOutlined className={styles.holeIcon} style={{ color: HOLE_ACCENT, fontSize: 13 }} />
            {/* dynamic: hole text colour. */}
            <span className={styles.holeRole} style={{ color: HOLE_TEXT }}>
              {demand.roleName}
            </span>
            <span className={styles.holeNote} style={{ color: HOLE_ACCENT }}>
              {props.metric === 'hours' && demand.gapH !== null
                ? t('projects.bar.holeHours', { hours: round(demand.gapH) })
                : t('projects.bar.holeTail')}
            </span>
          </div>
        )}
      </div>
    </div>
  );
}
