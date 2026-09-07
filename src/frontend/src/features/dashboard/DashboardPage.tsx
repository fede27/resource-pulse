import { useState } from 'react';
import { App, Alert, Empty, Input, Modal, Segmented, Skeleton, Tag } from 'antd';
import { CheckCircleOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import dayjs from 'dayjs';

import { SignalScope, type SignalDto } from '@/api/generated/schemas';
import { PageContainer } from '@/components/layout/PageContainer';
import { PageHeader } from '@/components/domain/PageHeader';
import { SignalCards, type SignalItem } from '@/components/domain/SignalCards';
import { useApiError } from '@/lib/errors';
import { useStyles } from './DashboardPage.styles';
import { SignalRow } from './SignalRow';
import { VERB } from './signalColors';
import { useDashboard } from './useDashboard';

export function DashboardPage() {
  const { styles, cx } = useStyles();
  const { t } = useTranslation();
  const { message } = App.useApp();
  const showApiError = useApiError();

  // Binary by decision: "my projects" means the ones I LEAD, and every signal —
  // including the ones whose subject is a person — is scoped through the root
  // projects it touches. The prototype's third "my team" scope went with it.
  const [scope, setScope] = useState<SignalScope>(SignalScope.All);
  const [pendingAck, setPendingAck] = useState<SignalDto | null>(null);
  const [reason, setReason] = useState('');

  const d = useDashboard(scope);

  const signals: SignalItem[] = [
    {
      key: 'gaps',
      label: t('dashboard.cards.uncovered'),
      value: d.verdict.gaps,
      hint: t('dashboard.cards.uncoveredHint', { hours: d.verdict.gapHours }),
      tone: d.verdict.gaps > 0 ? 'danger' : 'ok',
    },
    {
      key: 'overcommits',
      label: t('dashboard.cards.overcommit'),
      value: d.verdict.overcommits,
      tone: d.verdict.overcommits > 0 ? 'warning' : 'ok',
    },
    {
      key: 'accepted',
      label: t('dashboard.cards.accepted'),
      value: d.verdict.acknowledged,
      tone: 'neutral',
    },
  ];

  const confirmAck = async () => {
    if (!pendingAck?.id) return;
    try {
      await d.acknowledgeSignal(pendingAck.id, reason.trim() || null);
      message.success(t('dashboard.acceptedToast'));
    } catch (e) {
      showApiError(e);
    } finally {
      setPendingAck(null);
      setReason('');
    }
  };

  const onReopen = async (s: SignalDto) => {
    if (!s.id) return;
    try {
      await d.reopenSignal(s.id);
      message.success(t('dashboard.reopenedToast'));
    } catch (e) {
      showApiError(e);
    }
  };

  const onConfirm = async (s: SignalDto) => {
    if (!s.link?.allocationId) return;
    try {
      await d.confirmTentative(s.link.allocationId);
      message.success(t('dashboard.confirmedToast'));
    } catch (e) {
      showApiError(e);
    }
  };

  return (
    <PageContainer>
      <div className={styles.page}>
        <PageHeader
          title={t('dashboard.title')}
          subtitle={t('dashboard.subtitle')}
          signals={<SignalCards items={signals} />}
          actions={
            <Segmented
              value={scope}
              onChange={(v) => setScope(v as SignalScope)}
              options={[
                { label: t('dashboard.scope.mine'), value: SignalScope.Mine },
                { label: t('dashboard.scope.all'), value: SignalScope.All },
              ]}
            />
          }
        />

        {/* Three states, not two: an empty queue is only "the plan holds" if we
            know we looked. On a tenant the detector has never visited, that
            sentence would be a lie. */}
        {d.status === 'never' && (
          <Alert
            type="info"
            showIcon
            message={t('dashboard.sweep.neverTitle')}
            description={t('dashboard.sweep.neverBody')}
            style={{ marginBottom: 16 }}
          />
        )}
        {d.status === 'stale' && d.sweep?.lastSweptAt && (
          <Alert
            type="warning"
            showIcon
            message={t('dashboard.sweep.staleTitle')}
            description={t('dashboard.sweep.staleBody', {
              when: dayjs(d.sweep.lastSweptAt).format('D MMM YYYY HH:mm'),
            })}
            style={{ marginBottom: 16 }}
          />
        )}

        {d.isLoading ? (
          <Skeleton active paragraph={{ rows: 6 }} />
        ) : (
          <>
            <div
              className={cx(
                styles.verdict,
                d.verdict.clean ? styles.verdictClean : styles.verdictBreach,
              )}
            >
              {d.verdict.clean
                ? t('dashboard.verdict.clean')
                : t('dashboard.verdict.breach', {
                    hours: d.verdict.gapHours,
                    gaps: d.verdict.gaps,
                    overcommits: d.verdict.overcommits,
                  })}
            </div>

            <div className={styles.sectionHead}>
              <span className={styles.sectionTitle}>{t('dashboard.queue.title')}</span>
              <span className={styles.sectionHint}>{t('dashboard.queue.order')}</span>
            </div>

            <div className={styles.card}>
              {d.queue.shown.length === 0 ? (
                <div className={styles.empty}>
                  <Empty
                    image={<CheckCircleOutlined style={{ fontSize: 40 }} />}
                    description={false}
                  />
                  <div className={styles.emptyTitle}>{t('dashboard.queue.emptyTitle')}</div>
                  <div className={styles.emptyBody}>
                    {scope === SignalScope.All
                      ? t('dashboard.queue.emptyAll')
                      : t('dashboard.queue.emptyMine')}
                  </div>
                </div>
              ) : (
                d.queue.shown.map((s) => (
                  <SignalRow
                    key={s.id}
                    signal={s}
                    canPlan={d.canPlan}
                    busy={d.isMutating}
                    onAcknowledge={setPendingAck}
                    onReopen={(x) => void onReopen(x)}
                    onConfirm={(x) => void onConfirm(x)}
                  />
                ))
              )}
            </div>

            {d.queue.overflow > 0 && (
              <div className={styles.overflow}>
                {t('dashboard.queue.overflow', {
                  shown: d.queue.shown.length,
                  total: d.queue.total,
                  budget: d.queue.budget,
                })}
              </div>
            )}

            {d.feed.length > 0 && (
              <div className={styles.section}>
                <div className={styles.sectionHead}>
                  <span className={styles.sectionTitle}>{t('dashboard.feed.title')}</span>
                  {d.lastVisitedAt && (
                    <span className={styles.sectionHint}>
                      {t('dashboard.feed.since', {
                        when: dayjs(d.lastVisitedAt).format('D MMM HH:mm'),
                      })}
                    </span>
                  )}
                </div>
                <div className={styles.card}>
                  {d.feed.map(({ entry, verb }) => (
                    <div key={entry.signalId} className={styles.feedRow}>
                      {/* dynamic: the verb's tone. */}
                      <Tag
                        style={{
                          color: VERB[verb].fg,
                          background: VERB[verb].bg,
                          borderColor: VERB[verb].border,
                          margin: 0,
                        }}
                      >
                        {t(`dashboard.feed.verb.${verb}`)}
                      </Tag>
                      <span className={styles.feedText}>
                        {t(`dashboard.feed.text.${verb}`, {
                          role: entry.roleName ?? '—',
                          person: entry.resourceName ?? '—',
                          project: entry.rootProjectName ?? '—',
                          from: Math.round(entry.previousMagnitude ?? 0),
                          to: Math.round(entry.magnitude ?? 0),
                        })}
                      </span>
                      <span className={styles.feedWhen}>
                        {dayjs(entry.at).format('D MMM HH:mm')}
                      </span>
                    </div>
                  ))}
                </div>
              </div>
            )}
          </>
        )}

        {/* Accepting a risk does NOT hide it: the row stays in the queue,
            labelled, distinct from resolved. The dialog says so. */}
        <Modal
          open={pendingAck !== null}
          title={t('dashboard.acceptDialog.title')}
          okText={t('dashboard.acceptDialog.ok')}
          cancelText={t('common.cancel')}
          confirmLoading={d.isMutating}
          onOk={() => void confirmAck()}
          onCancel={() => {
            setPendingAck(null);
            setReason('');
          }}
        >
          <p>{t('dashboard.acceptDialog.body')}</p>
          <Input
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            placeholder={t('dashboard.acceptDialog.reasonPlaceholder')}
            maxLength={1000}
          />
        </Modal>
      </div>
    </PageContainer>
  );
}
