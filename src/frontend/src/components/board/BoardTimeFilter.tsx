import { Button, DatePicker, Segmented } from 'antd';
import { AimOutlined, CalendarOutlined, LeftOutlined, RightOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import { useTranslation } from 'react-i18next';
import type { Grain } from '@/components/timeline';
import type { BoardDomain } from './boardDomain';
import { useStyles } from './BoardTimeFilter.styles';

const ISO = 'YYYY-MM-DD';

export type BoardTimeFilterProps = {
  grain: Grain;
  onGrainChange: (g: Grain) => void;
  domain: BoardDomain;
  onDomainChange: (d: BoardDomain) => void;
  /** Bring today back into view (and re-centre the scroll on it). */
  onToday: () => void;
  /** Re-frame the domain around what the view actually has to show. */
  onFit: () => void;
};

// THE time filter for every timed view (Progetti, Persone, Disponibilità).
// Grain and window are one gesture — reading "settimana" and reading "2026" are
// the same question asked twice — so they live in one component and are never
// re-spelled per feature. Anything that shapes ROWS (group-by, sort, search)
// belongs elsewhere in the toolbar.
export function BoardTimeFilter({
  grain,
  onGrainChange,
  domain,
  onDomainChange,
  onToday,
  onFit,
}: BoardTimeFilterProps) {
  const { t } = useTranslation();
  const { styles } = useStyles();
  const year = dayjs(domain.minISO).year();
  const setYear = (y: number) => onDomainChange({ minISO: `${y}-01-01`, maxISO: `${y}-12-31` });

  return (
    <span className={styles.group}>
      <span className={styles.label}>{t('board.time.grain')}</span>
      <Segmented<Grain>
        size="small"
        value={grain}
        onChange={onGrainChange}
        options={[
          { value: 'day', label: t('board.time.day') },
          { value: 'week', label: t('board.time.week') },
          { value: 'month', label: t('board.time.month') },
        ]}
      />
      <span className={styles.divider} />
      <span className={styles.yearStepper}>
        <Button
          type="text"
          size="small"
          icon={<LeftOutlined />}
          aria-label={t('board.time.prevYear')}
          onClick={() => setYear(year - 1)}
        />
        <span className={styles.yearLabel}>{year}</span>
        <Button
          type="text"
          size="small"
          icon={<RightOutlined />}
          aria-label={t('board.time.nextYear')}
          onClick={() => setYear(year + 1)}
        />
      </span>
      <Button size="small" icon={<CalendarOutlined />} onClick={onToday}>
        {t('board.time.today')}
      </Button>
      <Button size="small" icon={<AimOutlined />} onClick={onFit}>
        {t('board.time.fit')}
      </Button>
      <DatePicker.RangePicker
        size="small"
        allowClear={false}
        value={[dayjs(domain.minISO), dayjs(domain.maxISO)]}
        onChange={(range) => {
          if (!range?.[0] || !range[1]) return;
          onDomainChange({ minISO: range[0].format(ISO), maxISO: range[1].format(ISO) });
        }}
      />
    </span>
  );
}
