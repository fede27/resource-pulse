import { Alert, Spin, theme } from 'antd';
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

export function SettingsPage() {
  const { t } = useTranslation();
  const { token } = theme.useToken();

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
        <div style={{ display: 'flex', justifyContent: 'center', padding: 80 }}>
          <Spin />
        </div>
      )}

      {!isError && ready && (
        <>
          <div style={{ marginBottom: 20, display: 'flex', gap: 8, flexWrap: 'wrap' }}>
            {SECTIONS.map((s) => (
              <button
                key={s.id}
                type="button"
                onClick={() => scrollTo(s.id)}
                style={{
                  fontSize: 13,
                  color: token.colorTextSecondary,
                  cursor: 'pointer',
                  padding: '5px 12px',
                  borderRadius: token.borderRadius,
                  background: token.colorBgContainer,
                  border: `1px solid ${token.colorBorderSecondary}`,
                }}
              >
                {t(s.labelKey)}
              </button>
            ))}
          </div>

          <div style={{ display: 'flex', flexDirection: 'column', gap: 20, maxWidth: 900 }}>
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
