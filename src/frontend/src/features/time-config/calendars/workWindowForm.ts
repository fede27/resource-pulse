import dayjs, { type Dayjs } from 'dayjs';
import type { DayOfWeek, WorkWindowDto } from '@/api/generated/schemas';
import { minutesToTime } from './workWindow.utils';

// Shape of the work-window create/edit form. Lives in a non-component module so
// the popover file can stay a pure component file (react-refresh).
export type WorkWindowFormValues = {
  dayOfWeek: DayOfWeek;
  startTime: Dayjs;
  endTime: Dayjs;
  validFrom: Dayjs;
  validTo: Dayjs | null;
};

export function formValuesToDto(v: WorkWindowFormValues, id?: string): WorkWindowDto {
  const startMin = v.startTime.hour() * 60 + v.startTime.minute();
  const endMin = v.endTime.hour() * 60 + v.endTime.minute();
  // Defensive fallbacks: when the "Validity period" panel is never opened, the
  // Form may have no value for these fields. Default to "active from today,
  // indefinitely" — matches what the collapsed panel visually communicates.
  const validFrom = (v.validFrom ?? dayjs().startOf('day')).format('YYYY-MM-DD');
  const validTo = v.validTo ? v.validTo.format('YYYY-MM-DD') : null;
  const dto: WorkWindowDto = {
    dayOfWeek: v.dayOfWeek,
    startTime: minutesToTime(startMin),
    endTime: minutesToTime(endMin),
    validFrom,
    validTo,
  };
  if (id !== undefined) dto.id = id;
  return dto;
}
