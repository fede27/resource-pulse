import { useEffect } from 'react';
import { Button, DatePicker, Form, Input, Select, Space } from 'antd';
import { CloseOutlined, PlusOutlined } from '@ant-design/icons';
import type { Dayjs } from 'dayjs';
import { useTranslation } from 'react-i18next';
import { CommitmentLevel, ProjectType } from '@/api/generated/schemas';
import { InspectorDrawer } from '@/components/domain/InspectorDrawer';
import type { PersonPoolEntry } from './useProjectsBoard';
import { useStyles } from './NewProjectPanel.styles';

const ISO = 'YYYY-MM-DD';

type PhaseRowValues = { name?: string; start?: Dayjs; end?: Dayjs };

type FormValues = {
  name: string;
  client?: string;
  ownerId?: string;
  type: ProjectType;
  commitmentLevel: CommitmentLevel;
  start?: Dayjs;
  end?: Dayjs;
  phases?: PhaseRowValues[];
};

// What the panel hands to the page: dates already serialized, incomplete
// phase rows already dropped (the prototype's submit contract).
export type NewProjectSubmit = {
  name: string;
  client: string | null;
  ownerId: string | null;
  type: ProjectType;
  commitmentLevel: CommitmentLevel;
  startISO: string;
  endISO: string;
  phases: { name: string; startISO: string; endISO: string }[];
};

export type NewProjectPanelProps = {
  open: boolean;
  saving: boolean;
  onClose: () => void;
  onSubmit: (values: NewProjectSubmit) => void;
  personPool: PersonPoolEntry[];
  defaultOwnerId: string | null;
};

