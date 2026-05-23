import { Button, Form, Input, Space, Switch } from 'antd';

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
        label="Nome"
        name="name"
        rules={[
          { required: true, message: 'Il nome è obbligatorio' },
          { max: 100, message: 'Massimo 100 caratteri' },
        ]}
      >
        <Input placeholder="es. Platform Engineering" />
      </Form.Item>

      {mode === 'edit' && (
        <Form.Item label="Attivo" name="isActive" valuePropName="checked">
          <Switch />
        </Form.Item>
      )}

      <Form.Item>
        <Space>
          <Button type="primary" htmlType="submit" loading={submitting}>
            {mode === 'create' ? 'Crea' : 'Salva'}
          </Button>
          <Button onClick={onCancel} disabled={submitting}>
            Annulla
          </Button>
        </Space>
      </Form.Item>
    </Form>
  );
}
