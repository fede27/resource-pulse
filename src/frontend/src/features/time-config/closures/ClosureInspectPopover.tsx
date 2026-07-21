import { useEffect, useRef } from 'react';
import { Button } from 'antd';
import { CloseOutlined, DeleteOutlined, EditOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import type { CompanyClosureReadDto } from '@/api/generated/schemas';
import { closureDays, formatClosureRange } from './closure.utils';
import { ClosureStatusPill } from './ClosureStatusPill';
import {
  POPOVER_HEIGHT_EST,
  POPOVER_WIDTH,
  useStyles,
} from './ClosureInspectPopover.styles';

export type ClosureInspectAnchor = { x: number; y: number };

export type ClosureInspectPopoverProps = {
  closure: CompanyClosureReadDto;
  anchor: ClosureInspectAnchor;
  onClose: () => void;
  onEdit: (closure: CompanyClosureReadDto) => void;
  onDelete: (closure: CompanyClosureReadDto) => void;
};

const MARGIN = 12;

export function ClosureInspectPopover({
  closure,
  anchor,
  onClose,
  onEdit,
  onDelete,
}: ClosureInspectPopoverProps) {
  const { t } = useTranslation();
  const { styles } = useStyles();
  const ref = useRef<HTMLDivElement>(null);

  // Dismiss on outside click / Escape. Deferred attach so the click that opened
  // the popover doesn't immediately close it.
  useEffect(() => {
    const onDown = (e: MouseEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node)) onClose();
    };
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
    };
    const timer = window.setTimeout(() => {
      document.addEventListener('mousedown', onDown);
      document.addEventListener('keydown', onKey);
    }, 0);
    return () => {
      window.clearTimeout(timer);
      document.removeEventListener('mousedown', onDown);
      document.removeEventListener('keydown', onKey);
    };
  }, [onClose]);

  const { top, left } = clampPosition(anchor);
  const days = closureDays(closure.dateFrom, closure.dateTo);

  return (
    // dynamic: fixed position tracks the click coordinates, clamped to viewport.
    <div ref={ref} className={styles.card} style={{ top, left }}>
      <div className={styles.header}>
        <span className={styles.title}>{closure.reason ?? '—'}</span>
        <span className={styles.close} onClick={onClose}>
          <CloseOutlined />
        </span>
      </div>

      <div className={styles.body}>
        <div className={styles.factRow}>
          <span className={styles.factLabel}>{t('timeConfig.closures.inspect.period')}</span>
          <span className={styles.factValue}>
            <strong>{formatClosureRange(closure.dateFrom, closure.dateTo)}</strong>
          </span>
        </div>
        <div className={styles.factRow}>
          <span className={styles.factLabel}>{t('timeConfig.closures.inspect.duration')}</span>
          <span className={styles.factValue}>
            {t('timeConfig.closures.form.days', { count: days })}{' '}
            <span className={styles.muted}>
              {t('timeConfig.closures.inspect.durationSuffixIncl')}
            </span>
          </span>
        </div>
        <div className={styles.stateRow}>
          <span className={styles.factLabel}>{t('timeConfig.closures.inspect.state')}</span>
          <ClosureStatusPill closure={closure} />
        </div>
        <div className={styles.note}>{t('timeConfig.closures.inspect.note')}</div>
      </div>

      <div className={styles.footer}>
        <Button
          size="small"
          type="text"
          danger
          icon={<DeleteOutlined />}
          onClick={() => {
            onClose();
            onDelete(closure);
          }}
        >
          {t('common.delete')}
        </Button>
        <Button
          size="small"
          type="primary"
          icon={<EditOutlined />}
          onClick={() => {
            onClose();
            onEdit(closure);
          }}
        >
          {t('common.edit')}
        </Button>
      </div>
    </div>
  );
}

function clampPosition(anchor: ClosureInspectAnchor): { top: number; left: number } {
  const vw = window.innerWidth;
  const vh = window.innerHeight;
  let left = anchor.x + MARGIN;
  let top = anchor.y;
  if (left + POPOVER_WIDTH > vw - MARGIN) left = anchor.x - POPOVER_WIDTH - MARGIN;
  if (left < MARGIN) left = MARGIN;
  if (top + POPOVER_HEIGHT_EST > vh - MARGIN) {
    top = Math.max(MARGIN, vh - POPOVER_HEIGHT_EST - MARGIN);
  }
  if (top < MARGIN) top = MARGIN;
  return { top, left };
}
