import { useCallback, useMemo, useRef, useState, type CSSProperties } from 'react';
import { Button, Segmented, Skeleton } from 'antd';
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
import { RowGap, useFrameMaxHeight, useWindowedRows } from '@/components/board';
import { useAvailabilityData, EMPTY_CAP } from './useAvailabilityData';
import {
  addDays,
  bucketAgg,
  buildBuckets,
  closureOn,
  groupByTeam,
  mondayOf,
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
const N_BUCKETS = { week: 12, day: 35 } as const;

type Grain = 'week' | 'day';
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
  const [startISO, setStartISO] = useState(() => addDays(mondayOf(todayISO), -7));
  const [collapsed, setCollapsed] = useState<Record<string, boolean>>({});
  const [sel, setSel] = useState<Selection | null>(null);
  const [adjEditor, setAdjEditor] = useState<{
    resource: ResourceReadDto;
    initial: EditorInitial;
  } | null>(null);
  const [calPicker, setCalPicker] = useState<ResourceReadDto | null>(null);

  // Board scroller (vertical windowing tracks this viewport) + frame height bound.
  const frameRef = useRef<HTMLDivElement | null>(null);
  const scrollRef = useRef<HTMLDivElement | null>(null);
  const maxHeight = useFrameMaxHeight(frameRef);
  // dynamic: viewport-remaining height measured at runtime, applied via CSS var.
  const frameStyle = {
    '--board-max-h': maxHeight === null ? 'none' : `${maxHeight}px`,
  } as CSSProperties;

  const nBuckets = N_BUCKETS[grain];
  const bucketW = BUCKET_W[grain];
  const cellH = CELL_H[grain];

  const buckets = useMemo<Bucket[]>(
    () => buildBuckets(startISO, grain, nBuckets),
    [startISO, grain, nBuckets],
  );
  const fromISO = buckets[0]?.from ?? startISO;
  const toISO = buckets[buckets.length - 1]?.to ?? startISO;

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

  const goToday = () =>
    setStartISO(grain === 'week' ? addDays(mondayOf(todayISO), -7) : addDays(todayISO, -7));
  const goPrev = () => setStartISO(addDays(startISO, grain === 'week' ? -28 : -7));
  const goNext = () => setStartISO(addDays(startISO, grain === 'week' ? 28 : 7));

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
  const minWidth = LABEL_W + nBuckets * bucketW;

  const header = useMemo(
    () => (
      <div className={styles.headerRow}>
        <div className={styles.labelHead} style={{ width: LABEL_W }}>
          {t('rolesTeams.avail.person')}
        </div>
        {buckets.map((b, i) => {
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
              {grain === 'week' ? (
                <>
                  <div className={cx(styles.headDate, isToday && styles.headDateToday)}>
                    {d.format('D MMM')}
                  </div>
                  <div className={styles.headWeekTag}>{t('rolesTeams.avail.week')}</div>
                </>
              ) : (
                <>
                  <div className={cx(styles.headDow, isWeekend && styles.headDowWeekend)}>
                    {d.format('dd')}
                  </div>
                  <div className={cx(styles.headDate, isToday && styles.headDateToday)}>
                    {d.format('D')}
                  </div>
                </>
              )}
              {closureFlags[i] && <div className={styles.closureMark} />}
            </div>
          );
        })}
      </div>
    ),
    [buckets, grain, bucketW, todayFlags, closureFlags, styles, cx, t],
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
              {buckets.map((b, i) => {
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
            {buckets.map((b, i) => {
              const agg = row.aggs[i]!;
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
                      style={{ color: colors.fg, fontSize: grain === 'week' ? 13 : 11 }}
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
          </div>
        );
      }),
    [
      segments,
      collapsed,
      buckets,
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
      <div className={styles.toolbar}>
        <Segmented<Grain>
          value={grain}
          onChange={(g) => setGrain(g)}
          options={[
            { label: t('rolesTeams.avail.grainWeek'), value: 'week' },
            { label: t('rolesTeams.avail.grainDay'), value: 'day' },
          ]}
        />
        <div className={styles.navGroup}>
          <Button size="small" onClick={goPrev}>
            ‹
          </Button>
          <Button size="small" onClick={goToday}>
            {t('rolesTeams.avail.today')}
          </Button>
          <Button size="small" onClick={goNext}>
            ›
          </Button>
        </div>
        <span className={styles.span}>{spanLabel}</span>
      </div>

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
