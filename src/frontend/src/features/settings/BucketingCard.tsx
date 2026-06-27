import { useState } from 'react';
import { App, Segmented } from 'antd';
import { useQueryClient } from '@tanstack/react-query';
import { Trans, useTranslation } from 'react-i18next';
import {
  getBucketingGetQueryKey,
  useBucketingUpdate,
} from '@/api/generated/bucketing/bucketing';
import { BucketGrain, type BucketingDefaultsDto } from '@/api/generated/schemas';
import { useApiError } from '@/lib/errors';
import { ConfigCard } from './ConfigCard';
import { ConstantNote } from './ConstantNote';
import { grainKey } from './helpers';
import { useStyles } from './BucketingCard.styles';

const nowTime = () =>
  new Date().toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' });

export function BucketingCard({ committed }: { committed: BucketingDefaultsDto }) {
  const { t } = useTranslation();
  const { styles } = useStyles();
  const { message } = App.useApp();
  const showApiError = useApiError();
  const queryClient = useQueryClient();

  // Normalize the loosely-typed DTO (optional grains) into a strict draft.
  const norm = (dto: BucketingDefaultsDto) => ({
    primaryGrain: dto.primaryGrain ?? BucketGrain.Week,
    secondaryGrain: dto.secondaryGrain ?? BucketGrain.Month,
  });
  const base = norm(committed);

  const [draft, setDraft] = useState(() => norm(committed));
  const [savedAt, setSavedAt] = useState<string | null>(null);

  const dirty =
    draft.primaryGrain !== base.primaryGrain || draft.secondaryGrain !== base.secondaryGrain;
  const valid = draft.primaryGrain !== draft.secondaryGrain;

  const mutation = useBucketingUpdate({
    mutation: {
      onSuccess: (data) => {
        message.success(t('settings.bucket.saveSuccess'));
        setSavedAt(nowTime());
        queryClient.setQueryData(getBucketingGetQueryKey(), data);
        queryClient
          .invalidateQueries({ queryKey: getBucketingGetQueryKey() })
          .catch(() => undefined);
      },
      onError: (e) => showApiError(e),
    },
  });

  const options = [BucketGrain.Day, BucketGrain.Week, BucketGrain.Month].map((g) => ({
    value: g,
    label: t(`settings.bucket.grains.${grainKey(g)}`),
  }));

  const save = () => {
    if (!valid) return;
    mutation.mutate({
      data: { primaryGrain: draft.primaryGrain, secondaryGrain: draft.secondaryGrain },
    });
  };

  return (
    <ConfigCard
      title={t('settings.bucket.title')}
      subtitle={t('settings.bucket.subtitle')}
      dirty={dirty}
      valid={valid}
      saving={mutation.isPending}
      savedAt={savedAt}
      onSave={save}
      onReset={() => setDraft(base)}
    >
      <div className={styles.row}>
        <div>
          <div className={styles.fieldLabel}>{t('settings.bucket.primary')}</div>
          <Segmented
            value={draft.primaryGrain}
            onChange={(v) => setDraft((s) => ({ ...s, primaryGrain: v as BucketGrain }))}
            options={options}
          />
        </div>
        <div>
          <div className={styles.fieldLabel}>{t('settings.bucket.secondary')}</div>
          <Segmented
            value={draft.secondaryGrain}
            onChange={(v) => setDraft((s) => ({ ...s, secondaryGrain: v as BucketGrain }))}
            options={options}
          />
        </div>
      </div>

      {!valid && <div className={styles.error}>{t('settings.bucket.mustDiffer')}</div>}
      {valid && (
        <div className={styles.summary}>
          <Trans
            i18nKey="settings.bucket.summary"
            values={{
              primary: t(`settings.bucket.grains.${grainKey(draft.primaryGrain)}`).toLowerCase(),
              secondary: t(`settings.bucket.grains.${grainKey(draft.secondaryGrain)}`).toLowerCase(),
            }}
            components={[<strong key="0" />, <strong key="1" />]}
          />
        </div>
      )}

      <ConstantNote>{t('settings.bucket.constantNote')}</ConstantNote>
    </ConfigCard>
  );
}
