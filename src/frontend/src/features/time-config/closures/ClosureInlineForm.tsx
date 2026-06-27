import { useEffect } from 'react';
import { Button, Card, DatePicker, Form, Input, Radio, Space, Tag, Typography } from 'antd';
import { DeleteOutlined } from '@ant-design/icons';
import dayjs, { type Dayjs } from 'dayjs';
import { useTranslation } from 'react-i18next';
import type { CompanyClosureReadDto } from '@/api/generated/schemas';
import { useStyles } from './ClosureInlineForm.styles';

const { Text } = Typography;

export type ClosureFormValues = {
  reason: string;
  kind: 'single' | 'range';
  dateFrom: Dayjs;
  dateTo: Dayjs;
};

export type ClosureInlineFormProps = {
  initial?: CompanyClosureReadDto;
  yearHint: number;
  saving: boolean;
  deleting?: boolean;
  onSubmit: (values: ClosureFormValues) => void;
  onCancel: () => void;
  onDelete?: () => void;
};

export function ClosureInlineForm({
  initial,
  yearHint,
  saving,
  deleting,
  onSubmit,
  onCancel,
  onDelete,
}: ClosureInlineFormProps) {
  const { t } = useTranslation();
  const { styles } = useStyles();
  const [form] = Form.useForm<ClosureFormValues>();
  const isEdit = !!initial?.id;

  const defaultDate =
    yearHint === dayjs().year() ? dayjs().startOf('day') : dayjs(`${yearHint}-01-01`);

  const initialValues: ClosureFormValues = {
    reason: initial?.reason ?? '',
    kind: initial && initial.dateFrom !== initial.dateTo ? 'range' : 'single',
    dateFrom: initial?.dateFrom ? dayjs(initial.dateFrom) : defaultDate,
    dateTo: initial?.dateTo ? dayjs(initial.dateTo) : defaultDate,
  };

  useEffect(() => {
    form.resetFields();
  }, [form, initial?.id]);

  const handleFinish = (values: ClosureFormValues) => {
    onSubmit({
      ...values,
      reason: values.reason.trim(),
      dateTo: values.kind === 'single' ? values.dateFrom : values.dateTo,
    });
  };

  return (
    <Card size="small" className={styles.card}>
      <Form<ClosureFormValues>
        form={form}
        layout="vertical"
        size="small"
        initialValues={initialValues}
        onFinish={handleFinish}
      >
        <div className={styles.header}>
          <Text strong>
            {isEdit
              ? t('timeConfig.closures.form.editTitle')
              : t('timeConfig.closures.form.newTitle')}
          </Text>
        </div>

        <Form.Item
          name="reason"
          rules={[
            { required: true, message: t('timeConfig.closures.form.reasonRequired') },
            { max: 200, message: t('timeConfig.closures.form.reasonMaxLength') },
          ]}
          className={styles.fieldMd}
        >
          <Input placeholder={t('timeConfig.closures.form.reasonPlaceholder')} autoFocus />
        </Form.Item>

        <Form.Item name="kind" className={styles.fieldSm}>
          <Radio.Group>
            <Radio value="single">{t('timeConfig.closures.form.kindSingle')}</Radio>
            <Radio value="range">{t('timeConfig.closures.form.kindRange')}</Radio>
          </Radio.Group>
        </Form.Item>

        <Form.Item dependencies={['kind', 'dateFrom', 'dateTo']} className={styles.fieldSm}>
          {({ getFieldValue }) => {
            const kind = getFieldValue('kind') as 'single' | 'range';
            const from = getFieldValue('dateFrom') as Dayjs | undefined;
            const to = getFieldValue('dateTo') as Dayjs | undefined;
            const days =
              kind === 'single'
                ? 1
                : from && to
                  ? Math.max(0, to.startOf('day').diff(from.startOf('day'), 'day') + 1)
                  : 0;
            return (
              <Space wrap>
                <Form.Item
                  name="dateFrom"
                  noStyle
                  rules={[
                    { required: true, message: t('timeConfig.closures.form.dateRequired') },
                  ]}
                >
                  <DatePicker format="DD/MM/YYYY" allowClear={false} />
                </Form.Item>
                {kind === 'range' && (
                  <>
                    <Text type="secondary">→</Text>
                    <Form.Item
                      name="dateTo"
                      noStyle
                      dependencies={['dateFrom']}
                      rules={[
                        { required: true, message: t('timeConfig.closures.form.dateRequired') },
                        ({ getFieldValue: gv }) => ({
                          validator(_, value: Dayjs | undefined) {
                            const start = gv('dateFrom') as Dayjs | undefined;
                            if (!value || !start) return Promise.resolve();
                            if (value.isBefore(start, 'day')) {
                              return Promise.reject(
                                new Error(t('timeConfig.closures.form.endBeforeStart')),
                              );
                            }
                            return Promise.resolve();
                          },
                        }),
                      ]}
                    >
                      <DatePicker format="DD/MM/YYYY" allowClear={false} />
                    </Form.Item>
                  </>
                )}
                {days > 0 && (
                  <Tag color="blue" className={styles.daysTag}>
                    {t('timeConfig.closures.form.days', { count: days })}
                  </Tag>
                )}
              </Space>
            );
          }}
        </Form.Item>

        <div className={styles.footer}>
          <div>
            {isEdit && onDelete && (
              <Button
                type="text"
                danger
                size="small"
                icon={<DeleteOutlined />}
                loading={!!deleting}
                onClick={onDelete}
              >
                {t('common.delete')}
              </Button>
            )}
          </div>
          <Space size={6}>
            <Button size="small" onClick={onCancel} disabled={saving || !!deleting}>
              {t('common.cancel')}
            </Button>
            <Button size="small" type="primary" htmlType="submit" loading={saving}>
              {isEdit ? t('common.save') : t('common.add')}
            </Button>
          </Space>
        </div>
      </Form>
    </Card>
  );
}
