import type { ReactNode } from 'react';
import { LockOutlined } from '@ant-design/icons';
import { createStyles } from 'antd-style';
import { useTranslation } from 'react-i18next';

// Surfaces the "this is a constant, not a knob" boundary (ADR-0020, §1
// explainability). Each card explains what it deliberately does NOT expose.

const useStyles = createStyles(({ token, css }) => ({
  root: css`
    display: flex;
    gap: ${token.marginXS}px;
    padding: ${token.paddingXS}px ${token.paddingSM}px;
    margin-block-start: ${token.marginSM}px;
    background: ${token.colorFillQuaternary};
    border: 1px solid ${token.colorBorderSecondary};
    border-radius: ${token.borderRadius}px;
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextSecondary};
    line-height: 1.5;
  `,
  icon: css`
    flex-shrink: 0;
    margin-block-start: 1px;
    color: ${token.colorTextQuaternary};
  `,
  iconGlyph: css`
    font-size: ${token.fontSizeSM}px;
  `,
  label: css`
    color: ${token.colorTextSecondary};
    font-weight: 500;
  `,
}));

export function ConstantNote({ children }: { children: ReactNode }) {
  const { t } = useTranslation();
  const { styles } = useStyles();

  return (
    <div className={styles.root}>
      <span className={styles.icon}>
        <LockOutlined className={styles.iconGlyph} />
      </span>
      <div>
        <strong className={styles.label}>{t('settings.constantLabel')}</strong>{' '}
        {children}
      </div>
    </div>
  );
}
