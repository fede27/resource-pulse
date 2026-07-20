import { useMemo } from 'react';
import { Alert, Form, InputNumber, Modal, Select } from 'antd';
import { useTranslation } from 'react-i18next';
import { useRolesGetAll } from '@/api/generated/roles/roles';
import type { LoadResult, RoleReadDto } from '@/api/generated/schemas';
import type { PersonPoolEntry } from './useProjectsBoard';
import type { EditDemandPatch, LaneModalState } from './useLaneActions';
import { useStyles } from './LaneActionModal.styles';

export type LaneActionModalProps = {
  state: LaneModalState;
  submitting: boolean;
  personPool: PersonPoolEntry[];
  onReassign: (resourceId: string) => void;
  onRetarget: (demandId: string) => void;
  onCover: (resourceId: string, percent: number) => void;
  onEditDemand: (patch: EditDemandPatch) => void;
  onCancel: () => void;
};

type FormValues = {
  resourceId?: string;
  demandId?: string;
  percent?: number;
  roleId?: string;
  requiredHours?: number | null;
  ownerResourceId?: string | null;
};

// One modal host for every lane gesture that needs input. The body switches on
// the pending action's kind; `destroyOnHidden` remounts the form per open so
// initialValues (prefilled from the target) always apply.
export function LaneActionModal({
  state,
  submitting,
  personPool,
  onReassign,
  onRetarget,
  onCover,
  onEditDemand,
  onCancel,
}: LaneActionModalProps) {
  const { t } = useTranslation();
  const { styles } = useStyles();
  const [form] = Form.useForm<FormValues>();

  const rolesQ = useRolesGetAll({ query: { enabled: state?.kind === 'editDemand' } });
  const roleOptions = useMemo(
    () =>
      (((rolesQ.data as LoadResult | undefined)?.data ?? []) as RoleReadDto[])
        .filter((r): r is RoleReadDto & { id: string; name: string } => !!r.id && !!r.name)
        .map((r) => ({ value: r.id, label: r.name })),
    [rolesQ.data],
  );

  const personOptions = useMemo(
    () => personPool.map((p) => ({ value: p.id, label: p.roleName ? `${p.name} · ${p.roleName}` : p.name })),
    [personPool],
  );

  const handleFinish = (values: FormValues) => {
    switch (state?.kind) {
      case 'reassign':
        if (values.resourceId) onReassign(values.resourceId);
        break;
      case 'retarget':
        if (values.demandId) onRetarget(values.demandId);
        break;
      case 'cover':
        if (values.resourceId && values.percent) onCover(values.resourceId, values.percent);
        break;
      case 'editDemand':
        if (values.roleId)
          onEditDemand({
            roleId: values.roleId,
            requiredHours: values.requiredHours ?? null,
            ownerResourceId: values.ownerResourceId ?? null,
          });
        break;
    }
  };

  // Title / CTA / initial values / body all key off the pending kind.
  const config = (() => {
    if (!state) return null;
    switch (state.kind) {
      case 'reassign': {
        const role = state.block.demandRoleName || '—';
        return {
          title: t('projects.lane.actions.reassignTitle', { role }),
          okText: t('projects.lane.actions.reassignCta'),
          initialValues: {} as FormValues,
          okDisabled: false,
          body: (
            <Form.Item
              name="resourceId"
              label={t('projects.lane.actions.reassignPersonLabel')}
              rules={[{ required: true }]}
            >
              <Select
                showSearch
                optionFilterProp="label"
                placeholder={t('projects.lane.actions.reassignPersonPlaceholder')}
                options={personOptions.filter((o) => o.value !== state.block.resourceId)}
              />
            </Form.Item>
          ),
        };
      }
      case 'retarget': {
        const others = state.project.demands.filter((d) => d.demandId !== state.block.demandId);
        return {
          title: t('projects.lane.actions.retargetTitle', { name: state.block.resourceName }),
          okText: t('projects.lane.actions.retargetCta'),
          initialValues: {} as FormValues,
          okDisabled: others.length === 0,
          body:
            others.length === 0 ? (
              <Alert type="info" showIcon message={t('projects.lane.actions.retargetNoOther')} />
            ) : (
              <Form.Item
                name="demandId"
                label={t('projects.lane.actions.retargetDemandLabel')}
                rules={[{ required: true }]}
              >
                <Select
                  showSearch
                  optionFilterProp="label"
                  placeholder={t('projects.lane.actions.retargetDemandPlaceholder')}
                  options={others.map((d) => ({ value: d.demandId, label: d.roleName }))}
                />
              </Form.Item>
            ),
        };
      }
      case 'cover': {
        return {
          title: t('projects.lane.actions.coverTitle', { role: state.demand.roleName }),
          okText: t('projects.lane.actions.coverCta'),
          initialValues: { percent: 50 } as FormValues,
          okDisabled: false,
          body: (
            <>
              <Form.Item
                name="resourceId"
                label={t('projects.lane.actions.coverPersonLabel')}
                rules={[{ required: true }]}
              >
                <Select
                  showSearch
                  optionFilterProp="label"
                  placeholder={t('projects.lane.actions.coverPersonPlaceholder')}
                  options={personOptions}
                />
              </Form.Item>
              <Form.Item
                name="percent"
                label={t('projects.lane.actions.coverPercentLabel')}
                rules={[{ required: true }]}
              >
                <InputNumber min={1} max={1000} step={5} className={styles.fullWidth} />
              </Form.Item>
              <Alert type="info" showIcon message={t('projects.lane.actions.coverPeriodNote')} />
            </>
          ),
        };
      }
      case 'editDemand': {
        return {
          title: t('projects.lane.actions.editDemandTitle', { role: state.demand.roleName }),
          okText: t('projects.lane.actions.editDemandCta'),
          initialValues: {
            roleId: state.demand.roleId,
            requiredHours: state.demand.requiredH,
            ownerResourceId: state.demand.ownerResourceId,
          } as FormValues,
          okDisabled: false,
          body: (
            <>
              <Form.Item
                name="roleId"
                label={t('projects.lane.actions.demandRoleLabel')}
                rules={[{ required: true }]}
              >
                <Select
                  showSearch
                  optionFilterProp="label"
                  placeholder={t('projects.lane.actions.demandRolePlaceholder')}
                  options={roleOptions}
                  loading={rolesQ.isPending}
                />
              </Form.Item>
              <Form.Item
                name="requiredHours"
                label={t('projects.lane.actions.demandHoursLabel')}
                extra={t('projects.lane.actions.demandHoursHint')}
              >
                <InputNumber min={1} step={10} className={styles.fullWidth} />
              </Form.Item>
              <Form.Item
                name="ownerResourceId"
                label={t('projects.lane.actions.demandOwnerLabel')}
              >
                <Select
                  allowClear
                  showSearch
                  optionFilterProp="label"
                  placeholder={t('projects.lane.actions.demandOwnerPlaceholder')}
                  options={personOptions}
                />
              </Form.Item>
            </>
          ),
        };
      }
    }
  })();

  return (
    <Modal
      open={!!state}
      title={config?.title}
      okText={config?.okText}
      okButtonProps={{ loading: submitting, disabled: config?.okDisabled ?? false }}
      onOk={() => form.submit()}
      onCancel={onCancel}
      destroyOnHidden
    >
      {config && (
        <Form
          form={form}
          layout="vertical"
          preserve={false}
          initialValues={config.initialValues}
          onFinish={handleFinish}
          className={styles.form}
        >
          {config.body}
        </Form>
      )}
    </Modal>
  );
}
