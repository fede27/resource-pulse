import { Button, Form, Input, Space, Switch } from 'antd';
import { useTranslation } from 'react-i18next';

export type TeamFormValues = {
  name: string;
  isActive: boolean;
};

type TeamFormProps = {
  initialValues?: Partial<TeamFormValues>;
  mode: 'create' | 'edit';
  submitting: boolean;
  onSubmit: (values: TeamFormValues) => void;
  onCancel: () => void;
};

export function TeamForm({ initialValues, mode, submitting, onSubmit, onCancel }: TeamFormProps) {
  const { t } = useTranslation();
  const [form] = Form.useForm<TeamFormValues>();

  return (
    <Form<TeamFormValues>
      form={form}
      layout="vertical"
      initialValues={{ isActive: true, ...initialValues }}
      onFinish={onSubmit}
      autoComplete="off"
      style={{ maxWidth: 480 }}
    >
      <Form.Item
        label={t('common.name')}
        name="name"
        rules={[
          { required: true, message: t('teams.nameRequired') },
          { max: 100, message: t('teams.nameMaxLength') },
        ]}
      >
        <Input placeholder={t('teams.namePlaceholder')} />
      </Form.Item>

      {mode === 'edit' && (
        <Form.Item label={t('common.active')} name="isActive" valuePropName="checked">
          <Switch />
        </Form.Item>
      )}

      <Form.Item>
        <Space>
          <Button type="primary" htmlType="submit" loading={submitting}>
            {mode === 'create' ? t('common.create') : t('common.save')}
          </Button>
          <Button onClick={onCancel} disabled={submitting}>
            {t('common.cancel')}
          </Button>
        </Space>
      </Form.Item>
    </Form>
  );
}
