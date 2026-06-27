import { useMemo, useState } from 'react';
import { Button, Card, Checkbox, Empty, Input, Space, Tag, Typography } from 'antd';
import { PlusOutlined, StarFilled } from '@ant-design/icons';
import { createStyles } from 'antd-style';
import { useTranslation } from 'react-i18next';
import type { BusinessCalendarReadDto } from '@/api/generated/schemas';
import { useDays } from '@/i18n/useDays';
import { formatPatternSummary, patternSummary, weeklyHours } from './workWindow.utils';

const { Text } = Typography;

const useStyles = createStyles(({ token, css }) => ({
  titleStrong: css`
    font-weight: 600;
  `,
  emptyWrap: css`
    padding: ${token.paddingLG}px;
  `,
  row: css`
    position: relative;
    padding: 14px ${token.padding}px;
    cursor: pointer;
    border-bottom: 1px solid ${token.colorBorderSecondary};
    background: transparent;
    transition: background ${token.motionDurationFast};
  `,
  rowSelected: css`
    background: ${token.colorPrimaryBg};
  `,
  accent: css`
    position: absolute;
    left: 0;
    top: 0;
    bottom: 0;
    width: 3px;
    background: ${token.colorPrimary};
  `,
  rowHead: css`
    margin-block-end: ${token.marginXXS}px;
  `,
  nameStrong: css`
    font-weight: 500;
  `,
  tagItem: css`
    margin: 0;
  `,
  summary: css`
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextTertiary};
    line-height: 1.5;
  `,
  hours: css`
    font-size: 11px;
    color: ${token.colorTextTertiary};
    margin-block-start: ${token.marginXXS}px;
    font-variant-numeric: tabular-nums;
  `,
  formRoot: css`
    padding: 14px;
    background: ${token.colorFillQuaternary};
    border-bottom: 1px solid ${token.colorBorderSecondary};
  `,
  formError: css`
    color: ${token.colorError};
    font-size: ${token.fontSizeSM}px;
    margin-block-start: ${token.marginXXS}px;
  `,
  checkboxRow: css`
    margin-block-start: ${token.marginSM}px;
  `,
  defaultHint: css`
    margin-block-start: ${token.marginXXS}px;
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextTertiary};
    margin-inline-start: 24px;
  `,
  formFooter: css`
    margin-block-start: ${token.marginSM}px;
    display: flex;
    justify-content: flex-end;
    gap: ${token.marginXS}px;
  `,
}));

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
  const { styles, cx } = useStyles();
  const days = useDays();

  const patternFallbacks = useMemo(
    () => ({
      empty: t('timeConfig.calendars.noActiveWindows'),
      variable: t('timeConfig.calendars.patternVariable'),
    }),
    [t],
  );

  return (
    <Card
      size="small"
      styles={{ body: { padding: 0 } }}
      title={
        <span className={styles.titleStrong}>
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
        <CreateCalendarForm
          hasDefault={hasDefault}
          submitting={submitting}
          onCancel={onCancelCreate}
          onCreate={onCreate}
        />
      )}

      {calendars.length === 0 && !creating ? (
        <div className={styles.emptyWrap}>
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
                className={cx(styles.row, selected && styles.rowSelected)}
              >
                {selected && <span className={styles.accent} />}
                <Space size={8} align="center" className={styles.rowHead}>
                  <span className={styles.nameStrong}>{c.name ?? '—'}</span>
                  {c.isDefault && (
                    <Tag color="gold" icon={<StarFilled />} className={styles.tagItem}>
                      {t('timeConfig.calendars.defaultBadge')}
                    </Tag>
                  )}
                </Space>
                <div className={styles.summary}>{summaryStr}</div>
                <div className={styles.hours}>
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

// Rendered only while `creating`, so it mounts fresh on each open — its local
// fields reset without an effect, and the input self-focuses via autoFocus.
function CreateCalendarForm({
  hasDefault,
  submitting,
  onCancel,
  onCreate,
}: {
  hasDefault: boolean;
  submitting: boolean;
  onCancel: () => void;
  onCreate: (input: { name: string; isDefault: boolean }) => void;
}) {
  const { t } = useTranslation();
  const { styles } = useStyles();
  const [name, setName] = useState('');
  const [isDefault, setIsDefault] = useState(false);
  const [nameError, setNameError] = useState<string | null>(null);

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
    <div className={styles.formRoot}>
      <Input
        autoFocus
        value={name}
        placeholder={t('timeConfig.calendars.namePlaceholder')}
        status={nameError ? 'error' : ''}
        onChange={(e) => {
          setName(e.target.value);
          if (nameError) setNameError(null);
        }}
        onPressEnter={submit}
        onKeyDown={(e) => {
          if (e.key === 'Escape') onCancel();
        }}
      />
      {nameError && <div className={styles.formError}>{nameError}</div>}
      <div className={styles.checkboxRow}>
        <Checkbox checked={isDefault} onChange={(e) => setIsDefault(e.target.checked)}>
          {t('timeConfig.calendars.setAsDefault')}
        </Checkbox>
        {isDefault && hasDefault && (
          <div className={styles.defaultHint}>
            {t('timeConfig.calendars.previousDefaultUnset')}
          </div>
        )}
      </div>
      <div className={styles.formFooter}>
        <Button size="small" onClick={onCancel} disabled={submitting}>
          {t('common.cancel')}
        </Button>
        <Button size="small" type="primary" loading={submitting} onClick={submit}>
          {t('common.create')}
        </Button>
      </div>
    </div>
  );
}

function formatHours(h: number): string {
  return h % 1 === 0 ? String(h) : h.toFixed(1);
}
