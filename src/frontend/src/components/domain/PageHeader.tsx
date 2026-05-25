import type { ReactNode } from 'react';
import { Typography } from 'antd';

const { Title } = Typography;

export type PageHeaderProps = {
  title: ReactNode;
  subtitle?: ReactNode;
  actions?: ReactNode;
};

export function PageHeader({ title, subtitle, actions }: PageHeaderProps) {
  return (
    <div
      style={{
        display: 'flex',
        alignItems: 'flex-end',
        justifyContent: 'space-between',
        gap: 24,
        flexWrap: 'wrap',
        marginBottom: 24,
      }}
    >
      <div style={{ minWidth: 0 }}>
        <Title level={2} style={{ margin: 0 }}>
          {title}
        </Title>
        {subtitle && (
          <div style={{ marginTop: 4, color: 'rgba(0,0,0,.45)', fontSize: 14 }}>{subtitle}</div>
        )}
      </div>
      {actions && (
        <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>{actions}</div>
      )}
    </div>
  );
}
