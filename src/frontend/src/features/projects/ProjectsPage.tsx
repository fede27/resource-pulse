import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Alert, Button, Empty, Skeleton, Spin } from 'antd';
import { InfoCircleOutlined, PlusOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import { useTranslation } from 'react-i18next';
import type { Grain } from '@/components/timeline';
import { PageHeader } from '@/components/domain/PageHeader';
import {
  defaultFilters,
  filterProjects,
  lifecycleOf,
  portfolioHealth,
  projectRowHeight,
  projectsExtent,
  projectVerdict,
  sortProjects,
  allRoles,
  type BoardFilters,
  type BoardProject,
  type InspectTarget,
  type Verdict,
} from './boardModel';
import { BoardTimeline, buildGeo, RowGap, useWindowedRows } from '@/components/board';
import { useProjectsBoard, type BoardDomain } from './useProjectsBoard';
import { BoardInspector } from './BoardInspector';
import { NewProjectPanel } from './NewProjectPanel';
import { ProjectReasonModal } from './ProjectReasonModal';
import { LaneActionModal } from './LaneActionModal';
import { useCreateProject } from './useCreateProject';
import { useProjectActions } from './useProjectActions';
import { useLaneActions } from './useLaneActions';
import { BoardLegend } from './BoardLegend';
import { BoardToolbar, type Metric } from './BoardToolbar';
import { HealthCards } from './HealthCards';
import { ProjectRow } from './ProjectRow';
import { useStyles } from './ProjectsPage.styles';

const ISO = 'YYYY-MM-DD';
const MAX_DOMAIN_DAYS = 366; // keep the visual domain within the API range cap

function clampDomain(d: BoardDomain): BoardDomain {
  const min = dayjs(d.minISO);
  const max = dayjs(d.maxISO);
  if (max.isBefore(min)) return { minISO: d.minISO, maxISO: d.minISO };
  if (max.diff(min, 'day') + 1 <= MAX_DOMAIN_DAYS) return d;
  return { minISO: d.minISO, maxISO: min.add(MAX_DOMAIN_DAYS - 1, 'day').format(ISO) };
}

// Stable fallback: an inline object would defeat ProjectRow's memo on every render.
const FALLBACK_VERDICT = { verdict: 'sustainable', reason: null } as const;

function withMargin(ext: { minISO: string; maxISO: string }, days = 10): BoardDomain {
  return {
    minISO: dayjs(ext.minISO).subtract(days, 'day').format(ISO),
    maxISO: dayjs(ext.maxISO).add(days, 'day').format(ISO),
  };
}

export function ProjectsPage() {
  const { t } = useTranslation();
  const { styles } = useStyles();

  const todayISO = dayjs().format(ISO);
  // Initial horizon: a little context behind today, ~5 months ahead (the
  // prototype's default); "Adatta" re-fits to the loaded projects.
  const initialDomain = useMemo<BoardDomain>(
    () => ({
      minISO: dayjs(todayISO).subtract(3, 'week').format(ISO),
      maxISO: dayjs(todayISO).add(5, 'month').format(ISO),
    }),
    [todayISO],
  );

  const [pickedDomain, setPickedDomain] = useState<BoardDomain | null>(null);
  const domain = pickedDomain ?? initialDomain;
  const setDomain = (d: BoardDomain) => setPickedDomain(clampDomain(d));

  const board = useProjectsBoard(domain);

  const [pickedBucket, setPickedBucket] = useState<Grain | null>(null);
  const bucket = pickedBucket ?? board.primaryGrain;

  const [metric, setMetric] = useState<Metric>('pct');
  const [filters, setFilters] = useState<BoardFilters>(defaultFilters);
  const [expanded, setExpanded] = useState<Set<string>>(new Set());
  const [inspect, setInspect] = useState<InspectTarget | null>(null);
  const [panelOpen, setPanelOpen] = useState(false);
  const { submit: submitNewProject, saving: creatingProject } = useCreateProject(() =>
    setPanelOpen(false),
  );
  const actions = useProjectActions();
  const laneActions = useLaneActions();

  const scrollRef = useRef<HTMLDivElement | null>(null);
  const [scrollNonce, setScrollNonce] = useState(0);

  const geo = useMemo(
    () => buildGeo(domain.minISO, domain.maxISO, bucket, board.fence),
    [domain.minISO, domain.maxISO, bucket, board.fence],
  );

  // Scroll to today on mount and when the bucket/domain jump changes.
  useEffect(() => {
    const el = scrollRef.current;
    if (!el) return;
    if (geo.todayIn) el.scrollLeft = Math.max(0, geo.todayX - 160);
    // eslint-disable-next-line react-hooks/exhaustive-deps -- re-run on explicit nonce bumps only
  }, [scrollNonce, geo.bucket]);

  const verdicts = useMemo(() => {
    const map = new Map<string, { verdict: Verdict; reason: ReturnType<typeof projectVerdict>['reason'] }>();
    for (const p of board.projects) {
      map.set(p.id, projectVerdict(p, board.peakByPerson, board.overloadThreshold));
    }
    return map;
  }, [board.projects, board.peakByPerson, board.overloadThreshold]);

  const verdictOf = (p: BoardProject): Verdict => verdicts.get(p.id)?.verdict ?? 'sustainable';

  const visible = useMemo(
    () =>
      sortProjects(
        filterProjects(board.projects, filters, {
          verdictOf,
          me: board.me,
          todayISO: board.todayISO,
          domain,
        }),
        filters.sort,
        verdictOf,
      ),
    // eslint-disable-next-line react-hooks/exhaustive-deps -- verdictOf is derived from `verdicts`
    [board.projects, filters, verdicts, board.me, board.todayISO, domain],
  );

  const health = useMemo(
    () => portfolioHealth(board.projects, verdictOf, board.peakByPerson, board.overloadThreshold),
    // eslint-disable-next-line react-hooks/exhaustive-deps -- verdictOf is derived from `verdicts`
    [board.projects, verdicts, board.peakByPerson, board.overloadThreshold],
  );

  const roles = useMemo(() => allRoles(board.projects), [board.projects]);

  const onToday = () => {
    if (!(todayISO >= domain.minISO && todayISO <= domain.maxISO)) {
      const y = dayjs(todayISO).year();
      setDomain({ minISO: `${y}-01-01`, maxISO: `${y}-12-31` });
    }
    setScrollNonce((n) => n + 1);
  };

  const onFit = () => {
    const src = visible.length ? visible : board.projects;
    const nonClosed = src.filter((p) => lifecycleOf(p, todayISO) !== 'closed');
    setDomain(withMargin(projectsExtent(nonClosed.length ? nonClosed : src, domain), 7));
    if (scrollRef.current) scrollRef.current.scrollLeft = 0;
  };

  // Stable identity: ProjectRow is memoized, an inline closure per row would
  // defeat it on every page render.
  const toggleExpand = useCallback(
    (id: string) =>
      setExpanded((prev) => {
        const next = new Set(prev);
        if (next.has(id)) next.delete(id);
        else next.add(id);
        return next;
      }),
    [],
  );

  // Vertical windowing (perf + reachability): rows only RENDER inside the
  // scroll viewport (+overscan); heights are derived from state, so the slice
  // is pure. Filters/health/inspector keep computing on the full list.
  const rowItems = useMemo(
    () =>
      visible.map((p, i) => ({
        key: p.id,
        height: projectRowHeight(p, expanded.has(p.id)),
        project: p,
        alt: i % 2 === 1,
      })),
    [visible, expanded],
  );
  const { segments } = useWindowedRows(scrollRef, rowItems);

  if (board.isLoading) return <Skeleton active paragraph={{ rows: 8 }} />;

  if (board.isError) {
    return <Alert type="error" showIcon message={t('projects.loadError')} />;
  }

  return (
    <div>
      <PageHeader
        title={t('projects.sectionTitle')}
        subtitle={t('projects.sectionSubtitle', { sustainable: health.sustainable, total: health.total })}
        actions={
          <Button type="primary" icon={<PlusOutlined />} onClick={() => setPanelOpen(true)}>
            {t('projects.newProject.action')}
          </Button>
        }
      />

      <HealthCards health={health} overloadThreshold={board.overloadThreshold} />

      <BoardToolbar
        metric={metric}
        onMetricChange={setMetric}
        bucket={bucket}
        onBucketChange={(b) => {
          setPickedBucket(b);
          setScrollNonce((n) => n + 1);
        }}
        domain={domain}
        onDomainChange={setDomain}
        onToday={onToday}
        onFit={onFit}
        filters={filters}
        onFiltersChange={setFilters}
        personPool={board.personPool}
        roles={roles}
        resultCount={visible.length}
        totalCount={board.projects.length}
      />

      <BoardLegend overloadThreshold={board.overloadThreshold} />

      <BoardTimeline
        geo={geo}
        scrollRef={scrollRef}
        headerTitle={t('projects.timeline.header')}
        isEmpty={visible.length === 0}
        emptyContent={
          <Empty
            description={
              <>
                <div>{t('projects.empty.title')}</div>
                <div>{t('projects.empty.description')}</div>
              </>
            }
          >
            <Button onClick={() => setFilters(defaultFilters())}>{t('projects.empty.action')}</Button>
          </Empty>
        }
      >
        {segments.map((s) =>
          s.kind === 'gap' ? (
            <RowGap key={s.key} height={s.height} />
          ) : (
            <ProjectRow
              key={s.item.key}
              project={s.item.project}
              geo={geo}
              metric={metric}
              verdict={verdicts.get(s.item.project.id) ?? FALLBACK_VERDICT}
              expanded={expanded.has(s.item.project.id)}
              alt={s.item.alt}
              onToggle={toggleExpand}
              onInspect={setInspect}
              onAction={actions.run}
              onLaneAction={laneActions.run}
              peakByPerson={board.peakByPerson}
              overloadThreshold={board.overloadThreshold}
              blockHoursOf={board.blockHoursOf}
            />
          ),
        )}
      </BoardTimeline>

      <div className={styles.footnote}>
        <InfoCircleOutlined />
        {t('projects.footnote')}
        {board.isFetching && (
          <span className={styles.fetchingHint}>
            <Spin size="small" />
          </span>
        )}
      </div>

      <BoardInspector
        target={inspect}
        onClose={() => setInspect(null)}
        onAction={actions.run}
        projects={board.projects}
        bands={board.bands}
        overloadThreshold={board.overloadThreshold}
        todayISO={board.todayISO}
        profileByPerson={board.profileByPerson}
        peakByPerson={board.peakByPerson}
        blockHoursOf={board.blockHoursOf}
      />

      <NewProjectPanel
        open={panelOpen}
        saving={creatingProject}
        onClose={() => setPanelOpen(false)}
        onSubmit={(values) => void submitNewProject(values)}
        personPool={board.personPool}
        defaultOwnerId={board.me.resourceId}
      />

      <ProjectReasonModal
        state={actions.reasonModal}
        submitting={actions.reasonSubmitting}
        onSubmit={(reason) => void actions.onReasonSubmit(reason)}
        onCancel={actions.onReasonCancel}
      />

      <LaneActionModal
        state={laneActions.laneModal}
        submitting={laneActions.laneSubmitting}
        personPool={board.personPool}
        onReassign={laneActions.onReassignSubmit}
        onRetarget={laneActions.onRetargetSubmit}
        onCover={laneActions.onCoverSubmit}
        onEditDemand={laneActions.onEditDemandSubmit}
        onCancel={laneActions.closeModal}
      />
    </div>
  );
}
