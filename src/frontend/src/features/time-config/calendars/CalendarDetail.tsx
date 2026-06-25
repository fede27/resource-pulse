import { useState } from 'react';
import {
  Alert,
  App,
  Button,
  Card,
  Dropdown,
  Input,
  Segmented,
  Space,
  Tag,
  theme,
  Typography,
} from 'antd';
import {
  EditOutlined,
  MoreOutlined,
  StarFilled,
  StarOutlined,
} from '@ant-design/icons';
import { useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import {
  getBusinessCalendarsGetAllQueryKey,
  useBusinessCalendarsAddWorkWindow,
  useBusinessCalendarsDelete,
  useBusinessCalendarsMarkAsDefault,
  useBusinessCalendarsRemoveWorkWindow,
  useBusinessCalendarsUpdate,
} from '@/api/generated/business-calendars/business-calendars';
import type { BusinessCalendarReadDto } from '@/api/generated/schemas';
import { useApiError } from '@/lib/errors';
import {
  isWindowActiveToday,
  isWindowFuture,
  isWindowHistorical,
  weeklyHours,
} from './workWindow.utils';
import { WeekGrid, type WeekGridView } from './WeekGrid';
import { formValuesToDto, type WorkWindowFormValues } from './workWindowForm';

const { Title, Text } = Typography;

export type CalendarDetailProps = {
  calendar: BusinessCalendarReadDto;
  onDeleted: () => void;
};

export function CalendarDetail({ calendar, onDeleted }: CalendarDetailProps) {
  const { t } = useTranslation();
  const { token } = theme.useToken();
  const { message, modal } = App.useApp();
  const queryClient = useQueryClient();
  const showApiError = useApiError();

  const calendarId = calendar.id ?? '';
  const windows = calendar.workWindows ?? [];
  const name = calendar.name ?? '';

  // Per-calendar UI state. The parent remounts this component on calendar
  // change (key={calendar.id}), so these initialise fresh — no reset effect.
  const [view, setView] = useState<WeekGridView>('today');
  const [renaming, setRenaming] = useState(false);
  const [tempName, setTempName] = useState(name);

  const startRename = () => {
    setTempName(name);
    setRenaming(true);
  };

  const invalidate = () =>
    queryClient.invalidateQueries({ queryKey: getBusinessCalendarsGetAllQueryKey() });

  const renameMutation = useBusinessCalendarsUpdate({
    mutation: {
      onSuccess: () => {
        message.success(t('timeConfig.calendars.renameSuccess'));
        void invalidate();
        setRenaming(false);
      },
      onError: (e) => showApiError(e),
    },
  });

  const promoteMutation = useBusinessCalendarsMarkAsDefault({
    mutation: {
      onSuccess: () => {
        message.success(t('timeConfig.calendars.promoteSuccess', { name }));
        void invalidate();
      },
      onError: (e) => showApiError(e),
    },
  });

  const deleteMutation = useBusinessCalendarsDelete({
    mutation: {
      onSuccess: () => {
        message.success(t('timeConfig.calendars.deleteSuccess', { name }));
        void invalidate();
        onDeleted();
      },
      onError: (e) => showApiError(e),
    },
  });

  const addWindowMutation = useBusinessCalendarsAddWorkWindow({
    mutation: {
      onSuccess: () => {
        message.success(t('timeConfig.calendars.window.addSuccess'));
        void invalidate();
      },
      onError: (e) => showApiError(e),
    },
  });

  // The backend has POST add + DELETE remove but no in-place PUT for a window:
  // "update" is delete + re-add (the new window gets a fresh id).
  const removeWindowMutation = useBusinessCalendarsRemoveWorkWindow({
    mutation: {
      onError: (e) => showApiError(e),
    },
  });

  const handleCreateWindow = async (values: WorkWindowFormValues) => {
    await addWindowMutation.mutateAsync({ id: calendarId, data: formValuesToDto(values) });
  };

  const handleUpdateWindow = async (windowId: string, values: WorkWindowFormValues) => {
    await removeWindowMutation.mutateAsync({ id: calendarId, windowId });
    await addWindowMutation.mutateAsync({ id: calendarId, data: formValuesToDto(values) });
    message.success(t('timeConfig.calendars.window.updateSuccess'));
  };

  const handleDeleteWindow = async (windowId: string) => {
    await removeWindowMutation.mutateAsync({ id: calendarId, windowId });
    message.success(t('timeConfig.calendars.window.removeSuccess'));
    void invalidate();
  };

  const confirmPromote = () => {
    modal.confirm({
      title: t('timeConfig.calendars.promoteConfirmTitle', { name }),
      content: t('timeConfig.calendars.promoteConfirmBody'),
      okText: t('timeConfig.calendars.promoteConfirmOk'),
      cancelText: t('common.cancel'),
      onOk: () => promoteMutation.mutateAsync({ id: calendarId }).catch(() => undefined),
    });
  };

  const confirmDelete = () => {
    modal.confirm({
      title: t('timeConfig.calendars.deleteConfirmTitle', { name }),
      content:
        windows.length > 0
          ? t('timeConfig.calendars.deleteConfirmBodyWithWindows', {
              count: windows.length,
            })
          : t('timeConfig.calendars.deleteConfirmBodyEmpty'),
      okText: t('timeConfig.calendars.deleteCalendarButton'),
      cancelText: t('common.cancel'),
      okButtonProps: { danger: true },
      onOk: () => deleteMutation.mutateAsync({ id: calendarId }).catch(() => undefined),
    });
  };

  const submitRename = () => {
    const trimmed = tempName.trim();
    if (!trimmed || trimmed === name) {
      setRenaming(false);
      setTempName(name);
      return;
    }
    renameMutation.mutate({ id: calendarId, data: { name: trimmed } });
  };

  const activeCount = windows.filter(isWindowActiveToday).length;
  const futureCount = windows.filter(isWindowFuture).length;
  const historicalCount = windows.filter(isWindowHistorical).length;
  const hours = weeklyHours(windows);

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <Card size="small">
        <div
          style={{
            display: 'flex',
            alignItems: 'flex-start',
            justifyContent: 'space-between',
            gap: 16,
          }}
        >
          <div style={{ flex: 1, minWidth: 0 }}>
            <Space size={10} align="center" wrap style={{ marginBottom: 6 }}>
              {renaming ? (
                <>
                  <Input
                    value={tempName}
                    onChange={(e) => setTempName(e.target.value)}
                    onPressEnter={submitRename}
                    onKeyDown={(e) => {
                      if (e.key === 'Escape') {
                        setRenaming(false);
                        setTempName(name);
                      }
                    }}
                    style={{ maxWidth: 320 }}
                    autoFocus
                  />
                  <Button
                    size="small"
                    type="primary"
                    loading={renameMutation.isPending}
                    onClick={submitRename}
                  >
                    {t('common.save')}
                  </Button>
                  <Button
                    size="small"
                    onClick={() => {
                      setRenaming(false);
                      setTempName(name);
                    }}
                    disabled={renameMutation.isPending}
                  >
                    {t('common.cancel')}
                  </Button>
                </>
              ) : (
                <>
                  <Title level={4} style={{ margin: 0 }}>
                    {name || '—'}
                  </Title>
                  {calendar.isDefault && (
                    <Tag color="gold" icon={<StarFilled />}>
                      {t('timeConfig.calendars.defaultCalendarBadge')}
                    </Tag>
                  )}
                  <Button
                    type="text"
                    size="small"
                    icon={<EditOutlined />}
                    aria-label={t('common.rename')}
                    onClick={startRename}
                  />
                </>
              )}
            </Space>
            <Space size={24} wrap>
              <Metric
                label={t('timeConfig.calendars.metricWeeklyHours')}
                value={`${formatHours(hours)} h`}
              />
              <Metric
                label={t('timeConfig.calendars.metricActiveWindows')}
                value={String(activeCount)}
              />
              <Metric
                label={t('timeConfig.calendars.metricTotalWindows')}
                value={String(windows.length)}
              />
            </Space>
          </div>
          <Space>
            {!calendar.isDefault && (
              <Button
                icon={<StarOutlined />}
                loading={promoteMutation.isPending}
                onClick={confirmPromote}
              >
                {t('timeConfig.calendars.setAsDefault')}
              </Button>
            )}
            <Dropdown
              placement="bottomRight"
              menu={{
                items: [
                  {
                    key: 'rename',
                    label: t('common.rename'),
                    onClick: startRename,
                  },
                  { type: 'divider' },
                  {
                    key: 'delete',
                    label: t('timeConfig.calendars.deleteCalendarButton'),
                    danger: true,
                    onClick: confirmDelete,
                  },
                ],
              }}
            >
              <Button icon={<MoreOutlined />} />
            </Dropdown>
          </Space>
        </div>

        <div
          style={{
            marginTop: 16,
            paddingTop: 16,
            borderTop: `1px solid ${token.colorBorderSecondary}`,
            display: 'flex',
            alignItems: 'center',
            gap: 12,
            flexWrap: 'wrap',
          }}
        >
          <Text type="secondary" style={{ fontSize: 13 }}>
            {t('timeConfig.calendars.view')}
          </Text>
          <Segmented<WeekGridView>
            value={view}
            onChange={(v) => setView(v)}
            options={[
              { label: t('timeConfig.calendars.viewToday'), value: 'today' },
              {
                label: t('timeConfig.calendars.viewAll', { count: windows.length }),
                value: 'all',
                disabled: windows.length === 0,
              },
              {
                label: t('timeConfig.calendars.viewHistorical', { count: historicalCount }),
                value: 'historical',
                disabled: historicalCount === 0,
              },
              {
                label: t('timeConfig.calendars.viewFuture', { count: futureCount }),
                value: 'future',
                disabled: futureCount === 0,
              },
            ]}
          />
        </div>
      </Card>

      {futureCount > 0 && view === 'today' && (
        <Alert
          type="warning"
          showIcon
          message={t('timeConfig.calendars.scheduledChangesTitle')}
          description={
            <>
              {t('timeConfig.calendars.scheduledChangesBody', { count: futureCount })}{' '}
              <Button
                type="link"
                size="small"
                onClick={() => setView('future')}
                style={{ padding: 0 }}
              >
                {t('timeConfig.calendars.scheduledChangesLink')}
              </Button>
            </>
          }
        />
      )}

      <WeekGrid
        windows={windows}
        view={view}
        saving={addWindowMutation.isPending}
        deleting={removeWindowMutation.isPending}
        onCreate={handleCreateWindow}
        onUpdate={handleUpdateWindow}
        onDelete={handleDeleteWindow}
      />
    </div>
  );
}

function Metric({ label, value }: { label: string; value: string }) {
  return (
    <span>
      <Text type="secondary" style={{ fontSize: 13 }}>
        {label}
      </Text>{' '}
      ·{' '}
      <strong style={{ fontVariantNumeric: 'tabular-nums' }}>{value}</strong>
    </span>
  );
}

function formatHours(h: number): string {
  return h % 1 === 0 ? String(h) : h.toFixed(1);
}
