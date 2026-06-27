import { Alert, Spin } from 'antd';
import { createStyles } from 'antd-style';
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

const SECTIONS = [
  { id: 'cfg-load', labelKey: 'settings.nav.load' },
  { id: 'cfg-fence', labelKey: 'settings.nav.fence' },
  { id: 'cfg-bucket', labelKey: 'settings.nav.bucket' },
  { id: 'cfg-commit', labelKey: 'settings.nav.commit' },
] as const;

const useStyles = createStyles(({ token, css }) => ({
  loading: css`
    display: flex;
    justify-content: center;
    padding: 80px;
  `,
  nav: css`
    margin-block-end: 20px;
    display: flex;
    gap: ${token.marginXS}px;
    flex-wrap: wrap;
  `,
  navChip: css`
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextSecondary};
    cursor: pointer;
    padding: 5px ${token.paddingSM}px;
    border-radius: ${token.borderRadius}px;
    background: ${token.colorBgContainer};
    border: 1px solid ${token.colorBorderSecondary};
    transition: border-color ${token.motionDurationFast};
    &:hover {
      border-color: ${token.colorPrimary};
    }
  `,
  cards: css`
    display: flex;
    flex-direction: column;
    gap: 20px;
    max-width: 900px;
  `,
}));

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
