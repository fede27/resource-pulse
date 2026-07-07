import { useMemo, useState } from 'react';
import { Drawer, Tabs } from 'antd';
import dayjs from 'dayjs';
import { useTranslation } from 'react-i18next';
import { InitialsAvatar } from '@/components/domain/InitialsAvatar';
import type { BoardGeo } from '@/components/board';
import { bandLabelFor, loadColor, type LoadBand } from '@/lib/loadBands';
import {
  bucketComposition,
  subBuckets,
  type BoardBucket,
  type PersonData,
} from './peopleBoardModel';
import { projectHue } from './projectHue';
import type { InspectTarget } from './PersonBoardRow';
import { useStyles } from './PersonInspector.styles';

export type PersonInspectorProps = {
  target: InspectTarget | null;
  onClose: () => void;
  peopleById: ReadonlyMap<string, PersonData>;
  geo: BoardGeo;
  bands: LoadBand[];
  countTentative: boolean;
};

type Face = 'utilization' | 'coverage';

const round = (n: number) => Math.round(n);
const fmtPct = (n: number) => (Number.isFinite(n) ? `${round(n)}%` : '∞');

// Two faces of the same person: Utilizzo (bucket-average %, band state,
// per-project composition) and Copertura (hours toward demands — the other
// face of the Progetti page).
export function PersonInspector(props: PersonInspectorProps) {
  const { t } = useTranslation();
  const { target } = props;
  const data = target ? props.peopleById.get(target.personId) : undefined;

  // Derived selection (repo convention): a new target resets to Utilizzo
  // without a setState-in-effect.
  const targetKey = target ? `${target.kind}:${target.personId}` : '';
  const [picked, setPicked] = useState<{ key: string; face: Face } | null>(null);
  const face: Face = picked && picked.key === targetKey ? picked.face : 'utilization';
  const setFace = (f: Face) => setPicked({ key: targetKey, face: f });

  return (
    <Drawer
      open={!!target && !!data}
      onClose={props.onClose}
      width={440}
      title={t('peopleBoard.inspector.title')}
      destroyOnHidden
    >
      {target && data && (
        <>
          <PersonHeader data={data} />
          <Tabs
            activeKey={face}
            onChange={(k) => setFace(k as Face)}
            items={[
              {
                key: 'utilization',
                label: t('peopleBoard.inspector.faceUtilization'),
                children: <UtilizationFace {...props} target={target} data={data} />,
              },
              {
                key: 'coverage',
                label: t('peopleBoard.inspector.faceCoverage'),
                children: <CoverageFace {...props} data={data} />,
              },
            ]}
          />
        </>
      )}
    </Drawer>
  );
}

function PersonHeader({ data }: { data: PersonData }) {
  const { t } = useTranslation();
  const { styles } = useStyles();
  const p = data.person;
  const sub = [p.roleName, p.teamName, t('peopleBoard.row.capPerWeek', { hours: Math.round(data.weeklyCapH) })]
    .filter(Boolean)
    .join(' · ');
  return (
    <div className={styles.personHeader}>
      <InitialsAvatar name={p.name} size={40} />
      <div>
        <h3>{p.name}</h3>
        <div className={styles.personSub}>{sub}</div>
      </div>
    </div>
  );
}

// ── Face 1 — UTILIZZO (%): bucket average + composition + sub-grain ──────

