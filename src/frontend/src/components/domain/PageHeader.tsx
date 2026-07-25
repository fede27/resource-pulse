import type { ReactNode } from 'react';
import { Typography } from 'antd';
import { useStyles } from './PageHeader.styles';

const { Title } = Typography;

export type PageHeaderProps = {
  title: ReactNode;
  subtitle?: ReactNode;
  /**
   * Global-health strip (a `<SignalCards>`), right-aligned before the actions.
   * Every page states its health here — never on a row of its own, which is
   * vertical space the boards need.
   */
  signals?: ReactNode;
  actions?: ReactNode;
};

export function PageHeader({ title, subtitle, signals, actions }: PageHeaderProps) {
  const { styles } = useStyles();
  return (
    <div className={styles.root}>
      <div className={styles.titleWrap}>
        <Title level={2} className={styles.title}>
          {title}
        </Title>
        {subtitle && <div className={styles.subtitle}>{subtitle}</div>}
      </div>
      {(signals || actions) && (
        <div className={styles.aside}>
          {signals}
          {actions && <div className={styles.actions}>{actions}</div>}
        </div>
      )}
    </div>
  );
}
