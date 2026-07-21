import { useTranslation } from 'react-i18next';
import type { CompanyClosureReadDto } from '@/api/generated/schemas';
import { closureStatus } from './closure.utils';
import { useStyles } from './ClosureStatusPill.styles';

export function ClosureStatusPill({ closure }: { closure: CompanyClosureReadDto }) {
  const { t } = useTranslation();
  const { styles, cx } = useStyles();
  const status = closureStatus(closure);

  if (status === 'ongoing') {
    return (
      <span className={cx(styles.pill, styles.ongoing)}>
        <span className={styles.dot} />
        {t('timeConfig.closures.statusOngoing')}
      </span>
    );
  }
  if (status === 'upcoming') {
    return (
      <span className={cx(styles.pill, styles.upcoming)}>
        {t('timeConfig.closures.statusUpcoming')}
      </span>
    );
  }
  return (
    <span className={cx(styles.pill, styles.past)}>
      {t('timeConfig.closures.statusPast')}
    </span>
  );
}
