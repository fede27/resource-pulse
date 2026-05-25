import { useEffect, useMemo, useRef, useState } from 'react';
import { Button, Card, Checkbox, Empty, Input, Space, Tag, theme, Typography } from 'antd';
import { PlusOutlined, StarFilled } from '@ant-design/icons';
import type { InputRef } from 'antd';
import { useTranslation } from 'react-i18next';
import type { BusinessCalendarReadDto } from '@/api/generated/schemas';
import { useDays } from '@/i18n/useDays';
import { formatPatternSummary, patternSummary, weeklyHours } from './workWindow.utils';

const { Text } = Typography;

export type CalendarListProps = {
  calendars: BusinessCalendarReadDto[];
  selectedId: string | null;
  hasDefault: boolean;
  creating: boolean;
  submitting: boolean;
  onSelect: (id: string) => void;
  onStartCreate: () => void;
  onCancelCreate: () => void;
  onCreate: (input: { name: string; isDefault: boolean }) => void;
};

export function CalendarList({
  calendars,
  selectedId,
  hasDefault,
  creating,
  submitting,
  onSelect,
  onStartCreate,
  onCancelCreate,
  onCreate,
}: CalendarListProps) {
  const { t } = useTranslation();
  const { token } = theme.useToken();
  const days = useDays();
  const [name, setName] = useState('');
  const [isDefault, setIsDefault] = useState(false);
  const [nameError, setNameError] = useState<string | null>(null);
  const inputRef = useRef<InputRef | null>(null);

  const patternFallbacks = useMemo(
    () => ({
      empty: t('timeConfig.calendars.noActiveWindows'),
      variable: t('timeConfig.calendars.patternVariable'),
    }),
    [t],
  );

  useEffect(() => {
    if (creating) {
      setName('');
      setIsDefault(false);
      setNameError(null);
      const handle = window.setTimeout(() => inputRef.current?.focus(), 0);
      return () => window.clearTimeout(handle);
    }
    return undefined;
  }, [creating]);

  const submit = () => {
    const trimmed = name.trim();
    if (!trimmed) {
      setNameError(t('timeConfig.calendars.nameRequired'));
      return;
    }
    if (trimmed.length < 2) {
      setNameError(t('timeConfig.calendars.nameTooShort'));
      return;
    }
    onCreate({ name: trimmed, isDefault });
  };

  return (
    <Card
      size="small"
      styles={{ body: { padding: 0 } }}
      title={
        <span style={{ fontWeight: 600 }}>
          {t('timeConfig.calendars.listTitle')}{' '}
          <Text type="secondary">· {calendars.length}</Text>
        </span>
      }
      extra={
        !creating && (
          <Button
            type="primary"
            size="small"
            icon={<PlusOutlined />}
            onClick={onStartCreate}
          >
            {t('timeConfig.calendars.newButton')}
          </Button>
        )
      }
    >
      {creating && (
        <div
          style={{
            padding: 14,
            background: token.colorFillQuaternary,
            borderBottom: `1px solid ${token.colorBorderSecondary}`,
          }}
        >
          <Input
            ref={inputRef}
            value={name}
            placeholder={t('timeConfig.calendars.namePlaceholder')}
            status={nameError ? 'error' : ''}
            onChange={(e) => {
              setName(e.target.value);
              if (nameError) setNameError(null);
            }}
            onPressEnter={submit}
            onKeyDown={(e) => {
              if (e.key === 'Escape') onCancelCreate();
            }}
          />
          {nameError && (
            <div style={{ color: token.colorError, fontSize: 12, marginTop: 4 }}>
              {nameError}
            </div>
          )}
          <div style={{ marginTop: 10 }}>
            <Checkbox checked={isDefault} onChange={(e) => setIsDefault(e.target.checked)}>
              {t('timeConfig.calendars.setAsDefault')}
            </Checkbox>
            {isDefault && hasDefault && (
              <div
                style={{
                  marginTop: 6,
                  fontSize: 12,
                  color: token.colorTextTertiary,
                  marginLeft: 24,
                }}
              >
                {t('timeConfig.calendars.previousDefaultUnset')}
              </div>
            )}
          </div>
          <div style={{ marginTop: 12, display: 'flex', justifyContent: 'flex-end', gap: 8 }}>
            <Button size="small" onClick={onCancelCreate} disabled={submitting}>
              {t('common.cancel')}
            </Button>
            <Button size="small" type="primary" loading={submitting} onClick={submit}>
              {t('common.create')}
            </Button>
          </div>
        </div>
      )}

      {calendars.length === 0 && !creating ? (
        <div style={{ padding: 24 }}>
          <Empty
            image={Empty.PRESENTED_IMAGE_SIMPLE}
            description={t('timeConfig.calendars.noneTitle')}
          >
            <Button type="primary" icon={<PlusOutlined />} onClick={onStartCreate}>
              {t('timeConfig.calendars.newCalendarButton')}
            </Button>
          </Empty>
        </div>
      ) : (
        <div>
          {calendars.map((c) => {
            const id = c.id ?? '';
            const selected = id === selectedId;
            const hours = weeklyHours(c.workWindows ?? []);
            const summaryStr = formatPatternSummary(
              patternSummary(c.workWindows ?? []),
              days.short,
              patternFallbacks,
            );
            const winCount = (c.workWindows ?? []).length;
            return (
              <div
                key={id || c.name}
                onClick={() => id && onSelect(id)}
                style={{
                  position: 'relative',
                  padding: '14px 16px',
                  cursor: 'pointer',
                  borderBottom: `1px solid ${token.colorBorderSecondary}`,
                  background: selected ? token.colorPrimaryBg : 'transparent',
                  transition: `background ${token.motionDurationFast}`,
                }}
              >
                {selected && (
                  <span
                    style={{
                      position: 'absolute',
                      left: 0,
                      top: 0,
                      bottom: 0,
                      width: 3,
                      background: token.colorPrimary,
                    }}
                  />
                )}
                <Space size={8} align="center" style={{ marginBottom: 4 }}>
                  <span style={{ fontWeight: 500 }}>{c.name ?? '—'}</span>
                  {c.isDefault && (
                    <Tag color="gold" icon={<StarFilled />} style={{ margin: 0 }}>
                      {t('timeConfig.calendars.defaultBadge')}
                    </Tag>
                  )}
                </Space>
                <div style={{ fontSize: 12, color: token.colorTextTertiary, lineHeight: 1.5 }}>
                  {summaryStr}
                </div>
                <div
                  style={{
                    fontSize: 11,
                    color: token.colorTextTertiary,
                    marginTop: 4,
                    fontVariantNumeric: 'tabular-nums',
                  }}
                >
                  {formatHours(hours)} {t('timeConfig.calendars.perWeekHours')} ·{' '}
                  {t('timeConfig.calendars.windowsCount', { count: winCount })}
                </div>
              </div>
            );
          })}
        </div>
      )}
    </Card>
  );
}

function formatHours(h: number): string {
  return h % 1 === 0 ? String(h) : h.toFixed(1);
}
