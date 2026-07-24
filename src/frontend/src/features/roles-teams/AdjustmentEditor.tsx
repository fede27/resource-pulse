import { useState } from 'react';
import {
  App,
  Button,
  Checkbox,
  DatePicker,
  InputNumber,
  Input,
  Modal,
} from 'antd';
import { DeleteOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import { useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import {
  getResourcesGetAllQueryKey,
  getResourcesGetCapacitiesQueryKey,
  useResourcesAddAdjustment,
  useResourcesRemoveAdjustment,
  useResourcesUpdateAdjustment,
} from '@/api/generated/resources/resources';
import { AdjustmentType, type IndividualAdjustmentDto } from '@/api/generated/schemas';
import { useApiError } from '@/lib/errors';
import { hoursToWire } from './availabilityModel';
import { parseDurationHours } from '@/lib/duration';
import { useStyles } from './AdjustmentEditor.styles';

const ISO = 'YYYY-MM-DD';

// A draft (no id) or an existing adjustment being edited.
export type EditorInitial = {
  id?: string;
  type: AdjustmentType;
  dateFrom: string;
  dateTo: string;
  hours?: string | null;
  reason?: string;
};

export type AdjustmentEditorProps = {
  open: boolean;
  resourceId: string;
  initial: EditorInitial;
  onClose: () => void;
};

export function AdjustmentEditor({
  open,
  resourceId,
  initial,
  onClose,
}: AdjustmentEditorProps) {
  const { t } = useTranslation();
  const { styles, cx } = useStyles();
  const { message } = App.useApp();
  const queryClient = useQueryClient();
  const showApiError = useApiError();

  const isEdit = !!initial.id;
  const [type, setType] = useState<AdjustmentType>(initial.type);
  const [from, setFrom] = useState(initial.dateFrom);
  const [to, setTo] = useState(initial.dateTo);
  const [fullDay, setFullDay] = useState(initial.hours == null);
  const [hours, setHours] = useState<number>(
    initial.hours != null ? parseDurationHours(initial.hours) : 4,
  );
  const [reason, setReason] = useState(initial.reason ?? '');
  const [errors, setErrors] = useState<{ reason?: boolean; to?: boolean; hours?: boolean }>({});

  // State initializes from `initial` on mount; the parent keys this component by
  // the edited adjustment so a new target remounts it (no reset-in-effect).

  const invalidate = () => {
    void queryClient.invalidateQueries({ queryKey: getResourcesGetAllQueryKey() });
    void queryClient.invalidateQueries({ queryKey: getResourcesGetCapacitiesQueryKey() });
  };

  const addMutation = useResourcesAddAdjustment({
    mutation: { onError: (e) => showApiError(e) },
  });
  const updateMutation = useResourcesUpdateAdjustment({
    mutation: { onError: (e) => showApiError(e) },
  });
  const removeMutation = useResourcesRemoveAdjustment({
    mutation: { onError: (e) => showApiError(e) },
  });

  const busy = addMutation.isPending || updateMutation.isPending || removeMutation.isPending;
  const isExtra = type === AdjustmentType.ExtraTime;

  const save = () => {
    const next: typeof errors = {};
    if (!reason.trim()) next.reason = true;
    if (dayjs(to).isBefore(dayjs(from))) next.to = true;
    if (!fullDay && (!hours || hours <= 0)) next.hours = true;
    setErrors(next);
    if (next.reason || next.to || next.hours) return;

    const data: IndividualAdjustmentDto = {
      dateFrom: from,
      dateTo: to,
      type,
      hours: fullDay ? null : hoursToWire(hours),
      reason: reason.trim(),
    };
    const onSuccess = () => {
      message.success(isExtra ? t('rolesTeams.toast.extraSaved') : t('rolesTeams.toast.ferieSaved'));
      invalidate();
      onClose();
    };
    if (isEdit && initial.id) {
      updateMutation.mutate({ id: resourceId, adjustmentId: initial.id, data }, { onSuccess });
    } else {
      addMutation.mutate({ id: resourceId, data }, { onSuccess });
    }
  };

  const remove = () => {
    if (!initial.id) return;
    removeMutation.mutate(
      { id: resourceId, adjustmentId: initial.id },
      {
        onSuccess: () => {
          message.success(t('rolesTeams.toast.adjDeleted'));
          invalidate();
          onClose();
        },
      },
    );
  };

  return (
    <Modal
      open={open}
      onCancel={onClose}
      title={isEdit ? t('rolesTeams.editor.editTitle') : t('rolesTeams.editor.newTitle')}
      width={520}
      footer={
        <div className={styles.footer}>
          <div>
            {isEdit && (
              <Button type="text" danger icon={<DeleteOutlined />} onClick={remove} loading={busy}>
                {t('common.delete')}
              </Button>
            )}
          </div>
          <div className={styles.footerRight}>
            <Button onClick={onClose}>{t('common.cancel')}</Button>
            <Button type="primary" onClick={save} loading={busy}>
              {isEdit ? t('rolesTeams.editor.save') : t('rolesTeams.editor.add')}
            </Button>
          </div>
        </div>
      }
    >
      <div className={styles.typeRow}>
        <button
          type="button"
          className={cx(styles.typeBtn, !isExtra && styles.typeBtnFerie)}
          onClick={() => setType(AdjustmentType.Absence)}
        >
          {t('rolesTeams.editor.typeFerie')}
          <div className={styles.typeHint}>{t('rolesTeams.editor.typeFerieHint')}</div>
        </button>
        <button
          type="button"
          className={cx(styles.typeBtn, isExtra && styles.typeBtnExtra)}
          onClick={() => setType(AdjustmentType.ExtraTime)}
        >
          {t('rolesTeams.editor.typeExtra')}
          <div className={styles.typeHint}>{t('rolesTeams.editor.typeExtraHint')}</div>
        </button>
      </div>

      <div className={styles.field}>
        <div className={styles.label}>{t('rolesTeams.editor.reason')}</div>
        <Input
          value={reason}
          status={errors.reason ? 'error' : ''}
          onChange={(e) => setReason(e.target.value)}
          placeholder={
            isExtra
              ? t('rolesTeams.editor.reasonPlaceholderExtra')
              : t('rolesTeams.editor.reasonPlaceholderFerie')
          }
        />
        {errors.reason && <div className={styles.err}>{t('rolesTeams.editor.reasonRequired')}</div>}
      </div>

      <div className={styles.twoCol}>
        <div>
          <div className={styles.label}>{t('rolesTeams.editor.from')}</div>
          <DatePicker
            className={styles.fullW}
            value={from ? dayjs(from) : null}
            onChange={(d) => {
              const v = d ? d.format(ISO) : '';
              setFrom(v);
              if (v && dayjs(to).isBefore(dayjs(v))) setTo(v);
            }}
          />
        </div>
        <div>
          <div className={styles.label}>{t('rolesTeams.editor.to')}</div>
          <DatePicker
            className={styles.fullW}
            status={errors.to ? 'error' : ''}
            value={to ? dayjs(to) : null}
            disabledDate={(d) => (from ? d.isBefore(dayjs(from), 'day') : false)}
            onChange={(d) => setTo(d ? d.format(ISO) : '')}
          />
          {errors.to && <div className={styles.err}>{t('rolesTeams.editor.dateOrder')}</div>}
        </div>
      </div>

      <div className={styles.fullDayBox}>
        <Checkbox checked={fullDay} onChange={(e) => setFullDay(e.target.checked)}>
          {t('rolesTeams.editor.fullDay')}
        </Checkbox>
        {!fullDay && (
          <div className={styles.hoursRow}>
            <span className={styles.label}>
              {isExtra
                ? t('rolesTeams.editor.extraHoursLabel')
                : t('rolesTeams.editor.absenceHoursLabel')}
            </span>
            <InputNumber
              min={0.5}
              step={0.5}
              value={hours}
              status={errors.hours ? 'error' : ''}
              onChange={(v) => setHours(typeof v === 'number' ? v : 0)}
            />
          </div>
        )}
        <div className={styles.hoursHint}>
          {isExtra
            ? fullDay
              ? t('rolesTeams.editor.hintExtraFull')
              : t('rolesTeams.editor.hintExtraPartial')
            : fullDay
              ? t('rolesTeams.editor.hintFerieFull')
              : t('rolesTeams.editor.hintFeriePartial')}
        </div>
      </div>
    </Modal>
  );
}
