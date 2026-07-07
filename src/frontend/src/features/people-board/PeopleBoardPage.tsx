import { useEffect, useMemo, useRef, useState } from 'react';
import { Alert, Button, Empty, Skeleton, Spin } from 'antd';
import { InfoCircleOutlined, TeamOutlined, UserOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import { useTranslation } from 'react-i18next';
import { BoardTimeline, buildGeo, type BoardDomain } from '@/components/board';
import { PageHeader } from '@/components/domain/PageHeader';
import { StatCard } from '@/components/domain/StatCard';
import type { Grain } from '@/components/timeline';
import { legendStops } from '@/lib/loadBands';
import {
  bucketsFromGeo,
  groupPeople,
  matchesBands,
  matchesQuery,
  peopleKpis,
  personStats,
  sortPeople,
  type GroupBy,
  type PeopleSort,
  type PersonStats,
} from './peopleBoardModel';
import { usePeopleBoard } from './usePeopleBoard';
import { PeopleBoardToolbar, type Metric } from './PeopleBoardToolbar';
import { PersonBoardRow, type InspectTarget } from './PersonBoardRow';
import { PersonInspector } from './PersonInspector';
import { useStyles } from './PeopleBoardPage.styles';

const ISO = 'YYYY-MM-DD';
const MAX_DOMAIN_DAYS = 366; // keep the visual domain within the API range cap

function clampDomain(d: BoardDomain): BoardDomain {
  const min = dayjs(d.minISO);
  const max = dayjs(d.maxISO);
  if (max.isBefore(min)) return { minISO: d.minISO, maxISO: d.minISO };
  if (max.diff(min, 'day') + 1 <= MAX_DOMAIN_DAYS) return d;
  return { minISO: d.minISO, maxISO: min.add(MAX_DOMAIN_DAYS - 1, 'day').format(ISO) };
}

// La pagina PERSONE: il pivot persone della timeline di copertura. Righe =
// persone (heatmap della media di bucket), corsie = i loro progetti, drag
// sulla capacità libera = proposta di copertura. Solo l'offerta — i buchi
// vivono su Progetti.
export function PeopleBoardPage() {
  const { t } = useTranslation();
  const { styles } = useStyles();

  const todayISO = dayjs().format(ISO);
  // Initial horizon: a little context behind today, ~3 months ahead (the
  // prototype's default); "Adatta" re-fits to the blocks' extent.
  const initialDomain = useMemo<BoardDomain>(
    () => ({
      minISO: dayjs(todayISO).subtract(2, 'week').format(ISO),
      maxISO: dayjs(todayISO).add(3, 'month').format(ISO),
    }),
    [todayISO],
  );

  const [pickedDomain, setPickedDomain] = useState<BoardDomain | null>(null);
  const domain = pickedDomain ?? initialDomain;
  const setDomain = (d: BoardDomain) => setPickedDomain(clampDomain(d));

  const board = usePeopleBoard(domain);

  const [pickedBucket, setPickedBucket] = useState<Grain | null>(null);
  const bucket = pickedBucket ?? board.primaryGrain;

  const [metric, setMetric] = useState<Metric>('pct');
  const [groupBy, setGroupBy] = useState<GroupBy>('role');
  const [query, setQuery] = useState('');
  const [bandSel, setBandSel] = useState<Set<number>>(new Set());
  const [countTent, setCountTent] = useState(false);
  const [sort, setSort] = useState<PeopleSort>('severity');
  const [expanded, setExpanded] = useState<Set<string>>(new Set());
  const [inspect, setInspect] = useState<InspectTarget | null>(null);

  const scrollRef = useRef<HTMLDivElement | null>(null);
  const [scrollNonce, setScrollNonce] = useState(0);

  const geo = useMemo(
    () => buildGeo(domain.minISO, domain.maxISO, bucket, board.fence),
    [domain.minISO, domain.maxISO, bucket, board.fence],
  );
  const buckets = useMemo(() => bucketsFromGeo(geo), [geo]);

  // Scroll to today on mount and when the bucket/domain jump changes.
  useEffect(() => {
    const el = scrollRef.current;
    if (!el) return;
    if (geo.todayIn) el.scrollLeft = Math.max(0, geo.todayX - 160);
    // eslint-disable-next-line react-hooks/exhaustive-deps -- re-run on explicit nonce bumps only
  }, [scrollNonce, geo.bucket]);

  const statsById = useMemo(() => {
    const map = new Map<string, PersonStats>();
    for (const d of board.people) map.set(d.person.id, personStats(d, buckets, countTent));
    return map;
  }, [board.people, buckets, countTent]);
  const statsOf = (id: string): PersonStats => statsById.get(id) ?? { peak: 0, min: 0 };

  const peopleById = useMemo(
    () => new Map(board.people.map((d) => [d.person.id, d])),
    [board.people],
  );

  const visible = useMemo(() => {
    const filtered = board.people.filter(
      (d) => matchesQuery(d.person, query) && matchesBands(d, buckets, bandSel, board.bands, countTent),
    );
    return sortPeople(filtered, sort, statsOf);
    // eslint-disable-next-line react-hooks/exhaustive-deps -- statsOf is derived from statsById
  }, [board.people, query, buckets, bandSel, board.bands, countTent, sort, statsById]);

  const groups = useMemo(() => groupPeople(visible, groupBy), [visible, groupBy]);

  const kpis = useMemo(
    () => peopleKpis(board.people, statsOf, board.bands),
    // eslint-disable-next-line react-hooks/exhaustive-deps -- statsOf is derived from statsById
    [board.people, statsById, board.bands],
  );

  const onToday = () => {
    if (!(todayISO >= domain.minISO && todayISO <= domain.maxISO)) {
      setDomain(initialDomain);
    }
    setScrollNonce((n) => n + 1);
  };

  const onFit = () => {
    // Fit to the extent of the visible people's blocks (with a margin).
    let min: string | null = null;
    let max: string | null = null;
    for (const d of visible.length ? visible : board.people) {
      for (const b of d.blocks) {
        if (min === null || b.from < min) min = b.from;
        if (max === null || b.to > max) max = b.to;
      }
    }
    if (min && max) {
      setDomain({
        minISO: dayjs(min).subtract(7, 'day').format(ISO),
        maxISO: dayjs(max).add(7, 'day').format(ISO),
      });
    }
    if (scrollRef.current) scrollRef.current.scrollLeft = 0;
  };

  const toggleExpand = (id: string) =>
    setExpanded((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });

  const toggleBand = (i: number) =>
    setBandSel((prev) => {
      const next = new Set(prev);
      if (next.has(i)) next.delete(i);
      else next.add(i);
      return next;
    });

  if (board.isLoading) return <Skeleton active paragraph={{ rows: 8 }} />;
  if (board.isError) return <Alert type="error" showIcon message={t('peopleBoard.loadError')} />;

  const stops = legendStops(board.bands);

  return (
    <div>
      <div className={styles.headerRow}>
        <PageHeader title={t('peopleBoard.sectionTitle')} subtitle={t('peopleBoard.sectionSubtitle')} />
        <div className={styles.kpis}>
          <div className={styles.kpi}>
            <StatCard
              label={t('peopleBoard.kpi.overloaded')}
              value={kpis.overloaded}
              suffix={t('peopleBoard.kpi.overloadedFoot', { threshold: board.overloadThreshold })}
              accentColor="#cf1322"
            />
          </div>
          <div className={styles.kpi}>
            <StatCard
              label={t('peopleBoard.kpi.underused')}
              value={kpis.underused}
              suffix={t('peopleBoard.kpi.underusedFoot')}
              accentColor="#3a6ea5"
            />
          </div>
        </div>
      </div>

      <PeopleBoardToolbar
        metric={metric}
        onMetricChange={setMetric}
        bucket={bucket}
        onBucketChange={(b) => {
          setPickedBucket(b);
          setScrollNonce((n) => n + 1);
        }}
        groupBy={groupBy}
        onGroupByChange={setGroupBy}
        query={query}
        onQueryChange={setQuery}
        bands={board.bands}
        bandSelection={bandSel}
        onToggleBand={toggleBand}
        countTentative={countTent}
        onCountTentativeChange={setCountTent}
        sort={sort}
        onSortChange={setSort}
        domain={domain}
        onDomainChange={setDomain}
        onToday={onToday}
        onFit={onFit}
        resultCount={visible.length}
        totalCount={board.people.length}
      />

      <BoardTimeline
        geo={geo}
        scrollRef={scrollRef}
        headerTitle={t('peopleBoard.timeline.header')}
        isEmpty={visible.length === 0}
        emptyContent={
          <Empty
            description={
              <>
                <div>{t('peopleBoard.empty.title')}</div>
                <div>{t('peopleBoard.empty.description')}</div>
              </>
            }
          >
            <Button
              onClick={() => {
                setBandSel(new Set());
                setQuery('');
              }}
            >
              {t('peopleBoard.empty.action')}
            </Button>
          </Empty>
        }
      >
        {groups.map((g) => (
          <div key={g.key || '—'}>
            <div className={styles.groupHeader}>
              {groupBy === 'team' ? <TeamOutlined /> : <UserOutlined />}
              <span>
                {g.label ?? t(groupBy === 'team' ? 'peopleBoard.groups.noTeam' : 'peopleBoard.groups.noRole')}
              </span>
              <span>· {g.people.length}</span>
            </div>
            {g.people.map((d, i) => (
              <PersonBoardRow
                key={d.person.id}
                data={d}
                geo={geo}
                buckets={buckets}
                metric={metric}
                countTentative={countTent}
                bands={board.bands}
                stats={statsOf(d.person.id)}
                expanded={expanded.has(d.person.id)}
                alt={i % 2 === 1}
                onToggle={() => toggleExpand(d.person.id)}
                onInspect={setInspect}
                rootProjects={board.rootProjects}
              />
            ))}
          </div>
        ))}
      </BoardTimeline>

      <div className={styles.legend}>
        <span className={styles.legendTitle}>{t('peopleBoard.legend.title')}</span>
        {stops.map((s, i) => (
          <span key={i} className={styles.legendItem}>
            {/* dynamic: band swatch colour from the configured bands. */}
            <span className={styles.legendSwatch} style={{ background: s.solid, opacity: 0.75 }} />
            {s.label} <span className={styles.legendNote}>{s.range}</span>
          </span>
        ))}
        <span className={styles.legendNote}>
          {t(countTent ? 'peopleBoard.legend.withTent' : 'peopleBoard.legend.hardOnly')}
        </span>
      </div>

      <div className={styles.footnote}>
        <InfoCircleOutlined />
        {t('peopleBoard.footnote')}
        {board.isFetching && (
          <span className={styles.fetchingHint}>
            <Spin size="small" />
          </span>
        )}
      </div>

      <PersonInspector
        target={inspect}
        onClose={() => setInspect(null)}
        peopleById={peopleById}
        geo={geo}
        bands={board.bands}
        countTentative={countTent}
      />
    </div>
  );
}
