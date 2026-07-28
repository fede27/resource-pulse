import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
  type CSSProperties,
} from 'react';
import { Skeleton } from 'antd';
import { CaretRightOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import { useTranslation } from 'react-i18next';
import type {
  BusinessCalendarReadDto,
  IndividualAdjustmentDto,
  ResourceReadDto,
} from '@/api/generated/schemas';
import { AdjustmentType } from '@/api/generated/schemas';
import { InitialsAvatar } from '@/components/domain/InitialsAvatar';
import { InspectorDrawer } from '@/components/domain/InspectorDrawer';
import {
  BoardTimeFilter,
  BoardToolbar,
  clampDomain,
  RowGap,
  useFrameMaxHeight,
  useVisibleXRange,
  useWindowedRows,
  type BoardDomain,
} from '@/components/board';
import type { Grain } from '@/components/timeline';
import { useAvailabilityData, EMPTY_CAP } from './useAvailabilityData';
import {
  addDays,
  bucketAgg,
  buildBuckets,
  closureOn,
  exceptionsExtent,
  groupByTeam,
  type Bucket,
  type BucketAgg,
  type TeamGroup,
} from './availabilityModel';
import { STATE_COLORS, FERIE_DOT, EXTRA_DOT } from './availabilityColors';
import { AvailabilityInspector } from './AvailabilityInspector';
import { AdjustmentEditor, type EditorInitial } from './AdjustmentEditor';
import { CalendarPicker } from './CalendarPicker';
import {
  BUCKET_W,
  CELL_H,
  LABEL_W,
  PERSON_ROW_H,
  teamRowHeight,
  useStyles,
} from './AvailabilityTimeline.styles';

const ISO = 'YYYY-MM-DD';

type Selection = { resource: ResourceReadDto; from: string; to: string };
type RowData = {
  person: ResourceReadDto;
  calendar: BusinessCalendarReadDto | undefined;
  capacityByDay: ReadonlyMap<string, number>;
  aggs: BucketAgg[];
};
// One positional sequence for the vertical windowing: team headers and person
// rows flattened as siblings, heights derived from state. A collapsed team
// contributes only its header item.
type AvailRowItem =
  | { kind: 'team'; key: string; height: number; group: TeamGroup<RowData> }
  | { kind: 'person'; key: string; height: number; row: RowData };

export function AvailabilityTimeline() {
  const { t } = useTranslation();
  const { styles, cx } = useStyles();

  const todayISO = dayjs().format(ISO);
  const [grain, setGrain] = useState<Grain>('week');
  // Same window grammar as the boards: a visual domain the user steers with the
  // shared time filter, not a fixed count of buckets stepped by ‹ / ›.
  const initialDomain = useMemo<BoardDomain>(
    () => ({
      minISO: dayjs(todayISO).subtract(1, 'week').format(ISO),
      maxISO: dayjs(todayISO).add(11, 'week').format(ISO),
    }),
    [todayISO],
  );
  const [pickedDomain, setPickedDomain] = useState<BoardDomain | null>(null);
  const domain = pickedDomain ?? initialDomain;
  const setDomain = (d: BoardDomain) => setPickedDomain(clampDomain(d));

  const [collapsed, setCollapsed] = useState<Record<string, boolean>>({});
  const [sel, setSel] = useState<Selection | null>(null);
  const [adjEditor, setAdjEditor] = useState<{
    resource: ResourceReadDto;
    initial: EditorInitial;
  } | null>(null);
  const [calPicker, setCalPicker] = useState<ResourceReadDto | null>(null);

  // Board scroller (both windowings track this viewport) + frame height bound.
  const frameRef = useRef<HTMLDivElement | null>(null);
  const scrollRef = useRef<HTMLDivElement | null>(null);
  const maxHeight = useFrameMaxHeight(frameRef);
  const [scrollNonce, setScrollNonce] = useState(0);
  // dynamic: viewport-remaining height measured at runtime, applied via CSS var.
  const frameStyle = {
    '--board-max-h': maxHeight === null ? 'none' : `${maxHeight}px`,
  } as CSSProperties;

  const bucketW = BUCKET_W[grain];
  const cellH = CELL_H[grain];

  const buckets = useMemo<Bucket[]>(
    () => buildBuckets(domain.minISO, domain.maxISO, grain),
    [domain.minISO, domain.maxISO, grain],
  );
  const fromISO = buckets[0]?.from ?? domain.minISO;
  const toISO = buckets[buckets.length - 1]?.to ?? domain.maxISO;

  const data = useAvailabilityData(fromISO, toISO);
  const { resources, calendarsById, calendars, closures, teamNameById, capacityByResource } = data;

  // Precompute per-person bucket aggregates once per data/layout change so
  // opening the drawer (sel state) never recomputes the grid cells.
  const rows = useMemo<RowData[]>(
    () =>
      resources.map((person) => {
        const calendar = person.businessCalendarId
          ? calendarsById.get(person.businessCalendarId)
          : undefined;
        const capacityByDay = person.id
          ? (capacityByResource.get(person.id) ?? EMPTY_CAP)
          : EMPTY_CAP;
        const aggs = buckets.map((b) => bucketAgg(person, calendar, closures, capacityByDay, b));
        return { person, calendar, capacityByDay, aggs };
      }),
    [resources, calendarsById, capacityByResource, closures, buckets],
  );

  const rowByPersonId = useMemo(() => {
    const map = new Map<string, RowData>();
    for (const r of rows) if (r.person.id) map.set(r.person.id, r);
    return map;
  }, [rows]);

  const groups = useMemo(
    () => groupByTeam(rows, (r) => r.person.teamId, teamNameById, t('rolesTeams.avail.noTeam')),
    [rows, teamNameById, t],
  );

  const todayFlags = useMemo(
    () => buckets.map((b) => todayISO >= b.from && todayISO <= b.to),
    [buckets, todayISO],
  );
  const closureFlags = useMemo(
    () => buckets.map((b) => !!(closureOn(closures, b.from) || closureOn(closures, b.to))),
    [buckets, closures],
  );

  // Horizontal windowing: at day grain a year is 365 columns × every person, so
  // rows render the visible slice only, with spacers holding the off-screen
  // width. Presentation only — `aggs` above is computed on every bucket.
  const xRange = useVisibleXRange(scrollRef);
  const [firstIdx, lastIdx] = useMemo(() => {
    const n = buckets.length;
    if (n === 0) return [0, -1] as const;
    const first = Math.max(0, Math.floor((xRange.minX - LABEL_W) / bucketW));
    const last = Math.min(n - 1, Math.ceil((xRange.maxX - LABEL_W) / bucketW));
    return last < first ? ([0, -1] as const) : ([first, last] as const);
  }, [xRange, buckets.length, bucketW]);

  const visibleBuckets = useMemo(
    () => buckets.slice(firstIdx, lastIdx + 1),
    [buckets, firstIdx, lastIdx],
  );
  const leadW = firstIdx * bucketW;
  const tailW = (buckets.length - 1 - lastIdx) * bucketW;

  // Flatten groups → team headers + (unless collapsed) person rows, into the
  // positional sequence the windowing walks. Presentation only — every
  // aggregate above (aggs, team totals) is computed on the full roster.
  const teamRowH = teamRowHeight(grain);
  const rowItems = useMemo<AvailRowItem[]>(() => {
    const out: AvailRowItem[] = [];
    for (const group of groups) {
      const key = group.teamId ?? '__none__';
      out.push({ kind: 'team', key: `team-${key}`, height: teamRowH, group });
      if (!(collapsed[key] ?? false)) {
        for (const row of group.members) {
          out.push({ kind: 'person', key: row.person.id ?? '', height: PERSON_ROW_H, row });
        }
      }
    }
    return out;
  }, [groups, collapsed, teamRowH]);

  const { segments } = useWindowedRows(scrollRef, rowItems);

  const todayIdx = useMemo(() => todayFlags.findIndex(Boolean), [todayFlags]);

  // Re-centre on today when asked (and after a grain change re-scales the axis).
  useEffect(() => {
    const el = scrollRef.current;
    if (!el || todayIdx < 0) return;
    el.scrollLeft = Math.max(0, todayIdx * bucketW - 160);
    // eslint-disable-next-line react-hooks/exhaustive-deps -- re-run on explicit nonce bumps and grain changes only
  }, [scrollNonce, grain]);

  const onToday = () => {
    if (!(todayISO >= domain.minISO && todayISO <= domain.maxISO)) setDomain(initialDomain);
    setScrollNonce((n) => n + 1);
  };

  // "Adatta" frames what this board actually has to show. Capacity exists every
  // day, so fitting to "the content" would be a no-op; the exceptions — ferie,
  // straordinari, chiusure — are the extent worth framing. No exceptions, no move.
  const onFit = () => {
    const ext = exceptionsExtent(resources, closures);
    if (!ext) return;
    setDomain({ minISO: addDays(ext.minISO, -7), maxISO: addDays(ext.maxISO, 7) });
    if (scrollRef.current) scrollRef.current.scrollLeft = 0;
  };

  const openCell = useCallback(
    (resource: ResourceReadDto, bucket: Bucket) =>
      setSel({ resource, from: bucket.from, to: bucket.to }),
    [],
  );
  const openCalendar = useCallback((resource: ResourceReadDto) => setCalPicker(resource), []);
  const toggleTeam = useCallback(
    (key: string) => setCollapsed((c) => ({ ...c, [key]: !c[key] })),
    [],
  );

  const spanLabel = `${dayjs(fromISO).format('D MMM')} – ${dayjs(toISO).format('D MMM')}`;
  const minWidth = LABEL_W + buckets.length * bucketW;

  const header = useMemo(
    () => (
      <div className={styles.headerRow}>
        <div className={styles.labelHead} style={{ width: LABEL_W }}>
          {t('rolesTeams.avail.person')}
        </div>
        {/* dynamic: off-screen column width from the horizontal window. */}
        <div className={styles.colGap} style={{ width: leadW }} />
        {visibleBuckets.map((b, k) => {
          const i = firstIdx + k;
          const d = dayjs(b.from);
          const isToday = todayFlags[i];
          const isWeekend = d.day() === 0 || d.day() === 6;
          return (
            <div
              key={b.from}
              className={cx(styles.headCell, isToday && styles.headCellToday)}
              // dynamic: bucket column width from grain.
              style={{ width: bucketW }}
            >
              {grain === 'day' ? (
                <>
                  <div className={cx(styles.headDow, isWeekend && styles.headDowWeekend)}>
                    {d.format('dd')}
                  </div>
                  <div className={cx(styles.headDate, isToday && styles.headDateToday)}>
                    {d.format('D')}
                  </div>
                </>
              ) : (
                <>
                  <div className={cx(styles.headDate, isToday && styles.headDateToday)}>
                    {grain === 'month' ? d.format('MMM') : d.format('D MMM')}
                  </div>
                  <div className={styles.headWeekTag}>
                    {grain === 'month' ? d.format('YYYY') : t('rolesTeams.avail.week')}
                  </div>
                </>
              )}
              {closureFlags[i] && <div className={styles.closureMark} />}
            </div>
          );
        })}
        {/* dynamic: off-screen column width from the horizontal window. */}
        <div className={styles.colGap} style={{ width: tailW }} />
      </div>
    ),
    [
      visibleBuckets,
      firstIdx,
      leadW,
      tailW,
      grain,
      bucketW,
      todayFlags,
      closureFlags,
      styles,
      cx,
      t,
    ],
  );

  // Windowed body rows (gap spacers + team headers + person rows). Deps exclude
  // `sel`/editors so opening an overlay never recomputes the visible cells.
  const body = useMemo(
    () =>
      segments.map((s) => {
        if (s.kind === 'gap') return <RowGap key={s.key} height={s.height} />;

        if (s.item.kind === 'team') {
          const group = s.item.group;
          const key = group.teamId ?? '__none__';
          const isCollapsed = collapsed[key] ?? false;
          return (
            <div key={s.item.key} className={styles.teamRow} style={{ height: teamRowH }}>
              <div
                className={styles.teamLabel}
                style={{ width: LABEL_W }}
                onClick={() => toggleTeam(key)}
              >
                <span className={cx(styles.teamChevron, !isCollapsed && styles.teamChevronOpen)}>
                  <CaretRightOutlined />
                </span>
                <span className={styles.teamName}>{group.teamName}</span>
                <span className={styles.teamCount}>· {group.members.length}</span>
              </div>
              {/* dynamic: off-screen column width from the horizontal window. */}
              <div className={styles.colGap} style={{ width: leadW }} />
              {visibleBuckets.map((b, k) => {
                const i = firstIdx + k;
                const total = group.members.reduce((sum, m) => sum + m.aggs[i]!.hours, 0);
                return (
                  <div
                    key={b.from}
                    className={styles.teamAggCell}
                    // dynamic: bucket column width + cell height from grain.
                    style={{ width: bucketW, height: cellH + 6 }}
                  >
                    {isCollapsed ? (total === 0 ? '–' : `${Math.round(total)}h`) : ''}
                  </div>
                );
              })}
              {/* dynamic: off-screen column width from the horizontal window. */}
              <div className={styles.colGap} style={{ width: tailW }} />
            </div>
          );
        }

        const row = s.item.row;
        return (
          <div key={s.item.key} className={styles.personRow} style={{ height: PERSON_ROW_H }}>
            <div className={styles.personLabel} style={{ width: LABEL_W }}>
              <InitialsAvatar name={row.person.name ?? '?'} size={32} seed={row.person.id ?? ''} />
              <div className={styles.personMeta}>
                <div className={styles.personName}>{row.person.name}</div>
                <div
                  className={styles.personCal}
                  title={t('rolesTeams.avail.changeCalendar')}
                  onClick={() => openCalendar(row.person)}
                >
                  {row.calendar?.name ?? '—'}
                </div>
              </div>
            </div>
            {/* dynamic: off-screen column width from the horizontal window. */}
            <div className={styles.colGap} style={{ width: leadW }} />
            {visibleBuckets.map((b, k) => {
              const agg = row.aggs[firstIdx + k]!;
              const colors = STATE_COLORS[agg.state];
              return (
                <div
                  key={b.from}
                  className={styles.cell}
                  // dynamic: bucket column width from grain.
                  style={{ width: bucketW }}
                  title={`${agg.hours}h`}
                  onClick={() => openCell(row.person, b)}
                >
                  <div
                    className={styles.chip}
                    // dynamic: colours resolved from live day-state; height from grain.
                    style={{
                      height: cellH,
                      background: colors.bg,
                      border: `1px solid ${colors.border}`,
                      borderLeft: `3px solid ${colors.dot}`,
                    }}
                  >
                    <span
                      className={styles.chipHours}
                      // dynamic: state colour + grain-dependent size.
                      style={{ color: colors.fg, fontSize: grain === 'day' ? 11 : 13 }}
                    >
                      {agg.hours === 0 ? '–' : `${agg.hours}h`}
                    </span>
                    {(agg.hasFerie || agg.hasExtra) && (
                      <span className={styles.markers}>
                        {agg.hasFerie && (
                          <span
                            className={styles.marker}
                            // dynamic: marker colour.
                            style={{ background: FERIE_DOT }}
                          />
                        )}
                        {agg.hasExtra && (
                          <span
                            className={styles.marker}
                            // dynamic: marker colour.
                            style={{ background: EXTRA_DOT }}
                          />
                        )}
                      </span>
                    )}
                  </div>
                </div>
              );
            })}
            {/* dynamic: off-screen column width from the horizontal window. */}
            <div className={styles.colGap} style={{ width: tailW }} />
          </div>
        );
      }),
    [
      segments,
      collapsed,
      visibleBuckets,
      firstIdx,
      leadW,
      tailW,
      grain,
      bucketW,
      cellH,
      teamRowH,
      openCell,
      openCalendar,
      toggleTeam,
      styles,
      cx,
      t,
    ],
  );

  const legend: { key: string; label: string; state: keyof typeof STATE_COLORS }[] = [
    { key: 'work', label: t('rolesTeams.avail.legendWork'), state: 'work' },
    { key: 'ferie', label: t('rolesTeams.avail.legendFerie'), state: 'ferie' },
    { key: 'extra', label: t('rolesTeams.avail.legendExtra'), state: 'extra' },
    { key: 'closure', label: t('rolesTeams.avail.legendClosure'), state: 'closure' },
    { key: 'off', label: t('rolesTeams.avail.legendOff'), state: 'off' },
  ];

  if (data.isLoading) return <Skeleton active />;

  const selRow = sel?.resource.id ? rowByPersonId.get(sel.resource.id) : undefined;

  return (
    <div>
      <BoardToolbar>
        <BoardToolbar.Row>
          <BoardTimeFilter
            grain={grain}
            onGrainChange={setGrain}
            domain={domain}
            onDomainChange={setDomain}
            onToday={onToday}
            onFit={onFit}
          />
          {/* The picked domain and the RENDERED span differ once buckets align
              to weeks/months — show what the grid actually covers. */}
          <BoardToolbar.Spacer>
            <BoardToolbar.Count>{spanLabel}</BoardToolbar.Count>
          </BoardToolbar.Spacer>
        </BoardToolbar.Row>
      </BoardToolbar>

      <div ref={frameRef} className={styles.frame} style={frameStyle}>
        <div ref={scrollRef} className={styles.scroll}>
          <div style={{ minWidth }}>
            {header}
            {body}
          </div>
        </div>
      </div>

      <div className={styles.footer}>
        <div className={styles.legend}>
          {legend.map((l) => (
            <span key={l.key} className={styles.legendItem}>
              <span
                className={styles.legendSwatch}
                // dynamic: state swatch colour.
                style={{
                  background: STATE_COLORS[l.state].bg,
                  border: `1px solid ${STATE_COLORS[l.state].border}`,
                }}
              />
              {l.label}
            </span>
          ))}
        </div>
        <span className={styles.hint}>{t('rolesTeams.avail.hint')}</span>
      </div>

      <InspectorDrawer
        open={!!sel}
        onClose={() => setSel(null)}
        title={sel?.resource.name ?? ''}
      >
        {sel && selRow && (
          <AvailabilityInspector
            resource={sel.resource}
            calendar={selRow.calendar}
            closures={closures}
            capacityByDay={selRow.capacityByDay}
            from={sel.from}
            to={sel.to}
            onChangeCalendar={() => setCalPicker(sel.resource)}
            onAddAdjustment={(type, f, to2) =>
              setAdjEditor({
                resource: sel.resource,
                initial: { type, dateFrom: f, dateTo: to2 },
              })
            }
            onEditAdjustment={(adj: IndividualAdjustmentDto) =>
              setAdjEditor({
                resource: sel.resource,
                initial: {
                  ...(adj.id ? { id: adj.id } : {}),
                  type: adj.type ?? AdjustmentType.Absence,
                  dateFrom: adj.dateFrom ?? sel.from,
                  dateTo: adj.dateTo ?? sel.to,
                  hours: adj.hours ?? null,
                  reason: adj.reason ?? '',
                },
              })
            }
          />
        )}
      </InspectorDrawer>

      {adjEditor && (
        <AdjustmentEditor
          key={`${adjEditor.resource.id}-${adjEditor.initial.id ?? 'new'}`}
          open
          resourceId={adjEditor.resource.id ?? ''}
          initial={adjEditor.initial}
          onClose={() => setAdjEditor(null)}
        />
      )}

      {calPicker && (
        <CalendarPicker
          key={calPicker.id}
          open
          resourceId={calPicker.id ?? ''}
          personName={calPicker.name ?? ''}
          currentCalendarId={calPicker.businessCalendarId}
          calendars={calendars}
          onClose={() => setCalPicker(null)}
        />
      )}
    </div>
  );
}
