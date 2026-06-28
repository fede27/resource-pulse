import { describe, it, expect } from 'vitest';
import dayjs from 'dayjs';
import { formValuesToDto, type WorkWindowFormValues } from './workWindowForm';

const base: WorkWindowFormValues = {
  dayOfWeek: 1,
  startTime: dayjs('2026-01-01T09:00:00'),
  endTime: dayjs('2026-01-01T17:30:00'),
  validFrom: dayjs('2026-02-01'),
  validTo: null,
};

describe('formValuesToDto', () => {
  it('serializes times to HH:mm:ss and dates to YYYY-MM-DD', () => {
    const dto = formValuesToDto(base);
    expect(dto.startTime).toBe('09:00:00');
    expect(dto.endTime).toBe('17:30:00');
    expect(dto.validFrom).toBe('2026-02-01');
    expect(dto.validTo).toBeNull();
  });

  it('includes the id only when one is supplied', () => {
    expect(formValuesToDto(base).id).toBeUndefined();
    expect(formValuesToDto(base, 'ww-1').id).toBe('ww-1');
  });

  it('serializes a bounded validity period', () => {
    const dto = formValuesToDto({ ...base, validTo: dayjs('2026-12-31') });
    expect(dto.validTo).toBe('2026-12-31');
  });

  it('defaults validFrom to today when the field is absent', () => {
    const dto = formValuesToDto({
      ...base,
      validFrom: undefined as unknown as WorkWindowFormValues['validFrom'],
    });
    expect(dto.validFrom).toBe(dayjs().startOf('day').format('YYYY-MM-DD'));
  });
});