function UtilizationFace({
  target,
  data,
  geo,
  bands,
  countTentative,
}: PersonInspectorProps & { target: InspectTarget; data: PersonData }) {
  const { t } = useTranslation();
  const { styles } = useStyles();

  const focus: BoardBucket = useMemo(() => {
    if (target.kind === 'cell') return target.bucket;
    return { from: geo.minISO, toExcl: geo.maxISO, x: 0, w: geo.contentW, label: t('peopleBoard.inspector.rangeTitle') };
  }, [target, geo, t]);

  const { total, byProject } = useMemo(
    () => bucketComposition(data, focus, countTentative),
    [data, focus, countTentative],
  );

  // Tentative blocks are always listed — greyed out when they don't count.
  const tentative = useMemo(
    () =>
      countTentative
        ? []
        : data.blocks.filter((b) => !b.hard && b.from < focus.toExcl && b.to >= focus.from),
    [countTentative, data.blocks, focus],
  );

  const c = loadColor(total.pct, bands);
  const bandLabel = Number.isFinite(total.pct)
    ? bandLabelFor(total.pct, bands)
    : (bands[bands.length - 1]?.label ?? '—');

  const sub = target.kind === 'cell' ? subBuckets(focus.from, focus.toExcl, geo.bucket) : [];
  const block = target.kind === 'block' ? target.block : null;

  return (
    <div>
      {block && (
        <>
          <div className={styles.sectionTitle}>{t('peopleBoard.inspector.blockTitle')}</div>
          <div className={styles.blockCard}>
            <div>
              <span>{t('peopleBoard.inspector.blockProject')}</span>
              <span>{block.projectName}</span>
            </div>
            <div>
              <span>{t('peopleBoard.inspector.blockPeriod')}</span>
              <span>
                {dayjs(block.from).format('D MMM')} → {dayjs(block.to).format('D MMM YYYY')} · {block.percent}%
              </span>
            </div>
            <div>
              <span>{t('peopleBoard.inspector.blockStatus')}</span>
              <span>{t(block.hard ? 'peopleBoard.inspector.hardLong' : 'peopleBoard.inspector.tentativeLong')}</span>
            </div>
            <div>
              <span>{t('peopleBoard.inspector.demandRole')}</span>
              <span>
                {block.demandRoleName || '—'}
                {block.mismatch && <em> · {t('peopleBoard.inspector.mismatch')}</em>}
              </span>
            </div>
          </div>
        </>
      )}

      <div className={styles.sectionTitle}>
        {target.kind === 'cell'
          ? t('peopleBoard.inspector.bucketTitle', { label: focus.label })
          : t('peopleBoard.inspector.rangeTitle')}
      </div>
      {/* dynamic: band colours resolved from live data. */}
      <div className={styles.bandBox} style={{ background: c.bg, border: `1px solid ${c.solid}` }}>
        <span className={styles.bandValue} style={{ color: c.fg }}>
          {fmtPct(total.pct)}
        </span>
        <div className={styles.bandMeta} style={{ color: c.fg }}>
          <div>{bandLabel}</div>
          <div>
            {focus.from} → {dayjs(focus.toExcl).subtract(1, 'day').format('YYYY-MM-DD')} ·{' '}
            {round(total.allocH)}h / {round(total.capH)}h
          </div>
        </div>
      </div>

      <div className={styles.sectionTitle}>
        {t('peopleBoard.inspector.composition', {
          mode: t(countTentative ? 'peopleBoard.inspector.modeAll' : 'peopleBoard.inspector.modeHard'),
        })}
      </div>
      {byProject.length === 0 && <div className={styles.emptyNote}>{t('peopleBoard.inspector.noBlocks')}</div>}
      {byProject.map((s) => {
        const hue = projectHue(s.rootProjectId);
        return (
          <div key={s.rootProjectId} className={styles.compRow}>
            {/* dynamic: per-project accent. */}
            <span className={styles.compDot} style={{ background: hue.accent }} />
            <span className={styles.compName}>
              {s.projectName}
              {s.allTentative && <em> · {t('peopleBoard.row.tentative')}</em>}
            </span>
            <span className={styles.compValue}>{fmtPct(s.pct)}</span>
          </div>
        );
      })}
      {byProject.length > 0 && (
        <div className={styles.totalRow}>
          <span>{t('peopleBoard.inspector.total')}</span>
          {/* dynamic: band text colour. */}
          <span style={{ color: c.fg }}>= {fmtPct(total.pct)}</span>
        </div>
      )}
      {tentative.length > 0 && (
        <div className={styles.tentativeNote}>
          {t('peopleBoard.inspector.tentativeNote', {
            list: tentative.map((b) => `${b.percent}% ${b.projectName}`).join(', '),
          })}
        </div>
      )}

      {sub.length > 1 && (
        <>
          <div className={styles.sectionTitle}>{t('peopleBoard.inspector.subDetail')}</div>
          {sub.map((s, i) => {
            const stat = bucketComposition(data, { ...s, x: 0, w: 0 }, countTentative).total;
            const sc = loadColor(stat.pct, bands);
            const fill = Number.isFinite(stat.pct) ? Math.min(100, stat.pct / 1.5) : 100;
            return (
              <div key={i} className={styles.subRow}>
                <span className={styles.subLabel}>{s.label}</span>
                <div className={styles.subTrack}>
                  {/* dynamic: fill width + band colour from live data. */}
                  <div className={styles.subFill} style={{ width: `${fill}%`, background: sc.solid }} />
                </div>
                {/* dynamic: band text colour. */}
                <span className={styles.subValue} style={{ color: sc.fg }}>
                  {fmtPct(stat.pct)}
                </span>
              </div>
            );
          })}
        </>
      )}

      <div className={styles.ruleBox}>
        {t('peopleBoard.inspector.utilizationRule', {
          tentMode: t(
            countTentative ? 'peopleBoard.inspector.utilizationRuleAll' : 'peopleBoard.inspector.utilizationRuleHard',
          ),
        })}
      </div>
    </div>
  );
}

