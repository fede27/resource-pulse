import { useMemo } from 'react';
import { Alert, Form, InputNumber, Modal, Select } from 'antd';
import { useTranslation } from 'react-i18next';
import { useRolesGetAll } from '@/api/generated/roles/roles';
import type { LoadResult, RoleReadDto } from '@/api/generated/schemas';
import type { PersonPoolEntry } from './useProjectsBoard';
import type { EditDemandPatch, LaneModalState, NewDemand } from './useLaneActions';
import { peopleByRole } from './boardModel';
import { useStyles } from './LaneActionModal.styles';

export type LaneActionModalProps = {
  // Covering has its own dialog (CoverDemandModal): it prices the coverage
  // before creating it, which a one-field form cannot do.
  state: Exclude<LaneModalState, { kind: 'cover' }>;
  submitting: boolean;
  personPool: PersonPoolEntry[];
  onReassign: (resourceId: string) => void;
  onRetarget: (demandId: string) => void;
  onAddDemand: (demand: NewDemand) => void;
  onEditDemand: (patch: EditDemandPatch) => void;
  onCancel: () => void;
};

type FormValues = {
  resourceId?: string;
  demandId?: string;
  projectNodeId?: string;
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
  onAddDemand,
  onEditDemand,
  onCancel,
}: LaneActionModalProps) {
  const { t } = useTranslation();
  const { styles } = useStyles();
  const [form] = Form.useForm<FormValues>();

  const rolesQ = useRolesGetAll({
    query: { enabled: state?.kind === 'editDemand' || state?.kind === 'addDemand' },
  });
  const roleOptions = useMemo(
    () =>
      (((rolesQ.data as LoadResult | undefined)?.data ?? []) as RoleReadDto[])
        .filter((r): r is RoleReadDto & { id: string; name: string } => !!r.id && !!r.name)
        .map((r) => ({ value: r.id, label: r.name })),
    [rolesQ.data],
  );

  // The role the person is being picked for: the demand's own on the lane
  // gestures, the one chosen in the form while a demand is being written.
  const pickedRoleId = Form.useWatch('roleId', form);
  const preferredRole =
    state?.kind === 'reassign'
        ? state.block.demandRoleName
        : (roleOptions.find((r) => r.value === pickedRoleId)?.label ?? null);

  const noRoleLabel = t('projects.lane.actions.noRoleGroup');
  const personOptions = useMemo(
    () => peopleByRole(personPool, preferredRole, noRoleLabel),
    [personPool, preferredRole, noRoleLabel],
  );

  const handleFinish = (values: FormValues) => {
    switch (state?.kind) {
      case 'reassign':
        if (values.resourceId) onReassign(values.resourceId);
        break;
      case 'retarget':
        if (values.demandId) onRetarget(values.demandId);
        break;
      case 'addDemand':
        if (values.roleId && values.projectNodeId)
          onAddDemand({
            projectNodeId: values.projectNodeId,
            roleId: values.roleId,
            requiredHours: values.requiredHours ?? null,
            ownerResourceId: values.ownerResourceId ?? null,
          });
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
                options={personOptions
                  .map((g) => ({ ...g, options: g.options.filter((o) => o.value !== state.block.resourceId) }))
                  .filter((g) => g.options.length > 0)}
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
      case 'addDemand': {
        // The node is where the demand LIVES, and therefore what its coverage
        // can follow (ADR-0035): the project itself, or one of its phases.
        const nodeOptions = [
          { value: state.project.id, label: t('projects.lane.actions.demandNodeProject', { name: state.project.name }) },
          ...state.project.phases.map((ph) => ({ value: ph.id, label: ph.label })),
        ];
        return {
          title: t('projects.lane.actions.addDemandTitle', { project: state.project.name }),
          okText: t('projects.lane.actions.addDemandCta'),
          initialValues: { projectNodeId: state.project.id } as FormValues,
          okDisabled: false,
          body: (
            <>
              <Form.Item
                name="projectNodeId"
                label={t('projects.lane.actions.demandNodeLabel')}
                rules={[{ required: true }]}
              >
                <Select options={nodeOptions} />
              </Form.Item>
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
                extra={t('projects.lane.actions.demandHoursHintNew')}
              >
                <InputNumber min={1} step={10} className={styles.fullWidth} />
              </Form.Item>
              <Form.Item name="ownerResourceId" label={t('projects.lane.actions.demandOwnerLabel')}>
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
