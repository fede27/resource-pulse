import type { CSSProperties, ReactNode } from 'react';
import { Card } from 'antd';
import { createStyles } from 'antd-style';

const useStyles = createStyles(({ token, css }) => ({
  card: css`
    height: 100%;
  `,
  label: css`
    color: ${token.colorTextTertiary};
    font-size: ${token.fontSizeSM}px;
  `,
  valueRow: css`
    margin-block-start: ${token.marginXXS}px;
    display: flex;
    align-items: baseline;
    gap: ${token.marginXXS}px;
  `,
  value: css`
    font-size: 28px;
    font-weight: ${token.fontWeightStrong};
    font-variant-numeric: tabular-nums;
    /* Falls back to the body text colour when no accent is supplied. */
    color: var(--stat-accent, ${token.colorText});
    line-height: 1.1;
  `,
  suffix: css`
    color: ${token.colorTextTertiary};
    font-size: ${token.fontSizeSM}px;
  `,
}));

export type StatCardProps = {
  label: ReactNode;
  value: ReactNode;
  suffix?: ReactNode;
  accentColor?: string;
};

export function StatCard({ label, value, suffix, accentColor }: StatCardProps) {
  const { styles } = useStyles();
  // dynamic: the accent colour is chosen by the caller from live load data
  // (e.g. overload red), so it can't be a static token — pipe it through a CSS
  // variable and let the scoped rule consume it.
  const accentVar = accentColor
    ? ({ '--stat-accent': accentColor } as CSSProperties)
    : undefined;
  return (
    <Card size="small" className={styles.card}>
      <div className={styles.label}>{label}</div>
      <div className={styles.valueRow}>
        <span className={styles.value} style={accentVar}>
          {value}
        </span>
        {suffix && <span className={styles.suffix}>{suffix}</span>}
      </div>
    </Card>
  );
}
