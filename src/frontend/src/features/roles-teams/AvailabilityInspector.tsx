import { useMemo } from 'react';
import { Button } from 'antd';
import { EditOutlined, PlusOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import { useTranslation } from 'react-i18next';
import {
  AdjustmentType,
  type BusinessCalendarReadDto,
  type CompanyClosureReadDto,
  type IndividualAdjustmentDto,
  type ResourceReadDto,
} from '@/api/generated/schemas';
import { parseDurationHours } from '@/lib/duration';
import {
  dayInfo,
  sumEffective,
  sumNominal,
  windowsWeeklyHours,
  patternFor,
} from './availabilityModel';
import { EXTRA_DOT, FERIE_DOT, STATE_COLORS } from './availabilityColors';
import { useStyles } from './AvailabilityInspector.styles';

export type AvailabilityInspectorProps = {
  resource: ResourceReadDto;
  calendar: BusinessCalendarReadDto | undefined;
  closures: CompanyClosureReadDto[];
  capacityByDay: ReadonlyMap<string, number>;
  from: string;
  to: string;
  onChangeCalendar: () => void;
  onAddAdjustment: (type: AdjustmentType, from: string, to: string) => void;
  onEditAdjustment: (adj: IndividualAdjustmentDto) => void;
};

const fmt = (iso: string) => dayjs(iso).format('D MMM YYYY');
const fmtShort = (iso: string) => dayjs(iso).format('D MMM');

export function AvailabilityInspector({
  resource,
  calendar,
  closures,
  capacityByDay,
  from,
  to,
  onChangeCalendar,
  onAddAdjustment,
  onEditAdjustment,
}: AvailabilityInspectorProps) {
  const { t } = useTranslation();
  const { styles, cx } = useStyles();

  const stateLabels: Record<string, string> = {
    work: t('rolesTeams.avail.legendWork'),
    ferie: t('rolesTeams.avail.legendFerie'),
    extra: t('rolesTeams.avail.legendExtra'),
    closure: t('rolesTeams.avail.legendClosure'),
    off: t('rolesTeams.avail.legendOff'),
  };

  const single = from === to;
  const effective = sumEffective(capacityByDay, from, to);
  const nominal = sumNominal(resource, calendar, from, to);
  const delta = Math.round((effective - nominal) * 10) / 10;
  const weekly = windowsWeeklyHours(patternFor(resource, calendar));

  const exceptions = useMemo(
    () =>
      (resource.adjustments ?? [])
        .filter((a) => a.dateFrom && a.dateTo && a.dateFrom <= to && a.dateTo >= from)
        .slice()
        .sort((a, b) => (a.dateFrom ?? '').localeCompare(b.dateFrom ?? '')),
    [resource.adjustments, from, to],
  );

  const single_di = single ? dayInfo(resource, calendar, closures, from) : null;
  const heroColors = single_di ? STATE_COLORS[single_di.state] : STATE_COLORS.off;
  const singleEffective = single ? (capacityByDay.get(from) ?? 0) : 0;

  return (
    <div>
      <div className={cx(styles.section, styles.sectionFirst)}>
        {t('rolesTeams.insp.assignedCalendar')}
      </div>
      <div className={styles.calCard}>
        <div>
          <div className={styles.calName}>{calendar?.name ?? '—'}</div>
          <div className={styles.calSub}>
            {t('rolesTeams.picker.weekHours', { hours: weekly })}
          </div>
        </div>
        <Button size="small" icon={<EditOutlined />} onClick={onChangeCalendar}>
          {t('rolesTeams.insp.change')}
        </Button>
      </div>

      <div className={styles.section}>
        {single
          ? t('rolesTeams.insp.dayTitle', { date: fmt(from) })
          : t('rolesTeams.insp.periodTitle', { from: fmtShort(from), to: fmtShort(to) })}
      </div>

      {single && single_di ? (
        <div>
          <div
            className={styles.dayHero}
            // dynamic: colour resolved from the live day-state.
            style={{ background: heroColors.bg, border: `1px solid ${heroColors.border}` }}
          >
            <span
              className={styles.heroHours}
              // dynamic: state colour.
              style={{ color: heroColors.fg }}
            >
              {singleEffective}h
            </span>
            <div className={styles.heroLabel} style={{ color: heroColors.fg }}>
              <div className={styles.heroState}>{stateLabels[single_di.state]}</div>
              <div>{dayjs(from).format('dddd')}</div>
            </div>
          </div>
          <div className={styles.row}>
            <span className={styles.rowLabel}>{t('rolesTeams.insp.baseFromCalendar')}</span>
            <span className={styles.rowValue}>
              {single_di.closure ? t('rolesTeams.insp.closureZero') : `${single_di.baseHours}h`}
            </span>
          </div>
          {single_di.closure && (
            <div className={styles.row}>
              <span className={styles.rowLabel}>{t('rolesTeams.insp.closureLabel')}</span>
              <span className={styles.rowValue}>{single_di.closure.reason}</span>
            </div>
          )}
          {single_di.ferieHours > 0 && (
            <div className={styles.row}>
              <span className={styles.rowLabel}>{t('rolesTeams.insp.ferie')}</span>
              <span className={styles.rowValue}>−{single_di.ferieHours}h</span>
            </div>
          )}
          {single_di.extraHours > 0 && (
            <div className={styles.row}>
              <span className={styles.rowLabel}>{t('rolesTeams.insp.extra')}</span>
              <span className={styles.rowValue}>+{single_di.extraHours}h</span>
            </div>
          )}
          <div className={styles.row}>
            <span className={styles.rowLabel}>{t('rolesTeams.insp.effective')}</span>
            <span className={cx(styles.rowValue, styles.rowStrong)}>{singleEffective}h</span>
          </div>
        </div>
      ) : (
        <div>
          <div className={styles.twoStat}>
            <div className={cx(styles.statBox, styles.statBoxAccent)}>
              <div className={cx(styles.statLabel, styles.statLabelAccent)}>
                {t('rolesTeams.insp.effective')}
              </div>
              <div className={cx(styles.statValue, styles.statValueAccent)}>{effective}h</div>
            </div>
            <div className={styles.statBox}>
              <div className={styles.statLabel}>{t('rolesTeams.insp.nominal')}</div>
              <div className={styles.statValue}>{nominal}h</div>
            </div>
          </div>
          {delta !== 0 && (
            <div
              className={styles.delta}
              // dynamic: sign-dependent semantic colour.
              style={{ color: delta < 0 ? STATE_COLORS.ferie.fg : STATE_COLORS.extra.fg }}
            >
              {delta < 0
                ? t('rolesTeams.insp.deltaLess', { delta })
                : t('rolesTeams.insp.deltaMore', { delta })}
            </div>
          )}
        </div>
      )}

      <div className={styles.section}>{t('rolesTeams.insp.exceptionsTitle')}</div>
      {exceptions.length === 0 && (
        <div className={styles.noExc}>{t('rolesTeams.insp.noExceptions')}</div>
      )}
      {exceptions.map((a) => {
        const isFerie = a.type === AdjustmentType.Absence;
        return (
          <div key={a.id} className={styles.exception} onClick={() => onEditAdjustment(a)}>
            <span
              className={styles.excDot}
              // dynamic: adjustment-type colour.
              style={{ background: isFerie ? FERIE_DOT : EXTRA_DOT }}
            />
            <div className={styles.excBody}>
              <div className={styles.excReason}>
                {a.reason || (isFerie ? t('rolesTeams.insp.ferie') : t('rolesTeams.insp.extra'))}
              </div>
              <div className={styles.excMeta}>
                {fmtShort(a.dateFrom ?? '')} – {fmtShort(a.dateTo ?? '')} ·{' '}
                {a.hours != null
                  ? t('rolesTeams.insp.hoursPerDay', { hours: parseDurationHours(a.hours) })
                  : t('rolesTeams.insp.fullDay')}
              </div>
            </div>
            <span
              className={styles.excTag}
              // dynamic: adjustment-type colour.
              style={{ color: isFerie ? STATE_COLORS.ferie.fg : STATE_COLORS.extra.fg }}
            >
              {isFerie ? t('rolesTeams.insp.ferie') : t('rolesTeams.insp.extra')}
            </span>
          </div>
        );
      })}
      <div className={styles.addRow}>
        <Button
          size="small"
          icon={<PlusOutlined />}
          onClick={() => onAddAdjustment(AdjustmentType.Absence, from, to)}
        >
          {t('rolesTeams.insp.addFerie')}
        </Button>
        <Button
          size="small"
          icon={<PlusOutlined />}
          onClick={() => onAddAdjustment(AdjustmentType.ExtraTime, from, to)}
        >
          {t('rolesTeams.insp.addExtra')}
        </Button>
      </div>

      <div className={styles.explain}>
        {t('rolesTeams.insp.explain', { cal: calendar?.name ?? '—' })}
      </div>
    </div>
  );
}
