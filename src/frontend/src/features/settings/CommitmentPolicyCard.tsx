import { useState } from 'react';
import { App, Checkbox } from 'antd';
import { useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import {
  getCommitmentPolicyGetQueryKey,
  useCommitmentPolicyUpdate,
} from '@/api/generated/commitment-policy/commitment-policy';
import { CommitmentLevel, type CommitmentPolicyDto } from '@/api/generated/schemas';
import { useApiError } from '@/lib/errors';
import { ConfigCard } from './ConfigCard';
import { ConstantNote } from './ConstantNote';
import { useStyles } from './CommitmentPolicyCard.styles';

const nowTime = () =>
  new Date().toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' });

type LevelKey = 'exploratory' | 'planned' | 'committed' | 'critical';

// Domain order of the CommitmentLevel enum.
const LEVELS: { value: CommitmentLevel; key: LevelKey }[] = [
  { value: CommitmentLevel.Exploratory, key: 'exploratory' },
  { value: CommitmentLevel.Planned, key: 'planned' },
  { value: CommitmentLevel.Committed, key: 'committed' },
  { value: CommitmentLevel.Critical, key: 'critical' },
];

const sortedSig = (levels: CommitmentLevel[]) => [...levels].sort((a, b) => a - b).join(',');

export function CommitmentPolicyCard({ committed }: { committed: CommitmentPolicyDto }) {
  const { t } = useTranslation();
  const { styles, cx } = useStyles();
  const { message } = App.useApp();
  const showApiError = useApiError();
  const queryClient = useQueryClient();

  const committedHard = committed.hardCommitLevels ?? [];
  const [hard, setHard] = useState<CommitmentLevel[]>(() => committed.hardCommitLevels ?? []);
  const [savedAt, setSavedAt] = useState<string | null>(null);

  const dirty = sortedSig(hard) !== sortedSig(committedHard);
  const valid = hard.length > 0;

  const mutation = useCommitmentPolicyUpdate({
    mutation: {
      onSuccess: (data) => {
        message.success(t('settings.commit.saveSuccess'));
        setSavedAt(nowTime());
        queryClient.setQueryData(getCommitmentPolicyGetQueryKey(), data);
        queryClient
          .invalidateQueries({ queryKey: getCommitmentPolicyGetQueryKey() })
          .catch(() => undefined);
      },
      onError: (e) => showApiError(e),
    },
  });

  const toggle = (lvl: CommitmentLevel) =>
    setHard((h) => (h.includes(lvl) ? h.filter((x) => x !== lvl) : [...h, lvl]));

  const save = () => {
    if (!valid) return;
    mutation.mutate({ data: { hardCommitLevels: hard } });
  };

  const labelFor = (key: LevelKey) => t(`settings.commit.levels.${key}`);
  const descFor = (key: LevelKey) => t(`settings.commit.levels.${key}Desc`);
  const allowedLabels = LEVELS.filter((l) => hard.includes(l.value)).map((l) => labelFor(l.key));
  const otherLabels = LEVELS.filter((l) => !hard.includes(l.value)).map((l) => labelFor(l.key));

  return (
    <ConfigCard
      title={t('settings.commit.title')}
      subtitle={t('settings.commit.subtitle')}
      dirty={dirty}
      valid={valid}
      saving={mutation.isPending}
      savedAt={savedAt}
      onSave={save}
      onReset={() => setHard(committedHard)}
    >
      <div className={styles.enableLabel}>{t('settings.commit.enableLabel')}</div>
      <div className={styles.list}>
        {LEVELS.map((l) => {
          const on = hard.includes(l.value);
          return (
            <label key={l.value} className={cx(styles.option, on && styles.optionOn)}>
              <Checkbox checked={on} onChange={() => toggle(l.value)} />
              <div className={styles.optionBody}>
                <div className={styles.optionTitle}>
                  {labelFor(l.key)}
                  {on && (
                    <span className={styles.permitsChip}>
                      {t('settings.commit.permitsHard')}
                    </span>
                  )}
                </div>
                <div className={styles.optionDesc}>{descFor(l.key)}</div>
              </div>
            </label>
          );
        })}
      </div>

      {!valid && <div className={styles.error}>{t('settings.commit.atLeastOne')}</div>}

      {valid && (
        <div className={styles.summary}>
          {t('settings.commit.summary', {
            allowed: allowedLabels.join(', '),
            others: otherLabels.length ? otherLabels.join(', ') : t('settings.commit.summaryOthersNone'),
          })}
        </div>
      )}

      <ConstantNote>{t('settings.commit.constantNote')}</ConstantNote>
    </ConfigCard>
  );
}
