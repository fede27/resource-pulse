import { useState } from 'react';
import { Tabs } from 'antd';
import { SwapOutlined, UserOutlined, WarningOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import { useTranslation } from 'react-i18next';
import type { LoadSegmentDto } from '@/api/generated/schemas';
import { gold, neutral, orange, text } from '@/app/palette';
import { InitialsAvatar } from '@/components/domain/InitialsAvatar';
import { InspectorDrawer } from '@/components/domain/InspectorDrawer';
import { bandLabelFor, loadColor, type LoadBand } from '@/lib/loadBands';
import type { BoardProject, CoverageBlock, DemandRow, InspectTarget, ProjectAction } from './boardModel';
import { tentativeNotesOf } from './boardModel';
import { ProjectActionsMenu } from './ProjectActionsMenu';
import {
  BLOCK_BORDER,
  BLOCK_TEXT,
  DEMAND_STATUS_COLORS,
  HOLE_ACCENT,
  HOLE_BG,
  HOLE_TEXT,
  MISMATCH_BG,
  MISMATCH_BORDER,
  MISMATCH_TEXT,
  OVER_ALLOC_TEXT,
} from './boardColors';
import { useStyles } from './BoardInspector.styles';

const round = (n: number) => Math.round(n);
const fmtShort = (iso: string) => dayjs(iso).format('D MMM');

export type BoardInspectorProps = {
  target: InspectTarget | null;
  onClose: () => void;
  onAction: (project: BoardProject, action: ProjectAction) => void;
  projects: BoardProject[];
  bands: LoadBand[];
  overloadThreshold: number;
  todayISO: string;
  profileByPerson: (resourceId: string) => LoadSegmentDto[];
  peakByPerson: (resourceId: string) => number;
  // Range-scoped hours of a block (% × capacity, consolidation P3); null while
  // the batch capacity read is still streaming in.
  blockHoursOf: (block: CoverageBlock) => number | null;
};

type Face = 'coverage' | 'utilization';

// On-demand explainability: two faces of the same data — Copertura (hours,
// demand reconciliation) and Utilizzo (percent, person load in time).
export function BoardInspector(props: BoardInspectorProps) {
  const { t } = useTranslation();
  const { target } = props;

  // Derived selection (repo convention): a new target resets the face — person
  // opens on utilization, else coverage — without a setState-in-effect.
  const targetKey = target ? `${target.kind}:${target.kind === 'person' ? target.resourceId : target.project.id}` : '';
  const [picked, setPicked] = useState<{ key: string; face: Face } | null>(null);
  const face: Face =
    picked && picked.key === targetKey
      ? picked.face
      : target?.kind === 'person'
        ? 'utilization'
        : 'coverage';
  const setFace = (f: Face) => setPicked({ key: targetKey, face: f });

  return (
    <InspectorDrawer
      open={!!target}
      onClose={props.onClose}
      title={t('projects.inspector.title')}
      {...(target?.kind === 'project'
        ? { extra: <ProjectActionsMenu project={target.project} onAction={props.onAction} /> }
        : {})}
    >
      {target && (
        <Tabs
          activeKey={face}
          onChange={(k) => setFace(k as Face)}
          items={[
            {
              key: 'coverage',
              label: t('projects.inspector.faceCoverage'),
              children: <CoverageFace {...props} target={target} onSwitchFace={() => setFace('utilization')} />,
            },
            {
              key: 'utilization',
              label: t('projects.inspector.faceUtilization'),
              children: <UtilizationFace {...props} target={target} />,
            },
          ]}
        />
      )}
    </InspectorDrawer>
  );
}

// ── Face 1 — COPERTURA (ore): demand/coverage reconciliation ─────────────

function CoverageFace({
  target,
  onSwitchFace,
  blockHoursOf,
}: BoardInspectorProps & { target: InspectTarget; onSwitchFace: () => void }) {
  const { t } = useTranslation();
  const { styles } = useStyles();
  const { project } = target;

  if (target.kind === 'person') {
    const rows = project.demands.filter((d) => d.coverage.some((c) => c.resourceId === target.resourceId));
    const name = target.block.resourceName;
    return (
      <div>
        <div className={styles.personHeader}>
          <InitialsAvatar name={name} size={40} />
          <div>
            <h3 style={{ margin: 0, fontSize: 16, fontWeight: 600 }}>{name}</h3>
            <div style={{ fontSize: 13, color: text.tertiary }}>
              {target.block.resourceRoleName ?? '—'} · {project.name}
            </div>
          </div>
        </div>
        <div className={styles.sectionTitle}>{t('projects.inspector.coversHere')}</div>
        {rows.length ? (
          rows.map((d) => <DemandRowCard key={d.demandId} demand={d} blockHoursOf={blockHoursOf} />)
        ) : (
          <div className={styles.emptyNote}>{t('projects.inspector.noRows')}</div>
        )}
        <div className={styles.ruleBox}>
          {t('projects.inspector.coverageRulePerson')}{' '}
          <a onClick={onSwitchFace}>{t('projects.inspector.faceUtilization')}</a>.
        </div>
      </div>
    );
  }

  const { requiredH, usefulH, gapH, overH } = project.totals;
  return (
    <div>
      <div className={styles.header}>
        <h3>{project.name}</h3>
        <div>
          {project.client ? `${project.client} · ` : ''}
          {t('projects.inspector.demandInHours')}
        </div>
      </div>

      <div className={styles.miniRow}>
        <MiniHours label={t('projects.inspector.required')} value={requiredH ? `${round(requiredH)}h` : '—'} />
        <MiniHours
          label={t('projects.inspector.covered')}
          value={`${round(usefulH)}h`}
          color={DEMAND_STATUS_COLORS.covered.color}
        />
        <MiniHours
          label={t('projects.inspector.uncoveredLabel')}
          value={`${round(gapH)}h`}
          color={gapH > 0 ? HOLE_TEXT : text.tertiary}
        />
      </div>
      {overH > 0 && (
        <div className={styles.overWarning}>
          <WarningOutlined style={{ color: gold[6] }} />
          <span>{t('projects.inspector.overAlloc', { hours: round(overH) })}</span>
        </div>
      )}

      <div className={styles.sectionTitle}>
        {t('projects.inspector.demandRows', { count: project.demands.length })}
      </div>
      {project.demands.map((d) => (
        <DemandRowCard key={d.demandId} demand={d} blockHoursOf={blockHoursOf} />
      ))}

      <div className={styles.ruleBox}>{t('projects.inspector.coverageRule')}</div>
    </div>
  );
}

function MiniHours({ label, value, color }: { label: string; value: string; color?: string }) {
  const { styles } = useStyles();
  return (
    <div className={styles.miniCard}>
      <div>{label}</div>
      {/* dynamic: value colour resolved from live gap data. */}
      <div style={color ? { color } : undefined}>{value}</div>
    </div>
  );
}

// One role's reconciliation, hours-native. Over-allocation is flagged, never
// credited; best-effort shows consumption without a reference (no fake fill).
function DemandRowCard({
  demand: d,
  blockHoursOf,
}: {
  demand: DemandRow;
  blockHoursOf: (block: CoverageBlock) => number | null;
}) {
  const { t } = useTranslation();
  const { styles } = useStyles();
  const meta = DEMAND_STATUS_COLORS[d.status];
  const fillPct = d.requiredH !== null && d.requiredH > 0 ? Math.min(100, (d.coveredH / d.requiredH) * 100) : 100;

  return (
    <div className={styles.demandRow}>
      <div className={styles.demandHead}>
        <span>{d.roleName}</span>
        <span className={styles.demandBadges}>
          {d.inferred && (
            // dynamic: provenance badge colours (purple axis).
            <span
              className={styles.mismatchTag}
              style={{ color: MISMATCH_TEXT, background: MISMATCH_BG, border: `1px solid ${MISMATCH_BORDER}` }}
            >
              {t('projects.inspector.inferredBadge')}
            </span>
          )}
          {d.mismatch && <MismatchTag />}
          {/* dynamic: status chip colours from the demand-status palette. */}
          <span
            className={styles.statusChip}
            style={{ background: meta.track, border: `1px solid ${meta.bar}`, color: meta.color }}
          >
            {t(`projects.demandStatus.${d.status}`)}
          </span>
        </span>
      </div>

      <div className={styles.numbersLine}>
        {d.requiredH !== null ? (
          <>
            <strong>{round(d.requiredH)}h</strong> {t('projects.inspector.requestedWord')} ·{' '}
            <span style={{ color: text.primary }}>
              {round(d.coveredH)}h {t('projects.inspector.allocatedWord')}
            </span>
            {d.gapH !== null && d.gapH > 0 && (
              <>
                {' '}
                · <strong style={{ color: meta.color }}>{t('projects.inspector.gapWord', { gap: round(d.gapH) })}</strong>
              </>
            )}
            {d.overH > 0 && (
              <>
                {' '}
                · <strong style={{ color: OVER_ALLOC_TEXT }}>{t('projects.inspector.overWord', { over: round(d.overH) })}</strong>
              </>
            )}
            {d.gapH !== null && d.gapH <= 0 && d.overH === 0 && (
              <>
                {' '}
                · <span style={{ color: DEMAND_STATUS_COLORS.covered.color }}>{t('projects.inspector.exactWord')}</span>
              </>
            )}
          </>
        ) : (
          <>
            <strong>{t('projects.demandStatus.noTarget')}</strong> ·{' '}
            {t('projects.inspector.bestEffortLine', { covered: round(d.coveredH) })}
          </>
        )}
      </div>

      {/* dynamic: track/fill colours + fill width from live coverage data. */}
      <div className={styles.hoursBar} style={{ background: meta.track }}>
        {d.requiredH !== null ? (
          <>
            <div
              style={{ position: 'absolute', left: 0, top: 0, bottom: 0, width: `${fillPct}%`, background: meta.bar, borderRadius: 4 }}
            />
            {d.overH > 0 && (
              <div
                title={t('projects.inspector.overWord', { over: round(d.overH) })}
                style={{
                  position: 'absolute',
                  right: 0,
                  top: 0,
                  bottom: 0,
                  width: 16,
                  background: `repeating-linear-gradient(135deg, ${orange[6]} 0 3px, ${orange[1]} 3px 6px)`,
                }}
              />
            )}
          </>
        ) : (
          <div
            style={{
              position: 'absolute',
              inset: 0,
              background: `repeating-linear-gradient(135deg, ${meta.bar}66 0 6px, ${meta.bar}22 6px 12px)`,
            }}
          />
        )}
      </div>

      <div className={styles.coverageEntries}>
        {d.coverage.map((c) => (
          <CoverageEntry key={c.id} block={c} blockHoursOf={blockHoursOf} />
        ))}
        {d.uncovered && (
          <div className={styles.coverageEntry} style={{ color: HOLE_TEXT }}>
            {/* dynamic: ghost dot carries the hole accent. */}
            <span className={styles.ghostDot} style={{ border: `1.5px dashed ${HOLE_ACCENT}`, color: HOLE_ACCENT }}>
              <UserOutlined style={{ fontSize: 11 }} />
            </span>
            <span>
              {t('projects.inspector.uncoveredOwner', {
                owner: d.ownerName ?? t('projects.lane.ownerFallback'),
              })}
            </span>
          </div>
        )}
      </div>
    </div>
  );
}

function CoverageEntry({
  block: c,
  blockHoursOf,
}: {
  block: CoverageBlock;
  blockHoursOf: (block: CoverageBlock) => number | null;
}) {
  const { t } = useTranslation();
  const { styles } = useStyles();
  // Per-entry hours derived client-side (% × capacity over the fetched range,
  // ADR-0026 / consolidation P3) — no per-block sidecar call.
  const rawHours = blockHoursOf(c);
  const hours = rawHours !== null ? round(rawHours) : null;

  return (
    <div className={styles.coverageEntry}>
      <InitialsAvatar name={c.resourceName} size={20} />
      <span>{c.resourceName}</span>
      {c.mismatch && <MismatchTag />}
      {/* dynamic: block tag styling follows hard/tentative. */}
      <span
        className={styles.blockTag}
        style={{
          color: c.hard ? BLOCK_TEXT : text.tertiary,
          border: `1px ${c.hard ? 'solid' : 'dashed'} ${c.hard ? BLOCK_BORDER : neutral.border}`,
        }}
      >
        {t(c.hard ? 'projects.bar.hard' : 'projects.bar.tentative')}
      </span>
      <span className={styles.entryNumbers}>
        {c.percent}%{hours !== null ? ` · ${hours}h` : ''}
      </span>
    </div>
  );
}

function MismatchTag() {
  const { t } = useTranslation();
  const { styles } = useStyles();
  return (
    // dynamic: mismatch tag colours (purple axis).
    <span
      className={styles.mismatchTag}
      style={{ color: MISMATCH_TEXT, background: MISMATCH_BG, border: `1px solid ${MISMATCH_BORDER}` }}
      title={t('projects.legend.mismatch')}
    >
      <SwapOutlined style={{ fontSize: 9 }} /> {t('projects.lane.mismatchTag')}
    </span>
  );
}

// ── Face 2 — UTILIZZO (%): person load in time ───────────────────────────

function UtilizationFace(props: BoardInspectorProps & { target: InspectTarget }) {
  const { t } = useTranslation();
  const { styles } = useStyles();
  const { target } = props;

  if (target.kind === 'person') return <PersonUtilization {...props} target={target} />;

  if (target.kind === 'hole') {
    return (
      <div>
        <div className={styles.personHeader}>
          {/* dynamic: ghost avatar carries the hole accent. */}
          <span
            className={styles.ghostDot}
            style={{ width: 40, height: 40, border: `1.5px dashed ${HOLE_ACCENT}`, color: HOLE_ACCENT, background: HOLE_BG }}
          >
            <UserOutlined style={{ fontSize: 18 }} />
          </span>
          <div>
            <h3 style={{ margin: 0, fontSize: 16, fontWeight: 600, color: HOLE_TEXT }}>{target.demand.roleName}</h3>
            <div style={{ fontSize: 13, color: HOLE_ACCENT }}>{t('projects.inspector.holeNoUtil')}</div>
          </div>
        </div>
        <div className={styles.hint}>{t('projects.inspector.holeNoUtilBody')}</div>
      </div>
    );
  }

  // kind === 'project' — people on it + peaks.
  const { project } = target;
  return (
    <div>
      <div className={styles.header}>
        <h3>{project.name}</h3>
        <div>{t('projects.inspector.utilizationSubtitle')}</div>
      </div>
      <div className={styles.sectionTitle}>
        {t('projects.inspector.peopleTitle', { count: project.people.length })}
      </div>
      {project.people.map((id) => {
        const peak = props.peakByPerson(id);
        const color = loadColor(peak, props.bands);
        const name =
          project.demands.flatMap((d) => d.coverage).find((c) => c.resourceId === id)?.resourceName ?? '—';
        return (
          <div key={id} className={styles.personRow}>
            <InitialsAvatar name={name} size={24} />
            <span>{name}</span>
            {/* dynamic: pill colours resolved from live load bands. */}
            <span
              className={styles.peakPill}
              style={{ background: color.bg, border: `1px solid ${color.solid}`, color: color.fg }}
            >
              <span className={styles.pillDot} style={{ background: color.solid }} />
              {round(peak)}%
            </span>
          </div>
        );
      })}
      <div className={styles.ruleBox}>
        {t('projects.inspector.utilizationRule', { threshold: props.overloadThreshold })}
      </div>
    </div>
  );
}

function PersonUtilization(
  props: BoardInspectorProps & { target: Extract<InspectTarget, { kind: 'person' }> },
) {
  const { t } = useTranslation();
  const { styles } = useStyles();
  const { target, bands, todayISO } = props;
  const { block, project } = target;

  const profile = props.profileByPerson(target.resourceId);
  const peak = props.peakByPerson(target.resourceId);
  const peakColor = loadColor(peak, bands);
  const peakBand = bandLabelFor(peak, bands);
  const peakSeg = profile.find((s) => (s.percent ?? 0) === peak) ?? profile[0];
  const curSeg = profile.find((s) => s.from && s.to && s.from <= todayISO && todayISO <= s.to);
  const curPct = curSeg?.percent ?? 0;
  const curColor = loadColor(curPct, bands);
  const maxPct = Math.max(120, peak);
  const tentativeNotes = tentativeNotesOf(props.projects, target.resourceId);

  return (
    <div>
      <div className={styles.personHeader}>
        <InitialsAvatar name={block.resourceName} size={44} />
        <div>
          <h3 style={{ margin: 0, fontSize: 17, fontWeight: 600 }}>{block.resourceName}</h3>
          <div style={{ fontSize: 13, color: text.tertiary }}>{block.resourceRoleName ?? '—'}</div>
        </div>
      </div>

      <div className={styles.sectionTitle}>{t('projects.inspector.onProject')}</div>
      <FactRow label={t('projects.inspector.project')} value={project.name} />
      <FactRow label={t('projects.inspector.localQuota')} value={`${block.percent}%`} mono />
      <FactRow
        label={t('projects.inspector.blockStatus')}
        value={t(block.hard ? 'projects.inspector.hardLong' : 'projects.inspector.tentativeLong')}
      />
      <FactRow
        label={t('projects.inspector.projectProvenance')}
        value={t(`projects.provenance.${project.proposed ? 'proposed' : 'committed'}`)}
      />
      <FactRow
        label={t('projects.inspector.period')}
        value={`${fmtShort(block.from)} – ${fmtShort(block.to)}`}
        mono
      />

      <div className={styles.sectionTitle}>{t('projects.inspector.profileTitle')}</div>
      <div className={styles.hint}>{t('projects.inspector.profileHint')}</div>
      {profile.map((s, i) => {
        const pct = s.percent ?? 0;
        const c = loadColor(pct, bands);
        const isPeak = pct === peak && peak > 0;
        const isNow = curSeg && s.from === curSeg.from && s.to === curSeg.to;
        return (
          // dynamic: peak segment highlighted with its band colour.
          <div key={i} className={styles.segment} style={isPeak ? { background: c.bg } : undefined}>
            <div className={styles.segmentHead}>
              <span className={styles.segmentWhen}>
                {/* dynamic: band dot colour. */}
                <span className={styles.segmentDot} style={{ background: c.solid }} />
                {s.from ? fmtShort(s.from) : ''} – {s.to ? fmtShort(s.to) : ''}
                {isNow && <span className={styles.nowPill}>{t('projects.timeline.today')}</span>}
              </span>
              {/* dynamic: percent colour follows the band. */}
              <span className={styles.segmentPct} style={{ fontWeight: isPeak ? 700 : 500, color: c.fg }}>
                {round(pct)}%{isPeak ? ` · ${t('projects.inspector.peakWord')}` : ''}
              </span>
            </div>
            <div className={styles.segmentTrack}>
              {/* dynamic: mini-bar width/colour from live data. */}
              <div
                style={{
                  width: `${Math.min(100, (pct / maxPct) * 100)}%`,
                  height: '100%',
                  background: c.solid,
                  opacity: isPeak ? 1 : 0.55,
                }}
              />
            </div>
          </div>
        );
      })}
      {!profile.length && <div className={styles.emptyNote}>{t('projects.inspector.noProfile')}</div>}

      {peakSeg && (
        <>
          <div className={styles.sectionTitle}>{t('projects.inspector.compositionTitle')}</div>
          <div className={styles.hint}>
            {t('projects.inspector.peakPeriod')}{' '}
            <span style={{ fontVariantNumeric: 'tabular-nums' }}>
              {peakSeg.from ? fmtShort(peakSeg.from) : ''} – {peakSeg.to ? fmtShort(peakSeg.to) : ''}
            </span>
          </div>
          {(peakSeg.byProject ?? []).map((bp, i) => (
            <div key={i} className={styles.compRow}>
              <span>{bp.projectName || '—'}</span>
              <span>{round(bp.percent ?? 0)}%</span>
            </div>
          ))}
          {/* dynamic: total border colour follows the peak band. */}
          <div className={styles.compTotal} style={{ borderBottom: `2px solid ${peakColor.solid}` }}>
            <span>{t('projects.inspector.peakTotal')}</span>
            <span style={{ color: peakColor.fg }}>= {round(peak)}%</span>
          </div>
        </>
      )}
      {tentativeNotes.map((n, i) => (
        <div key={i} className={styles.tentativeNote}>
          {t('projects.inspector.tentativeNote', { pct: n.percent, project: n.project })}
        </div>
      ))}

      <div className={styles.sectionTitle}>{t('projects.inspector.stateTitle')}</div>
      <div className={styles.statusRow}>
        {/* dynamic: pill colours resolved from live load bands. */}
        <span
          className={styles.bigPill}
          style={{ background: peakColor.bg, border: `1px solid ${peakColor.solid}`, color: peakColor.fg }}
        >
          <span className={styles.pillDot} style={{ background: peakColor.solid }} />
          {t('projects.inspector.atPeak', { band: peakBand })}
        </span>
        <span className={styles.todayNote}>
          {t('projects.timeline.today')}:{' '}
          {/* dynamic: today's colour follows its band. */}
          <strong style={{ color: curColor.fg }}>
            {round(curPct)}% · {bandLabelFor(curPct, bands)}
          </strong>
        </span>
      </div>

      <div className={styles.ruleBox}>
        {t('projects.inspector.personRule', {
          peak: round(peak),
          local: block.percent,
          project: project.name,
          threshold: props.overloadThreshold,
        })}
      </div>
    </div>
  );
}

function FactRow({ label, value, mono }: { label: string; value: string; mono?: boolean }) {
  const { styles, cx } = useStyles();
  return (
    <div className={styles.factRow}>
      <span>{label}</span>
      <span className={cx(mono && styles.factMono)}>{value}</span>
    </div>
  );
}
