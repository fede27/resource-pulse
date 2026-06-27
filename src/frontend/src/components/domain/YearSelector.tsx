import { Button, Segmented, Space } from 'antd';
import { LeftOutlined, RightOutlined } from '@ant-design/icons';
import { createStyles } from 'antd-style';
import dayjs from 'dayjs';
import { useTranslation } from 'react-i18next';

const useStyles = createStyles(({ token, css }) => ({
  label: css`
    color: ${token.colorTextSecondary};
    font-size: ${token.fontSizeSM}px;
  `,
}));

export type YearSelectorProps = {
  value: number;
  onChange: (year: number) => void;
  /** Years to surface as quick picks. The selector always allows ‹ › step-through. */
  availableYears: number[];
  /** Optional override for the leading label (defaults to the i18n "common.year" or "Anno"). */
  label?: string;
};

export function YearSelector({
  value,
  onChange,
  availableYears,
  label,
}: YearSelectorProps) {
  const { t } = useTranslation();
  const { styles } = useStyles();
  const currentYear = dayjs().year();
  const resolvedLabel = label ?? t('timeConfig.closures.year');

  const safeYears = availableYears.length > 0 ? availableYears : [currentYear];
  const minYear = Math.min(...safeYears);
  const maxYear = Math.max(...safeYears);

  // Show up to 5 years centered on `value`, clamped to the available range.
  const desired = new Set<number>();
  for (let offset = -2; offset <= 2; offset += 1) desired.add(value + offset);
  desired.add(value);
  desired.add(currentYear);
  safeYears.forEach((y) => desired.add(y));

  const visible = [...desired]
    .filter((y) => y >= minYear - 1 && y <= maxYear + 1)
    .sort((a, b) => a - b);

  return (
    <Space size="small" wrap>
      <span className={styles.label}>{resolvedLabel}</span>
      <Button
        size="small"
        icon={<LeftOutlined />}
        aria-label={String(value - 1)}
        onClick={() => onChange(value - 1)}
      />
      <Segmented<number>
        value={value}
        onChange={(v) => onChange(Number(v))}
        options={visible.map((y) => ({
          label: y === currentYear ? `${y} ·` : `${y}`,
          value: y,
        }))}
      />
      <Button
        size="small"
        icon={<RightOutlined />}
        aria-label={String(value + 1)}
        onClick={() => onChange(value + 1)}
      />
      {value !== currentYear && (
        <Button type="link" size="small" onClick={() => onChange(currentYear)}>
          {t('timeConfig.closures.goToYear', { year: currentYear })}
        </Button>
      )}
    </Space>
  );
}
