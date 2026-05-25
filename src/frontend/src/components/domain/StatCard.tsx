import type { ReactNode } from 'react';
import { Card } from 'antd';

export type StatCardProps = {
  label: ReactNode;
  value: ReactNode;
  suffix?: ReactNode;
  accentColor?: string;
};

export function StatCard({ label, value, suffix, accentColor }: StatCardProps) {
  return (
    <Card size="small" style={{ height: '100%' }}>
      <div style={{ color: 'rgba(0,0,0,.45)', fontSize: 13 }}>{label}</div>
      <div
        style={{
          marginTop: 6,
          display: 'flex',
          alignItems: 'baseline',
          gap: 6,
        }}
      >
        <span
          style={{
            fontSize: 28,
            fontWeight: 600,
            fontVariantNumeric: 'tabular-nums',
            color: accentColor ?? 'rgba(0,0,0,.88)',
            lineHeight: 1.1,
          }}
        >
          {value}
        </span>
        {suffix && (
          <span style={{ color: 'rgba(0,0,0,.45)', fontSize: 13 }}>{suffix}</span>
        )}
      </div>
    </Card>
  );
}
