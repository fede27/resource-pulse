import { Button, DatePicker } from 'antd';
import { AimOutlined, CalendarOutlined, LeftOutlined, RightOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import { useTranslation } from 'react-i18next';
import { useStyles } from './BoardDateControls.styles';

const ISO = 'YYYY-MM-DD';

export type BoardDomain = { minISO: string; maxISO: string };

export type BoardDateControlsProps = {
  domain: BoardDomain;
  onDomainChange: (d: BoardDomain) => void;
  onToday: () => void;
  onFit: () => void;
};

// Shared date navigation for the board toolbars: year stepper · Oggi · Adatta ·
// custom range. Both boards (Progetti, Persone) render the same control so the
// timeline grammar stays identical.
export function BoardDateControls({ domain, onDomainChange, onToday, onFit }: BoardDateControlsProps) {
  const { t } = useTranslation();
  const { styles } = useStyles();
  const year = dayjs(domain.minISO).year();
  const setYear = (y: number) => onDomainChange({ minISO: `${y}-01-01`, maxISO: `${y}-12-31` });

  return (
    <span className={styles.group}>
      <span className={styles.yearStepper}>
        <Button
          type="text"
          size="small"
          icon={<LeftOutlined />}
          aria-label={t('board.controls.prevYear')}
          onClick={() => setYear(year - 1)}
        />
        <span className={styles.yearLabel}>{year}</span>
        <Button
          type="text"
          size="small"
          icon={<RightOutlined />}
          aria-label={t('board.controls.nextYear')}
          onClick={() => setYear(year + 1)}
        />
      </span>
      <Button size="small" icon={<CalendarOutlined />} onClick={onToday}>
        {t('board.controls.today')}
      </Button>
      <Button size="small" icon={<AimOutlined />} onClick={onFit}>
        {t('board.controls.fit')}
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
