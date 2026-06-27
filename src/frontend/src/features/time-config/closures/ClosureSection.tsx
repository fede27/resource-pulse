import { useState, type ReactNode } from 'react';
import { Card, Dropdown, Table, Tag, Typography } from 'antd';
import { CaretRightFilled, MoreOutlined } from '@ant-design/icons';
import type { ColumnsType } from 'antd/es/table';
import { createStyles } from 'antd-style';
import dayjs from 'dayjs';
import { useTranslation } from 'react-i18next';
import type { CompanyClosureReadDto } from '@/api/generated/schemas';

const { Text } = Typography;

const useStyles = createStyles(({ token, css }) => ({
  tnum: css`
    font-variant-numeric: tabular-nums;
  `,
  actionTrigger: css`
    display: inline-flex;
    padding: ${token.paddingXXS}px;
    cursor: pointer;
    color: ${token.colorTextTertiary};
  `,
  card: css`
    overflow: hidden;
  `,
  titleRow: css`
    display: flex;
    align-items: center;
    gap: ${token.marginXS}px;
    cursor: pointer;
  `,
  caret: css`
    transition: transform ${token.motionDurationFast};
    transform: none;
    color: ${token.colorTextTertiary};
    font-size: 10px;
  `,
  caretOpen: css`
    transform: rotate(90deg);
  `,
  statusDot: css`
    width: 8px;
    height: 8px;
    border-radius: 50%;
    display: inline-block;
  `,
  statusUpcoming: css`
    background: ${token.colorPrimary};
  `,
  statusPast: css`
    background: ${token.colorTextDisabled};
  `,
  inlineSlot: css`
    padding: ${token.paddingSM}px;
    background: ${token.colorFillQuaternary};
    border-bottom: 1px solid ${token.colorBorderSecondary};
  `,
  rowClickable: css`
    cursor: pointer;
  `,
}));

export type ClosureSectionStatus = 'upcoming' | 'past';

export type ClosureSectionProps = {
  title: string;
  status: ClosureSectionStatus;
  closures: CompanyClosureReadDto[];
  /** Rendered above the table when present (create or edit form). */
  inlineSlot?: ReactNode;
  /** Row with this id is hidden from the table (because the inline editor took over). */
  hiddenRowId?: string | null;
  defaultOpen?: boolean;
  onEdit: (closure: CompanyClosureReadDto) => void;
  onDelete: (closure: CompanyClosureReadDto) => void;
};

