import type { ReactNode } from 'react';
import { Typography } from 'antd';
import { useStyles } from './PageHeader.styles';

const { Title } = Typography;

export type PageHeaderProps = {
  title: ReactNode;
  subtitle?: ReactNode;
  actions?: ReactNode;
};

export function PageHeader({ title, subtitle, actions }: PageHeaderProps) {
  const { styles } = useStyles();
  return (
    <div className={styles.root}>
      <div className={styles.titleWrap}>
        <Title level={2} className={styles.title}>
          {title}
        </Title>
        {subtitle && <div className={styles.subtitle}>{subtitle}</div>}
      </div>
      {actions && <div className={styles.actions}>{actions}</div>}
    </div>
  );
}
