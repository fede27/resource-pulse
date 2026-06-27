import type { ReactNode } from 'react';
import { Typography } from 'antd';
import { createStyles } from 'antd-style';

const { Title } = Typography;

const useStyles = createStyles(({ token, css }) => ({
  root: css`
    display: flex;
    align-items: flex-end;
    justify-content: space-between;
    gap: ${token.marginLG}px;
    flex-wrap: wrap;
    margin-block-end: ${token.marginLG}px;
  `,
  titleWrap: css`
    min-width: 0;
  `,
  title: css`
    margin: 0;
  `,
  subtitle: css`
    margin-block-start: ${token.marginXXS}px;
    color: ${token.colorTextTertiary};
    font-size: ${token.fontSize}px;
  `,
  actions: css`
    display: flex;
    gap: ${token.marginXS}px;
    align-items: center;
  `,
}));

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