// Variant 2 of the Claude Design study: classic data entry in a non-modal
// slide-over — same visual language as the inspector drawer, no mask, the
// timeline stays visible and interactive behind it.
export function NewProjectPanel({
  open,
  saving,
  onClose,
  onSubmit,
  personPool,
  defaultOwnerId,
}: NewProjectPanelProps) {
  const { t } = useTranslation();
  const { styles } = useStyles();
  const [form] = Form.useForm<FormValues>();

  const start = Form.useWatch('start', form);
  const end = Form.useWatch('end', form);
  const hasDates = !!start && !!end;
  // exactOptionalPropertyTypes: antd's minDate/maxDate don't accept undefined,
  // so the bounds are spread only once both project dates exist.
  const phaseDateBounds = start && end ? { minDate: start, maxDate: end } : {};

  useEffect(() => {
    if (open) form.resetFields();
  }, [form, open]);

  const handleFinish = (values: FormValues) => {
    if (!values.start || !values.end) return;
    onSubmit({
      name: values.name.trim(),
      client: values.client?.trim() || null,
      ownerId: values.ownerId ?? null,
      type: values.type,
      commitmentLevel: values.commitmentLevel,
      startISO: values.start.format(ISO),
      endISO: values.end.format(ISO),
      phases: (values.phases ?? [])
        .filter((p): p is Required<PhaseRowValues> => !!p.name?.trim() && !!p.start && !!p.end)
        .map((p) => ({
          name: p.name.trim(),
          startISO: p.start.format(ISO),
          endISO: p.end.format(ISO),
        })),
    });
  };

  const typeOptions = [
    { value: ProjectType.Internal, label: t('projects.newProject.typeInternal') },
    { value: ProjectType.Customer, label: t('projects.newProject.typeCustomer') },
    { value: ProjectType.Investment, label: t('projects.newProject.typeInvestment') },
    { value: ProjectType.Maintenance, label: t('projects.newProject.typeMaintenance') },
  ];

  const commitmentOptions = [
    { value: CommitmentLevel.Exploratory, label: t('projects.newProject.commitmentExploratory') },
    { value: CommitmentLevel.Planned, label: t('projects.newProject.commitmentPlanned') },
    { value: CommitmentLevel.Committed, label: t('projects.newProject.commitmentCommitted') },
    { value: CommitmentLevel.Critical, label: t('projects.newProject.commitmentCritical') },
  ];

  return (
    <InspectorDrawer
      open={open}
      onClose={onClose}
      mask={false}
      title={t('projects.newProject.title')}
      subtitle={t('projects.newProject.subtitle')}
      footer={
        <>
          <Button onClick={onClose} disabled={saving}>
            {t('common.cancel')}
          </Button>
          <Button type="primary" loading={saving} onClick={() => form.submit()}>
            {t('projects.newProject.submit')}
          </Button>
        </>
      }
    >
      <Form<FormValues>
        form={form}
        layout="vertical"
        onFinish={handleFinish}
        initialValues={{
          type: ProjectType.Customer,
          commitmentLevel: CommitmentLevel.Planned,
          ownerId: defaultOwnerId ?? undefined,
          phases: [],
        }}
      >
        <Form.Item
          name="name"
          label={t('projects.newProject.nameLabel')}
          rules={[
            { required: true, whitespace: true, message: t('projects.newProject.nameRequired') },
            { max: 500, message: t('projects.newProject.nameMaxLength') },
          ]}
        >
          <Input placeholder={t('projects.newProject.namePlaceholder')} autoFocus />
        </Form.Item>

        <Form.Item
          name="client"
          label={t('projects.newProject.clientLabel')}
          extra={t('projects.newProject.clientHint')}
          rules={[{ max: 500, message: t('projects.newProject.clientMaxLength') }]}
        >
          <Input placeholder={t('projects.newProject.clientPlaceholder')} />
        </Form.Item>

        <div className={styles.fieldPair}>
          <Form.Item name="ownerId" label={t('projects.newProject.ownerLabel')}>
            <Select
              allowClear
              showSearch
              optionFilterProp="label"
              placeholder={t('projects.newProject.ownerPlaceholder')}
              options={personPool.map((p) => ({ value: p.id, label: p.name }))}
            />
          </Form.Item>
          <Form.Item name="type" label={t('projects.newProject.typeLabel')}>
            <Select options={typeOptions} />
          </Form.Item>
        </div>

        <Form.Item
          name="commitmentLevel"
          label={t('projects.newProject.commitmentLabel')}
          extra={t('projects.newProject.commitmentHint')}
        >
          <Select options={commitmentOptions} />
        </Form.Item>

        <div className={styles.fieldPair}>
          <Form.Item
            name="start"
            label={t('projects.newProject.startLabel')}
            rules={[{ required: true, message: t('projects.newProject.dateRequired') }]}
          >
            <DatePicker format="DD/MM/YYYY" allowClear={false} className={styles.datePicker} />
          </Form.Item>
          <Form.Item
            name="end"
            label={t('projects.newProject.endLabel')}
            extra={t('projects.newProject.endHint')}
            dependencies={['start']}
            rules={[
              { required: true, message: t('projects.newProject.dateRequired') },
              ({ getFieldValue }) => ({
                validator(_, value: Dayjs | undefined) {
                  const s = getFieldValue('start') as Dayjs | undefined;
                  if (!value || !s || !value.isBefore(s, 'day')) return Promise.resolve();
                  return Promise.reject(new Error(t('projects.newProject.endBeforeStart')));
                },
              }),
            ]}
          >
            <DatePicker format="DD/MM/YYYY" allowClear={false} className={styles.datePicker} />
          </Form.Item>
        </div>

        <Form.List name="phases">
          {(fields, { add, remove }) => (
            <>
              <div className={styles.phasesHeader}>
                <span className={styles.phasesTitle}>{t('projects.newProject.phasesTitle')}</span>
                <Button
                  size="small"
                  icon={<PlusOutlined />}
                  disabled={!hasDates}
                  onClick={() => start && end && add({ start, end } satisfies PhaseRowValues)}
                >
                  {t('projects.newProject.addPhase')}
                </Button>
              </div>
              {!hasDates ? (
                <div className={styles.phasesHint}>{t('projects.newProject.phasesNeedDates')}</div>
              ) : fields.length === 0 ? (
                <div className={styles.phasesHint}>{t('projects.newProject.phasesEmpty')}</div>
              ) : null}
              {fields.map(({ key, name: idx }) => (
                <div key={key} className={styles.phaseRow}>
                  <div className={styles.phaseFields}>
                    <Form.Item name={[idx, 'name']}>
                      <Input placeholder={t('projects.newProject.phaseNamePlaceholder')} />
                    </Form.Item>
                    <div className={styles.phaseDates}>
                      <Form.Item name={[idx, 'start']}>
                        <DatePicker
                          format="DD/MM/YYYY"
                          allowClear={false}
                          {...phaseDateBounds}
                          className={styles.datePicker}
                        />
                      </Form.Item>
                      <Form.Item
                        name={[idx, 'end']}
                        dependencies={[['phases', idx, 'start']]}
                        rules={[
                          ({ getFieldValue }) => ({
                            validator(_, value: Dayjs | undefined) {
                              const s = getFieldValue(['phases', idx, 'start']) as Dayjs | undefined;
                              if (!value || !s || !value.isBefore(s, 'day')) return Promise.resolve();
                              return Promise.reject(
                                new Error(t('projects.newProject.phaseEndBeforeStart')),
                              );
                            },
                          }),
                        ]}
                      >
                        <DatePicker
                          format="DD/MM/YYYY"
                          allowClear={false}
                          {...phaseDateBounds}
                          className={styles.datePicker}
                        />
                      </Form.Item>
                    </div>
                  </div>
                  <Space>
                    <Button
                      type="text"
                      size="small"
                      icon={<CloseOutlined />}
                      aria-label={t('projects.newProject.removePhase')}
                      onClick={() => remove(idx)}
                    />
                  </Space>
                </div>
              ))}
            </>
          )}
        </Form.List>
      </Form>
    </InspectorDrawer>
  );
}
