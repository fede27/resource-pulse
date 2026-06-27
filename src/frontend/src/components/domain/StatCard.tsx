import type { CSSProperties, ReactNode } from 'react';
import { Card } from 'antd';
import { useStyles } from './StatCard.styles';

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