export function ClosureSection({
  title,
  status,
  closures,
  inlineSlot,
  hiddenRowId,
  defaultOpen = true,
  onEdit,
  onDelete,
}: ClosureSectionProps) {
  const { t } = useTranslation();
  const { styles, cx } = useStyles();
  const [open, setOpen] = useState(defaultOpen);
  const showBody = open || !!inlineSlot;

  if (closures.length === 0 && !inlineSlot) return null;

  const visibleRows = hiddenRowId ? closures.filter((c) => c.id !== hiddenRowId) : closures;
  const dayShort = t('timeConfig.closures.dayShort');

  const columns: ColumnsType<CompanyClosureReadDto> = [
    {
      title: t('timeConfig.closures.columnPeriod'),
      dataIndex: 'dateFrom',
      key: 'period',
      render: (_, c) => (
        <span className={styles.tnum}>{formatRange(c.dateFrom, c.dateTo)}</span>
      ),
    },
    {
      title: t('timeConfig.closures.columnReason'),
      dataIndex: 'reason',
      key: 'reason',
      render: (reason: string | undefined) => reason ?? '—',
    },
    {
      title: t('timeConfig.closures.columnDuration'),
      key: 'duration',
      width: 100,
      align: 'right',
      render: (_, c) => (
        <span className={styles.tnum}>
          {computeDays(c.dateFrom, c.dateTo)}
          {dayShort}
        </span>
      ),
    },
    {
      title: t('timeConfig.closures.columnStatus'),
      key: 'status',
      width: 110,
      render: (_, c) => <ClosureStatusTag closure={c} />,
    },
    {
      title: '',
      key: 'actions',
      width: 50,
      render: (_, c) => (
        <Dropdown
          placement="bottomRight"
          menu={{
            items: [
              { key: 'edit', label: t('common.edit'), onClick: () => onEdit(c) },
              { type: 'divider' },
              {
                key: 'delete',
                label: t('common.delete'),
                danger: true,
                onClick: () => onDelete(c),
              },
            ],
          }}
        >
          <span className={styles.actionTrigger} onClick={(e) => e.stopPropagation()}>
            <MoreOutlined />
          </span>
        </Dropdown>
      ),
    },
  ];

  return (
    <Card
      size="small"
      className={styles.card}
      styles={{ body: { padding: 0 } }}
      title={
        <div onClick={() => setOpen((o) => !o)} className={styles.titleRow}>
          <CaretRightFilled className={cx(styles.caret, showBody && styles.caretOpen)} />
          <span
            className={cx(
              styles.statusDot,
              status === 'upcoming' ? styles.statusUpcoming : styles.statusPast,
            )}
          />
          <Text strong>{title}</Text>
          <Text type="secondary" className={styles.tnum}>
            · {closures.length}
          </Text>
        </div>
      }
    >
      {showBody && (
        <>
          {inlineSlot && <div className={styles.inlineSlot}>{inlineSlot}</div>}
          {visibleRows.length > 0 && (
            <Table<CompanyClosureReadDto>
              rowKey={(c) => c.id ?? `${c.dateFrom}-${c.dateTo}`}
              columns={columns}
              dataSource={visibleRows}
              pagination={false}
              size="small"
              onRow={(c) => ({
                className: c.id ? styles.rowClickable : undefined,
                onClick: (e) => {
                  if ((e.target as HTMLElement).closest('.ant-dropdown-trigger')) return;
                  if (c.id) onEdit(c);
                },
              })}
            />
          )}
        </>
      )}
    </Card>
  );
}

function ClosureStatusTag({ closure }: { closure: CompanyClosureReadDto }) {
  const { t } = useTranslation();
  const today = dayjs().startOf('day');
  const from = closure.dateFrom ? dayjs(closure.dateFrom) : null;
  const to = closure.dateTo ? dayjs(closure.dateTo) : null;
  if (
    from &&
    to &&
    (today.isSame(from, 'day') ||
      (today.isAfter(from) && (today.isSame(to, 'day') || today.isBefore(to))))
  ) {
    return <Tag color="red">{t('timeConfig.closures.statusOngoing')}</Tag>;
  }
  if (from && from.isAfter(today, 'day')) {
    return <Tag color="blue">{t('timeConfig.closures.statusUpcoming')}</Tag>;
  }
  return <Tag>{t('timeConfig.closures.statusPast')}</Tag>;
}

function formatRange(fromIso: string | undefined, toIso: string | undefined): string {
  if (!fromIso) return '—';
  const from = dayjs(fromIso);
  const to = toIso ? dayjs(toIso) : from;
  if (from.isSame(to, 'day')) return from.format('DD MMMM YYYY');
  if (from.isSame(to, 'year') && from.isSame(to, 'month')) {
    return `${from.format('D')}–${to.format('D MMMM YYYY')}`;
  }
  if (from.isSame(to, 'year')) {
    return `${from.format('D MMM')} – ${to.format('D MMM YYYY')}`;
  }
  return `${from.format('D MMM YYYY')} – ${to.format('D MMM YYYY')}`;
}

function computeDays(fromIso: string | undefined, toIso: string | undefined): number {
  if (!fromIso) return 0;
  const from = dayjs(fromIso).startOf('day');
  const to = toIso ? dayjs(toIso).startOf('day') : from;
  return Math.max(0, to.diff(from, 'day') + 1);
}
