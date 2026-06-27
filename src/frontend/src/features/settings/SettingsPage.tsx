import { Alert, Spin } from 'antd';
import { useTranslation } from 'react-i18next';
import { useBucketingGet } from '@/api/generated/bucketing/bucketing';
import { useCommitmentPolicyGet } from '@/api/generated/commitment-policy/commitment-policy';
import { useLoadBandsGet } from '@/api/generated/load-bands/load-bands';
import { useTimeFenceGet } from '@/api/generated/time-fence/time-fence';
import { PageHeader } from '@/components/domain/PageHeader';
import { LoadBandCard } from './LoadBandCard';
import { TimeFenceCard } from './TimeFenceCard';
import { BucketingCard } from './BucketingCard';
import { CommitmentPolicyCard } from './CommitmentPolicyCard';
import { useStyles } from './SettingsPage.styles';

const SECTIONS = [
  { id: 'cfg-load', labelKey: 'settings.nav.load' },
  { id: 'cfg-fence', labelKey: 'settings.nav.fence' },
  { id: 'cfg-bucket', labelKey: 'settings.nav.bucket' },
  { id: 'cfg-commit', labelKey: 'settings.nav.commit' },
] as const;

export function SettingsPage() {
  const { t } = useTranslation();
  const { styles } = useStyles();

  const loadBands = useLoadBandsGet();
  const timeFence = useTimeFenceGet();
  const bucketing = useBucketingGet();
  const commitment = useCommitmentPolicyGet();

  const isError =
    loadBands.isError || timeFence.isError || bucketing.isError || commitment.isError;
  const ready =
    loadBands.data && timeFence.data && bucketing.data && commitment.data;

  const scrollTo = (id: string) =>
    document.getElementById(id)?.scrollIntoView({ behavior: 'smooth', block: 'start' });

  return (
    <div>
      <PageHeader title={t('settings.title')} subtitle={t('settings.subtitle')} />

      {isError && <Alert type="error" showIcon message={t('settings.loadError')} />}

      {!isError && !ready && (
        <div className={styles.loading}>
          <Spin />
        </div>
      )}

      {!isError && ready && (
        <>
          <div className={styles.nav}>
            {SECTIONS.map((s) => (
              <button
                key={s.id}
                type="button"
                onClick={() => scrollTo(s.id)}
                className={styles.navChip}
              >
                {t(s.labelKey)}
              </button>
            ))}
          </div>

          <div className={styles.cards}>
            <div id="cfg-load">
              <LoadBandCard committed={loadBands.data} />
            </div>
            <div id="cfg-fence">
              <TimeFenceCard committed={timeFence.data} />
            </div>
            <div id="cfg-bucket">
              <BucketingCard committed={bucketing.data} />
            </div>
            <div id="cfg-commit">
              <CommitmentPolicyCard committed={commitment.data} />
            </div>
          </div>
        </>
      )}
    </div>
  );
}
