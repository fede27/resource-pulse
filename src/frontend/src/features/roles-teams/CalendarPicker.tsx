import { useState } from 'react';
import { App, Button, Modal } from 'antd';
import { useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import {
  getResourcesGetAllQueryKey,
  getResourcesGetCapacitiesQueryKey,
  useResourcesAssignCalendar,
} from '@/api/generated/resources/resources';
import type { BusinessCalendarReadDto } from '@/api/generated/schemas';
import { useApiError } from '@/lib/errors';
import { windowsWeeklyHours } from './availabilityModel';
import { useStyles } from './CalendarPicker.styles';

export type CalendarPickerProps = {
  open: boolean;
  resourceId: string;
  personName: string;
  currentCalendarId: string | undefined;
  calendars: BusinessCalendarReadDto[];
  onClose: () => void;
};

export function CalendarPicker({
  open,
  resourceId,
  personName,
  currentCalendarId,
  calendars,
  onClose,
}: CalendarPickerProps) {
  const { t } = useTranslation();
  const { styles, cx } = useStyles();
  const { message } = App.useApp();
  const queryClient = useQueryClient();
  const showApiError = useApiError();

  // Initializes from the person's current calendar on mount; the parent keys
  // this component by resource id so a different person remounts it.
  const [calId, setCalId] = useState<string | undefined>(currentCalendarId);

  const assignMutation = useResourcesAssignCalendar({
    mutation: {
      onSuccess: () => {
        message.success(t('rolesTeams.toast.calendarAssigned'));
        void queryClient.invalidateQueries({ queryKey: getResourcesGetAllQueryKey() });
        void queryClient.invalidateQueries({ queryKey: getResourcesGetCapacitiesQueryKey() });
        onClose();
      },
      onError: (e) => showApiError(e),
    },
  });

  const assign = () => {
    if (!calId) return;
    assignMutation.mutate({ id: resourceId, data: { businessCalendarId: calId } });
  };

  return (
    <Modal
      open={open}
      onCancel={onClose}
      title={t('rolesTeams.picker.title', { name: personName })}
      width={480}
      footer={[
        <Button key="cancel" onClick={onClose}>
          {t('common.cancel')}
        </Button>,
        <Button
          key="assign"
          type="primary"
          onClick={assign}
          loading={assignMutation.isPending}
          disabled={!calId}
        >
          {t('rolesTeams.picker.assign')}
        </Button>,
      ]}
    >
      <div className={styles.list}>
        {calendars.map((c) => {
          const active = c.id === calId;
          const weekly = windowsWeeklyHours(c.workWindows ?? []);
          return (
            <div
              key={c.id}
              className={cx(styles.option, active && styles.optionActive)}
              onClick={() => setCalId(c.id)}
            >
              <span className={cx(styles.radio, active && styles.radioActive)}>
                {active && <span className={styles.radioDot} />}
              </span>
              <div className={styles.body}>
                <div className={styles.name}>
                  {c.name}
                  {c.isDefault && (
                    <span className={styles.defaultTag}>{t('rolesTeams.picker.default')}</span>
                  )}
                </div>
                <div className={styles.sub}>
                  {t('rolesTeams.picker.weekHours', { hours: weekly })}
                </div>
              </div>
            </div>
          );
        })}
      </div>
    </Modal>
  );
}
