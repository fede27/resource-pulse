import { useEffect, useRef } from 'react';
import { Button, Form, Input, Select, Space, Typography } from 'antd';
import { CloseOutlined } from '@ant-design/icons';
import type { InputRef } from 'antd';
import { createStyles } from 'antd-style';
import { useTranslation } from 'react-i18next';

const { Text } = Typography;

const useStyles = createStyles(({ token, css }) => ({
  root: css`
    padding: 14px;
    background: ${token.colorFillQuaternary};
    border-bottom: 1px solid ${token.colorBorderSecondary};
  `,
  header: css`
    display: flex;
    align-items: center;
    justify-content: space-between;
    margin-block-end: ${token.marginSM}px;
  `,
  title: css`
    font-size: ${token.fontSizeSM}px;
  `,
  close: css`
    cursor: pointer;
    color: ${token.colorTextTertiary};
    display: inline-flex;
  `,
  closeGlyph: css`
    font-size: 11px;
  `,
  field: css`
    margin-block-end: ${token.marginXS}px;
  `,
  fieldLast: css`
    margin-block-end: ${token.marginSM}px;
  `,
  footer: css`
    display: flex;
    justify-content: flex-end;
  `,
}));

export type PersonCreateValues = {
  name: string;
  email: string | undefined;
  roleId: string | undefined;
};

export type PersonInlineCreateRoleOption = {
  id: string;
  label: string;
};

export type PersonInlineCreateProps = {
  saving: boolean;
  onSubmit: (values: PersonCreateValues) => void;
  onCancel: () => void;
  roleOptions: PersonInlineCreateRoleOption[];
};

export function PersonInlineCreate({
  saving,
  onSubmit,
  onCancel,
  roleOptions,
}: PersonInlineCreateProps) {
  const { t } = useTranslation();
  const { styles } = useStyles();
  const [form] = Form.useForm<PersonCreateValues>();
  const ref = useRef<InputRef>(null);

  useEffect(() => {
    setTimeout(() => ref.current?.focus(), 0);
  }, []);

  return (
    <div
      className={styles.root}
      onKeyDown={(e) => {
        if (e.key === 'Escape') onCancel();
      }}
    >
      <div className={styles.header}>
        <Text strong className={styles.title}>
          {t('people.newPersonTitle')}
        </Text>
        <span
          onClick={onCancel}
          className={styles.close}
          role="button"
          aria-label={t('common.cancel')}
        >
          <CloseOutlined className={styles.closeGlyph} />
        </span>
      </div>

      <Form<PersonCreateValues>
        form={form}
        layout="vertical"
        size="small"
        onFinish={(values) =>
          onSubmit({
            name: values.name.trim(),
            email: values.email?.trim() || undefined,
            roleId: values.roleId || undefined,
          })
        }
        initialValues={{ name: '', email: '' }}
      >
        <Form.Item
          name="name"
          className={styles.field}
          rules={[
            { required: true, message: t('people.nameRequired') },
            { max: 200, message: t('people.nameMaxLength') },
          ]}
        >
          <Input
            ref={ref}
            placeholder={t('people.newPersonNamePlaceholder')}
          />
        </Form.Item>

        <Form.Item
          name="email"
          className={styles.field}
          rules={[
            { type: 'email', message: t('people.emailInvalid') },
            { max: 256, message: t('people.emailMaxLength') },
          ]}
        >
          <Input placeholder={t('people.emailPlaceholder')} />
        </Form.Item>

        <Form.Item name="roleId" className={styles.fieldLast}>
          <Select
            allowClear
            placeholder={t('people.rolePlaceholder')}
            options={roleOptions.map((r) => ({ value: r.id, label: r.label }))}
            showSearch
            optionFilterProp="label"
          />
        </Form.Item>

        <div className={styles.footer}>
          <Space size={6}>
            <Button size="small" onClick={onCancel} disabled={saving}>
              {t('common.cancel')}
            </Button>
            <Button
              size="small"
              type="primary"
              htmlType="submit"
              loading={saving}
            >
              {t('common.add')}
            </Button>
          </Space>
        </div>
      </Form>
    </div>
  );
}
