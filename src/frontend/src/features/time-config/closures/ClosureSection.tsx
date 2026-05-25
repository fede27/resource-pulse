import { useState, type ReactNode } from 'react';
import { Card, Dropdown, Table, Tag, theme, Typography } from 'antd';
import { CaretRightFilled, MoreOutlined } from '@ant-design/icons';
import type { ColumnsType } from 'antd/es/table';
import dayjs from 'dayjs';
import { useTranslation } from 'react-i18next';
import type { CompanyClosureReadDto } from '@/api/generated/schemas';

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
  const { token } = theme.useToken();
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
        <span style={{ fontVariantNumeric: 'tabular-nums' }}>
          {formatRange(c.dateFrom, c.dateTo)}
        </span>
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
        <span style={{ fontVariantNumeric: 'tabular-nums' }}>
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
          <span
            style={{
              display: 'inline-flex',
              padding: 4,
              cursor: 'pointer',
              color: token.colorTextTertiary,
            }}
            onClick={(e) => e.stopPropagation()}
          >
            <MoreOutlined />
          </span>
        </Dropdown>
      ),
    },
  ];

  return (
    <Card
      size="small"
      style={{ overflow: 'hidden' }}
      styles={{ body: { padding: 0 } }}
      title={
        <div
          onClick={() => setOpen((o) => !o)}
          style={{ display: 'flex', alignItems: 'center', gap: 8, cursor: 'pointer' }}
        >
          <CaretRightFilled
            style={{
              transition: `transform ${token.motionDurationFast}`,
              transform: showBody ? 'rotate(90deg)' : 'none',
              color: token.colorTextTertiary,
              fontSize: 10,
            }}
          />
          <span
            style={{
              width: 8,
              height: 8,
              borderRadius: '50%',
              background:
                status === 'upcoming' ? token.colorPrimary : token.colorTextDisabled,
              display: 'inline-block',
            }}
          />
          <Text strong>{title}</Text>
          <Text type="secondary" style={{ fontVariantNumeric: 'tabular-nums' }}>
            · {closures.length}
          </Text>
        </div>
      }
    >
      {showBody && (
        <>
          {inlineSlot && (
            <div
              style={{
                padding: 12,
                background: token.colorFillQuaternary,
                borderBottom: `1px solid ${token.colorBorderSecondary}`,
              }}
            >
              {inlineSlot}
            </div>
          )}
          {visibleRows.length > 0 && (
            <Table<CompanyClosureReadDto>
              rowKey={(c) => c.id ?? `${c.dateFrom}-${c.dateTo}`}
              columns={columns}
              dataSource={visibleRows}
              pagination={false}
              size="small"
              onRow={(c) => ({
                style: { cursor: c.id ? 'pointer' : 'default' },
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
