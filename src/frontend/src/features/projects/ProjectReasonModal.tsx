import { Alert, Form, Input, Modal } from 'antd';
import { useTranslation } from 'react-i18next';
import type { ReasonModalState } from './useProjectActions';
import { useStyles } from './ProjectReasonModal.styles';

export type ProjectReasonModalProps = {
  state: ReasonModalState;
  submitting: boolean;
  onSubmit: (reason: string) => void;
  onCancel: () => void;
};

// Suspend/Cancel both require a reason (the domain rejects a blank one), so
// the confirm is a small form modal instead of a bare modal.confirm.
export function ProjectReasonModal({ state, submitting, onSubmit, onCancel }: ProjectReasonModalProps) {
  const { t } = useTranslation();
  const { styles } = useStyles();
  const [form] = Form.useForm<{ reason: string }>();
  const isCancel = state?.action === 'cancel';

  return (
    <Modal
      open={!!state}
      title={
        state
          ? t(isCancel ? 'projects.actions.cancelTitle' : 'projects.actions.suspendTitle', {
              name: state.project.name,
            })
          : ''
      }
      okText={t(isCancel ? 'projects.actions.cancelCta' : 'projects.actions.suspendCta')}
      okButtonProps={{ danger: isCancel, loading: submitting }}
      onOk={() => form.submit()}
      onCancel={onCancel}
      destroyOnHidden
    >
      {isCancel ? (
        <Alert type="warning" showIcon message={t('projects.actions.cancelBody')} />
      ) : (
        <Alert type="info" showIcon message={t('projects.actions.suspendBody')} />
      )}
      <Form
        form={form}
        layout="vertical"
        preserve={false}
        onFinish={(v) => onSubmit(v.reason.trim())}
        className={styles.form}
      >
        <Form.Item
          name="reason"
          label={t('projects.actions.reasonLabel')}
          rules={[
            {
              required: true,
              whitespace: true,
              message: t('projects.actions.reasonRequired'),
            },
          ]}
        >
          <Input.TextArea
            rows={3}
            maxLength={500}
            placeholder={t('projects.actions.reasonPlaceholder')}
          />
        </Form.Item>
      </Form>
    </Modal>
  );
}
