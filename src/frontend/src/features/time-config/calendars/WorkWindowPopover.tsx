import { useEffect, useState } from 'react';
import { Button, Collapse, DatePicker, Form, Select, Space, Tag, TimePicker, Typography } from 'antd';
import { DeleteOutlined, EditOutlined } from '@ant-design/icons';
import dayjs, { type Dayjs } from 'dayjs';
import { useTranslation } from 'react-i18next';
import type { DayOfWeek, WorkWindowDto } from '@/api/generated/schemas';
import { useDays } from '@/i18n/useDays';
import {
  columnIndexToDayOfWeek,
  dayOfWeekToColumnIndex,
  formatHourMinute,
  isWindowFuture,
  isWindowHistorical,
  timeToMinutes,
} from './workWindow.utils';
import type { WorkWindowFormValues } from './workWindowForm';
import { useStyles } from './WorkWindowPopover.styles';

const { Text } = Typography;

/**
 * `inspect` shows a read-only facts view first (Modifica/Elimina), `edit` opens
 * the form directly. Ignored in create mode (no facts to inspect). Default
 * `edit` preserves the create/direct-edit callers.
 */
export type WorkWindowPopoverStartMode = 'inspect' | 'edit';

export type WorkWindowPopoverContentProps = {
  initial: Partial<WorkWindowDto> | null;
  saving: boolean;
  deleting: boolean;
  startMode?: WorkWindowPopoverStartMode;
  onSubmit: (values: WorkWindowFormValues) => void;
  onCancel: () => void;
  onDelete?: () => void;
};

const HMS = 'HH:mm:ss';

function toDayjsTime(t: string | undefined, fallback: string): Dayjs {
  return dayjs(t ?? fallback, HMS);
}