// ── Face 2 — COPERTURA (ore): hours toward demands over the visible range ─

function CoverageFace({
  data,
  geo,
  countTentative,
}: PersonInspectorProps & { data: PersonData }) {
  const { t } = useTranslation();
  const { styles } = useStyles();

  const range: BoardBucket = useMemo(
    () => ({ from: geo.minISO, toExcl: geo.maxISO, x: 0, w: 0, label: '' }),
    [geo.minISO, geo.maxISO],
  );
  // Hours face always includes tentative rows (annotated) — hiding proposed
  // hours here would misstate what the person is committed toward.
  const { total, byProject } = useMemo(() => bucketComposition(data, range, true), [data, range]);
  void countTentative;

  return (
    <div>
      <div className={styles.sectionTitle}>{t('peopleBoard.inspector.coverageTitle')}</div>
      <div className={styles.coverageSubtitle}>
        {t('peopleBoard.inspector.coverageSubtitle', { name: data.person.name.split(' ')[0] ?? data.person.name })}
      </div>
      {byProject.map((s) => {
        const hue = projectHue(s.rootProjectId);
        const pctOfCap = total.capH > 0 ? Math.min(100, (s.hours / total.capH) * 100) : 0;
        return (
          <div key={s.rootProjectId} className={styles.hoursRow}>
            <div className={styles.hoursHead}>
              {/* dynamic: per-project accent. */}
              <span className={styles.compDot} style={{ background: hue.accent }} />
              <span className={styles.hoursName}>
                {s.projectName}
                {s.allTentative && <em> · {t('peopleBoard.row.tentative')}</em>}
              </span>
              <span className={styles.hoursValue}>{round(s.hours)}h</span>
            </div>
            <div className={styles.hoursTrack}>
              {/* dynamic: fill width + project colour from live data. */}
              <div
                className={styles.hoursFill}
                style={{ width: `${pctOfCap}%`, background: hue.accent, opacity: s.allTentative ? 0.4 : 0.8 }}
              />
            </div>
          </div>
        );
      })}
      {byProject.length === 0 && <div className={styles.emptyNote}>{t('peopleBoard.inspector.noBlocks')}</div>}
      <div className={styles.totalRow}>
        <span>{t('peopleBoard.inspector.coverageTotal')}</span>
        <span>{t('peopleBoard.inspector.coverageOf', { alloc: round(total.allocH), cap: round(total.capH) })}</span>
      </div>
      <div className={styles.ruleBox}>
        {t('peopleBoard.inspector.coverageRule', {
          cap: round(total.capH),
          weekly: Math.round(data.weeklyCapH),
        })}
      </div>
    </div>
  );
}
