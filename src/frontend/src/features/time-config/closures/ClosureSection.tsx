import { useState, type ReactNode } from 'react';
import { Card, Dropdown, Table, Typography } from 'antd';
import { CaretRightFilled, MoreOutlined } from '@ant-design/icons';
import type { ColumnsType } from 'antd/es/table';
import { useTranslation } from 'react-i18next';
import type { CompanyClosureReadDto } from '@/api/generated/schemas';
import { closureDays, formatClosureRange } from './closure.utils';
import { ClosureStatusPill } from './ClosureStatusPill';
import type { ClosureInspectAnchor } from './ClosureInspectPopover';
import { useStyles } from './ClosureSection.styles';

const { Text } = Typography;

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
  onInspect: (closure: CompanyClosureReadDto, anchor: ClosureInspectAnchor) => void;
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
  onInspect,
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
        <span className={styles.tnum}>{formatClosureRange(c.dateFrom, c.dateTo)}</span>
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
          {closureDays(c.dateFrom, c.dateTo)}
          {dayShort}
        </span>
      ),
    },
    {
      title: t('timeConfig.closures.columnStatus'),
      key: 'status',
      width: 110,
      render: (_, c) => <ClosureStatusPill closure={c} />,
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
                  if (c.id) onInspect(c, { x: e.clientX, y: e.clientY });
                },
              })}
            />
          )}
        </>
      )}
    </Card>
  );
}