export function WorkWindowPopoverContent({
  initial,
  saving,
  deleting,
  startMode = 'edit',
  onSubmit,
  onCancel,
  onDelete,
}: WorkWindowPopoverContentProps) {
  const { t } = useTranslation();
  const { styles } = useStyles();
  const days = useDays();
  const [form] = Form.useForm<WorkWindowFormValues>();
  const isEdit = !!initial?.id;

  // Inspect-first only applies to an existing window. The popovers hosting this
  // content use `destroyOnHidden`, so it mounts fresh per record — the initial
  // state is enough, no reset effect needed.
  const [mode, setMode] = useState<WorkWindowPopoverStartMode>(
    isEdit ? startMode : 'edit',
  );

  const initialDayOfWeek = (initial?.dayOfWeek ?? 1) as DayOfWeek;
  const initialValues: WorkWindowFormValues = {
    dayOfWeek: initialDayOfWeek,
    startTime: toDayjsTime(initial?.startTime, '09:00:00'),
    endTime: toDayjsTime(initial?.endTime, '13:00:00'),
    validFrom: initial?.validFrom ? dayjs(initial.validFrom) : dayjs().startOf('day'),
    validTo: initial?.validTo ? dayjs(initial.validTo) : null,
  };

  // Reset only when the popover is reused for a different record. In create
  // mode `id` is undefined so this runs once on mount; in edit mode it runs
  // when the user switches to a different window. Listening on the time
  // fields would wipe the user's in-flight edits on any parent re-render.
  useEffect(() => {
    form.resetFields();
  }, [form, initial?.id]);

  if (isEdit && mode === 'inspect') {
    return (
      <WorkWindowInspect
        window={initial as WorkWindowDto}
        onEdit={() => setMode('edit')}
        onDelete={onDelete}
      />
    );
  }

  const hasNonDefaultValidity =
    !!initial?.validTo ||
    !!(initial?.validFrom && !dayjs(initial.validFrom).isSame(dayjs().startOf('day'), 'day'));

  // AntD pickers default to portaling their dropdowns to <body>. Inside a
  // Popover that listens for outside clicks, a click on the picker dropdown
  // gets treated as an outside click and dismisses the Popover before the
  // value commits. Anchor every picker's dropdown to a sibling element inside
  // the form so the click is seen as inside.
  const getPopupContainer = (triggerNode: HTMLElement): HTMLElement =>
    triggerNode.parentElement ?? document.body;

  return (
    <Form<WorkWindowFormValues>
      form={form}
      layout="vertical"
      size="small"
      initialValues={initialValues}
      onFinish={onSubmit}
      className={styles.form}
    >
      <Form.Item
        label={t('timeConfig.calendars.window.day')}
        name="dayOfWeek"
        rules={[{ required: true, message: t('common.required') }]}
      >
        <Select<DayOfWeek>
          getPopupContainer={getPopupContainer}
          options={days.long.map((label, idx) => ({
            label,
            value: columnIndexToDayOfWeek(idx),
          }))}
        />
      </Form.Item>

      <Form.Item label={t('timeConfig.calendars.window.time')} required>
        <Space.Compact className={styles.fullWidth}>
          <Form.Item
            name="startTime"
            noStyle
            rules={[{ required: true, message: t('common.required') }]}
          >
            <TimePicker
              format="HH:mm"
              minuteStep={15}
              className={styles.half}
              needConfirm={false}
              getPopupContainer={getPopupContainer}
            />
          </Form.Item>
          <Form.Item
            name="endTime"
            noStyle
            dependencies={['startTime']}
            rules={[
              { required: true, message: t('common.required') },
              ({ getFieldValue }) => ({
                validator(_, value: Dayjs | undefined) {
                  const start = getFieldValue('startTime') as Dayjs | undefined;
                  if (!value || !start) return Promise.resolve();
                  const sMin = timeToMinutes(start.format(HMS));
                  const eMin = timeToMinutes(value.format(HMS));
                  if (eMin <= sMin) {
                    return Promise.reject(
                      new Error(t('timeConfig.calendars.window.endBeforeStart')),
                    );
                  }
                  return Promise.resolve();
                },
              }),
            ]}
          >
            <TimePicker
              format="HH:mm"
              minuteStep={15}
              className={styles.half}
              needConfirm={false}
              getPopupContainer={getPopupContainer}
            />
          </Form.Item>
        </Space.Compact>
        <Text type="secondary" className={styles.caption}>
          {t('timeConfig.calendars.window.lunchBreakHint')}
        </Text>
      </Form.Item>

      <Collapse
        ghost
        size="small"
        defaultActiveKey={hasNonDefaultValidity ? ['validity'] : []}
        items={[
          {
            key: 'validity',
            label: <Text type="secondary">{t('timeConfig.calendars.window.validity')}</Text>,
            // Mount the validity fields even when collapsed so AntD Form
            // applies initialValues and tracks them in the values object.
            // Without this, a collapsed panel skips field registration and
            // submit receives `validFrom: undefined`.
            forceRender: true,
            children: (
              <>
                <Form.Item
                  label={t('timeConfig.calendars.window.validFrom')}
                  name="validFrom"
                  rules={[{ required: true, message: t('common.required') }]}
                >
                  <DatePicker
                    format="DD/MM/YYYY"
                    className={styles.fullWidth}
                    getPopupContainer={getPopupContainer}
                  />
                </Form.Item>
                <Form.Item
                  label={t('timeConfig.calendars.window.validTo')}
                  name="validTo"
                  dependencies={['validFrom']}
                  rules={[
                    ({ getFieldValue }) => ({
                      validator(_, value: Dayjs | null | undefined) {
                        if (!value) return Promise.resolve();
                        const from = getFieldValue('validFrom') as Dayjs | undefined;
                        if (from && !value.isAfter(from, 'day')) {
                          return Promise.reject(
                            new Error(t('timeConfig.calendars.window.validToAfterFrom')),
                          );
                        }
                        return Promise.resolve();
                      },
                    }),
                  ]}
                >
                  <DatePicker
                    format="DD/MM/YYYY"
                    className={styles.fullWidth}
                    allowClear
                    getPopupContainer={getPopupContainer}
                  />
                </Form.Item>
                <Text type="secondary" className={styles.caption}>
                  {t('timeConfig.calendars.window.validityHint')}
                </Text>
              </>
            ),
          },
        ]}
      />

      <div className={styles.footer}>
        <div>
          {isEdit && onDelete && (
            <Button
              danger
              type="text"
              size="small"
              icon={<DeleteOutlined />}
              loading={deleting}
              onClick={onDelete}
            >
              {t('common.delete')}
            </Button>
          )}
        </div>
        <Space size={6}>
          <Button size="small" onClick={onCancel} disabled={saving || deleting}>
            {t('common.cancel')}
          </Button>
          <Button size="small" type="primary" htmlType="submit" loading={saving}>
            {isEdit ? t('common.save') : t('common.add')}
          </Button>
        </Space>
      </div>
    </Form>
  );
}

