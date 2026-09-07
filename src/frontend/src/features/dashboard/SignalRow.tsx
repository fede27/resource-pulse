import { useState } from 'react';
import { Button, Tag, Tooltip } from 'antd';
import {
  ArrowRightOutlined,
  CheckOutlined,
  DownOutlined,
  RightOutlined,
} from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { useNavigate } from '@tanstack/react-router';

import { SignalKind, type SignalDto } from '@/api/generated/schemas';
import {
  isAggregate,
  isInlineResolvable,
  linkOf,
  slugOf,
  toneOf,
  zoneSlugOf,
} from './dashboardModel';
import { NO_ZONE_STRIPE, TONE, ZONE_STRIPE } from './signalColors';
import { useStyles } from './SignalRow.styles';

export type SignalRowProps = {
  signal: SignalDto;
  canPlan: boolean;
  busy: boolean;
  onAcknowledge: (signal: SignalDto) => void;
  onReopen: (signal: SignalDto) => void;
  onConfirm: (signal: SignalDto) => void;
};

export function SignalRow({
  signal,
  canPlan,
  busy,
  onAcknowledge,
  onReopen,
  onConfirm,
}: SignalRowProps) {
  const { styles, cx } = useStyles();
  const { t } = useTranslation();
  const navigate = useNavigate();
  const [expanded, setExpanded] = useState(false);

  const tone = TONE[toneOf(signal.kind)];
  const slug = slugOf(signal.kind);
  const zoneSlug = zoneSlugOf(signal.zone);
  const acknowledged = !!signal.isAcknowledged;
  const hasDetail = (signal.contributions?.length ?? 0) > 0;

  // The parts come from the server; the sentence is composed HERE, so it can be
  // translated (ADR-0024 pattern taken to its conclusion).
  const parts = {
    role: signal.roleName ?? '—',
    person: signal.resourceName ?? '—',
    project: signal.rootProjectName ?? '—',
    owner: signal.ownerName ?? t('dashboard.ownerFallback'),
    count: Math.round(signal.magnitude ?? 0),
    hours: Math.round(signal.magnitude ?? 0),
    points: Math.round(signal.magnitude ?? 0),
    zone: zoneSlug ? t(`dashboard.zone.${zoneSlug}`) : '',
    deadline: signal.deadlineAt ?? '',
  };

  const follow = () => {
    const link = linkOf(signal);
    void navigate({ to: link.to });
  };

  return (
    <div className={cx(styles.item, acknowledged && styles.acknowledged)}>
      <div className={styles.row}>
      <div className={styles.gutter}>
        {/* dynamic: zone is an ordered ramp, so the stripe is data. */}
        <span
          className={styles.stripe}
          style={{ background: signal.zone ? ZONE_STRIPE[signal.zone] : NO_ZONE_STRIPE }}
        />
        {signal.isUnseen && (
          <Tooltip title={t('dashboard.unseen')}>
            <span className={styles.unseenDot} />
          </Tooltip>
        )}
      </div>

      <div className={styles.body}>
        <div className={styles.chips}>
          {/* dynamic: the kind's tone. */}
          <Tag style={{ color: tone.fg, background: tone.bg, borderColor: tone.border, margin: 0 }}>
            {t(`dashboard.kind.${slug}`)}
          </Tag>
          {zoneSlug && <Tag style={{ margin: 0 }}>{t(`dashboard.zone.${zoneSlug}`)}</Tag>}
          {acknowledged && (
            <Tag icon={<CheckOutlined />} style={{ margin: 0 }}>
              {t('dashboard.accepted')}
            </Tag>
          )}
        </div>

        <div className={styles.title}>{t(`dashboard.signal.${slug}.title`, parts)}</div>
        <div className={styles.reason}>{t(`dashboard.signal.${slug}.reason`, parts)}</div>

        {isAggregate(signal) && (signal.memberNames?.length ?? 0) > 0 && (
          <div className={styles.members}>{signal.memberNames?.join(' · ')}</div>
        )}

        {acknowledged && signal.acknowledgedReason && (
          <div className={styles.ackNote}>
            {t('dashboard.riskAssumed', { reason: signal.acknowledgedReason })}
          </div>
        )}
      </div>

      <div className={styles.actions}>
        {hasDetail && (
          <Button
            size="small"
            type="text"
            icon={expanded ? <DownOutlined /> : <RightOutlined />}
            onClick={() => setExpanded((v) => !v)}
          >
            {t('dashboard.composition')}
          </Button>
        )}

        {/* The only gesture that completes here: a state change that moves no
            hours. Everything else opens the page where the context is. */}
        {canPlan && !acknowledged && isInlineResolvable(signal) ? (
          <Button size="small" type="primary" loading={busy} onClick={() => onConfirm(signal)}>
            {t('dashboard.confirmAllocation')}
          </Button>
        ) : (
          <Button size="small" icon={<ArrowRightOutlined />} iconPosition="end" onClick={follow}>
            {t(`dashboard.verb.${slug}`)}
          </Button>
        )}

        {/* Hide-don't-disable, at the highest point: a Viewer sees the queue and
            simply has no decision to take on it. */}
        {canPlan &&
          (acknowledged ? (
            <Button size="small" type="text" loading={busy} onClick={() => onReopen(signal)}>
              {t('dashboard.reopen')}
            </Button>
          ) : (
            <Tooltip title={t('dashboard.acceptHint')}>
              <Button size="small" type="text" onClick={() => onAcknowledge(signal)}>
                {t('dashboard.accept')}
              </Button>
            </Tooltip>
          ))}
      </div>
      </div>

      {expanded && hasDetail && (
        <div className={styles.detail}>
          <div className={styles.detailCard}>
            <div className={styles.detailLabel}>{t('dashboard.compositionTitle')}</div>
            <div className={styles.detailList}>
              {signal.contributions?.map((c) => (
                <div key={c.rootProjectId} className={styles.detailItem}>
                  <span>{c.rootProjectName}</span>
                </div>
              ))}
            </div>
            <div className={styles.detailNote}>
              {signal.kind === SignalKind.Overcommit
                ? t('dashboard.compositionNoteOvercommit')
                : t('dashboard.compositionNote')}
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
