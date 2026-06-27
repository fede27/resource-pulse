import type { ReactNode } from 'react';
import { Button } from 'antd';
import { useTranslation } from 'react-i18next';
import { useStyles } from './ConfigCard.styles';

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
