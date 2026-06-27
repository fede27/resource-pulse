import type { ReactNode } from 'react';
import { Button } from 'antd';
import { createStyles } from 'antd-style';
import { useTranslation } from 'react-i18next';

export type ConfigCardProps = {
  title: ReactNode;
  subtitle?: ReactNode;
  dirty: boolean;
  valid: boolean;
  saving?: boolean;
  savedAt?: string | null;
  onSave: () => void;
  onReset: () => void;
  children: ReactNode;
};

const useStyles = createStyles(({ token, css }) => ({
  root: css`
    background: ${token.colorBgContainer};
    border: 1px solid ${token.colorBorderSecondary};
    border-radius: ${token.borderRadiusLG}px;
    overflow: hidden;
  `,
  header: css`
    padding: ${token.padding}px 20px;
    border-bottom: 1px solid ${token.colorBorderSecondary};
    display: flex;
    justify-content: space-between;
    align-items: flex-start;
    gap: ${token.margin}px;
  `,
  headerText: css`
    min-width: 0;
  `,
  title: css`
    font-size: ${token.fontSizeLG}px;
    font-weight: 600;
  `,
  subtitle: css`
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextTertiary};
    margin-block-start: 3px;
    max-width: 640px;
    line-height: 1.5;
  `,
  dirtyBadge: css`
    flex-shrink: 0;
    height: 22px;
    padding: 0 ${token.paddingXS}px;
    border-radius: 11px;
    font-size: ${token.fontSizeSM}px;
    font-weight: 500;
    background: ${token.colorWarningBg};
    border: 1px solid ${token.colorWarningBorder};
    color: ${token.colorWarningText};
    display: inline-flex;
    align-items: center;
    gap: 5px;
  `,
  dirtyDot: css`
    width: 6px;
    height: 6px;
    border-radius: 50%;
    background: ${token.colorWarning};
  `,
  body: css`
    padding: 20px;
  `,
  footer: css`
    padding: ${token.paddingSM}px 20px;
    border-top: 1px solid ${token.colorBorderSecondary};
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: ${token.marginSM}px;
    background: ${token.colorFillQuaternary};
  `,
  footerNote: css`
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextTertiary};
  `,
  footerActions: css`
    display: flex;
    gap: ${token.marginXS}px;
  `,
}));

// Shell for one org-level config aggregate: header + dirty badge + body +
// footer with independent save/reset (each aggregate has its own lifecycle).
export function ConfigCard({
  title,
  subtitle,
  dirty,
  valid,
  saving,
  savedAt,
  onSave,
  onReset,
  children,
}: ConfigCardProps) {
  const { t } = useTranslation();
  const { styles } = useStyles();

  return (
    <div className={styles.root}>
      <div className={styles.header}>
        <div className={styles.headerText}>
          <div className={styles.title}>{title}</div>
          {subtitle && <div className={styles.subtitle}>{subtitle}</div>}
        </div>
        {dirty && (
          <span className={styles.dirtyBadge}>
            <span className={styles.dirtyDot} />
            {t('settings.dirtyBadge')}
          </span>
        )}
      </div>

      <div className={styles.body}>{children}</div>

      <div className={styles.footer}>
        <span className={styles.footerNote}>
          {savedAt ? t('settings.savedAt', { time: savedAt }) : t('settings.aggregateFootnote')}
        </span>
        <div className={styles.footerActions}>
          <Button size="small" onClick={onReset} disabled={!dirty || !!saving}>
            {t('common.cancel')}
          </Button>
          <Button
            size="small"
            type="primary"
            onClick={onSave}
            loading={!!saving}
            disabled={!dirty || !valid}
          >
            {t('common.save')}
          </Button>
        </div>
      </div>
    </div>
  );
}
