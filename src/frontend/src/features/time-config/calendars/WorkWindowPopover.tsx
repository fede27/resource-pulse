import { useEffect } from 'react';
import { Button, Collapse, DatePicker, Form, Select, Space, TimePicker, Typography } from 'antd';
import { DeleteOutlined } from '@ant-design/icons';
import dayjs, { type Dayjs } from 'dayjs';
import { useTranslation } from 'react-i18next';
import type { DayOfWeek, WorkWindowDto } from '@/api/generated/schemas';
import { useDays } from '@/i18n/useDays';
import {
  columnIndexToDayOfWeek,
  dayOfWeekToColumnIndex,
  minutesToTime,
  timeToMinutes,
} from './workWindow.utils';

const { Text } = Typography;

export type WorkWindowFormValues = {
  dayOfWeek: DayOfWeek;
  startTime: Dayjs;
  endTime: Dayjs;
  validFrom: Dayjs;
  validTo: Dayjs | null;
};

export type WorkWindowPopoverContentProps = {
  initial: Partial<WorkWindowDto> | null;
  saving: boolean;
  deleting: boolean;
  onSubmit: (values: WorkWindowFormValues) => void;
  onCancel: () => void;
  onDelete?: () => void;
};

const HMS = 'HH:mm:ss';

function toDayjsTime(t: string | undefined, fallback: string): Dayjs {
  return dayjs(t ?? fallback, HMS);
}

export function formValuesToDto(v: WorkWindowFormValues, id?: string): WorkWindowDto {
  const startMin = v.startTime.hour() * 60 + v.startTime.minute();
  const endMin = v.endTime.hour() * 60 + v.endTime.minute();
  // Defensive fallbacks: when the "Validity period" panel is never opened, the
  // Form may have no value for these fields. Default to "active from today,
  // indefinitely" — matches what the collapsed panel visually communicates.
  const validFrom = (v.validFrom ?? dayjs().startOf('day')).format('YYYY-MM-DD');
  const validTo = v.validTo ? v.validTo.format('YYYY-MM-DD') : null;
  const dto: WorkWindowDto = {
    dayOfWeek: v.dayOfWeek,
    startTime: minutesToTime(startMin),
    endTime: minutesToTime(endMin),
    validFrom,
    validTo,
  };
  if (id !== undefined) dto.id = id;
  return dto;
}

export function WorkWindowPopoverContent({
  initial,
  saving,
  deleting,
  onSubmit,
  onCancel,
  onDelete,
}: WorkWindowPopoverContentProps) {
  const { t } = useTranslation();
  const days = useDays();
  const [form] = Form.useForm<WorkWindowFormValues>();
  const isEdit = !!initial?.id;

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
      style={{ width: 300 }}
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
        <Space.Compact style={{ width: '100%' }}>
          <Form.Item
            name="startTime"
            noStyle
            rules={[{ required: true, message: t('common.required') }]}
          >
            <TimePicker
              format="HH:mm"
              minuteStep={15}
              style={{ width: '50%' }}
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
              style={{ width: '50%' }}
              needConfirm={false}
              getPopupContainer={getPopupContainer}
            />
          </Form.Item>
        </Space.Compact>
        <Text type="secondary" style={{ fontSize: 12 }}>
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
                    style={{ width: '100%' }}
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
                    style={{ width: '100%' }}
                    allowClear
                    getPopupContainer={getPopupContainer}
                  />
                </Form.Item>
                <Text type="secondary" style={{ fontSize: 12 }}>
                  {t('timeConfig.calendars.window.validityHint')}
                </Text>
              </>
            ),
          },
        ]}
      />

      <div
        style={{
          marginTop: 12,
          display: 'flex',
          justifyContent: 'space-between',
          gap: 8,
        }}
      >
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

export const dayOfWeekColumnIndex = dayOfWeekToColumnIndex;
