import type { ReactNode } from 'react';
import { LockOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { useStyles } from './ConstantNote.styles';

// Surfaces the "this is a constant, not a knob" boundary (ADR-0020, §1
// explainability). Each card explains what it deliberately does NOT expose.

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