function WorkWindowInspect({
  window: w,
  onEdit,
  onDelete,
}: {
  window: WorkWindowDto;
  onEdit: () => void;
  onDelete: (() => void) | undefined;
}) {
  const { t } = useTranslation();
  const { styles } = useStyles();
  const days = useDays();

  const dayLabel = days.long[dayOfWeekToColumnIndex(w.dayOfWeek)] ?? '';
  const durationHours =
    (timeToMinutes(w.endTime) - timeToMinutes(w.startTime)) / 60;
  const durationLabel = durationHours % 1 === 0 ? String(durationHours) : durationHours.toFixed(1);

  const kind = isWindowHistorical(w) ? 'past' : isWindowFuture(w) ? 'future' : 'active';
  const stateTag =
    kind === 'past'
      ? { color: 'default' as const, label: t('timeConfig.calendars.window.inspect.statePast') }
      : kind === 'future'
        ? { color: 'gold' as const, label: t('timeConfig.calendars.window.inspect.stateFuture') }
        : { color: 'green' as const, label: t('timeConfig.calendars.window.inspect.stateActive') };

  return (
    <div className={styles.inspect}>
      <div className={styles.inspectTitle}>
        {t('timeConfig.calendars.window.inspect.title', { day: dayLabel })}
      </div>

      <div className={styles.factRow}>
        <span className={styles.factLabel}>{t('timeConfig.calendars.window.time')}</span>
        <span className={styles.factValue}>
          <strong>
            {formatHourMinute(w.startTime)}–{formatHourMinute(w.endTime)}
          </strong>{' '}
          · {durationLabel}
          {t('timeConfig.calendars.window.inspect.hoursSuffix')}
        </span>
      </div>
      <div className={styles.factRow}>
        <span className={styles.factLabel}>
          {t('timeConfig.calendars.window.inspect.validFromFact')}
        </span>
        <span className={styles.factValue}>
          {formatValidity(w.validFrom)}{' '}
          <span className={styles.muted}>
            {t('timeConfig.calendars.window.inspect.included')}
          </span>
        </span>
      </div>
      <div className={styles.factRow}>
        <span className={styles.factLabel}>
          {t('timeConfig.calendars.window.inspect.validToFact')}
        </span>
        <span className={styles.factValue}>
          {w.validTo ? (
            <>
              {formatValidity(w.validTo)}{' '}
              <span className={styles.muted}>
                {t('timeConfig.calendars.window.inspect.excluded')}
              </span>
            </>
          ) : (
            <span className={styles.muted}>
              {t('timeConfig.calendars.window.inspect.indefinite')}
            </span>
          )}
        </span>
      </div>
      <div className={styles.stateRow}>
        <span className={styles.factLabel}>
          {t('timeConfig.calendars.window.inspect.state')}
        </span>
        <Tag color={stateTag.color}>{stateTag.label}</Tag>
      </div>

      <div className={styles.note}>{t('timeConfig.calendars.window.inspect.note')}</div>

      <div className={styles.footer}>
        <div>
          {onDelete && (
            <Button
              danger
              type="text"
              size="small"
              icon={<DeleteOutlined />}
              onClick={onDelete}
            >
              {t('common.delete')}
            </Button>
          )}
        </div>
        <Button size="small" type="primary" icon={<EditOutlined />} onClick={onEdit}>
          {t('common.edit')}
        </Button>
      </div>
    </div>
  );
}

function formatValidity(iso: string | undefined): string {
  return iso ? dayjs(iso).format('D MMM YYYY') : '—';
}
