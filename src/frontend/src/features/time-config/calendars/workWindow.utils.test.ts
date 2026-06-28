import { describe, it, expect } from 'vitest';
import dayjs from 'dayjs';
import type { WorkWindowDto } from '@/api/generated/schemas';
import {
  columnIndexToDayOfWeek,
  dayOfWeekToColumnIndex,
  formatHourMinute,
  formatPatternSummary,
  isWindowActiveOn,
  isWindowActiveToday,
  isWindowFuture,
  isWindowHistorical,
  minutesToTime,
  patternSummary,
  timeToMinutes,
  weeklyHours,
} from './workWindow.utils';

const ISO = (d: dayjs.Dayjs) => d.format('YYYY-MM-DD');
const today = dayjs().startOf('day');

// dayOfWeek backend: Sun=0..Sat=6. Mon=1.
function win(p: Partial<WorkWindowDto>): WorkWindowDto {
  return {
    dayOfWeek: 1,
    startTime: '09:00:00',
    endTime: '17:00:00',
    validFrom: ISO(today.subtract(1, 'day')),
    validTo: null,
    ...p,
  } as WorkWindowDto;
}

describe('day-of-week ↔ column index', () => {
  it('maps Sunday(0) to the last column and shifts the rest down', () => {
    expect(dayOfWeekToColumnIndex(0)).toBe(6); // Sun → col 6
    expect(dayOfWeekToColumnIndex(1)).toBe(0); // Mon → col 0
    expect(dayOfWeekToColumnIndex(6)).toBe(5); // Sat → col 5
    expect(dayOfWeekToColumnIndex(undefined)).toBe(6);
  });

  it('round-trips column index back to backend day-of-week', () => {
    expect(columnIndexToDayOfWeek(0)).toBe(1); // col 0 → Mon
    expect(columnIndexToDayOfWeek(6)).toBe(0); // col 6 → Sun
    for (let i = 0; i < 7; i += 1) {
      expect(dayOfWeekToColumnIndex(columnIndexToDayOfWeek(i))).toBe(i);
    }
  });
});

describe('time conversions', () => {
  it('parses HH:mm and HH:mm:ss to minutes since midnight', () => {
    expect(timeToMinutes('09:30')).toBe(570);
    expect(timeToMinutes('09:30:00')).toBe(570);
    expect(timeToMinutes(undefined)).toBe(0);
  });

  it('formats minutes back to HH:mm:ss', () => {
    expect(minutesToTime(570)).toBe('09:30:00');
    expect(minutesToTime(90)).toBe('01:30:00');
  });

  it('trims a time string to HH:mm for display', () => {
    expect(formatHourMinute('09:00:00')).toBe('09:00');
    expect(formatHourMinute(undefined)).toBe('');
  });
});

describe('window validity', () => {
  it('is active when today is within [validFrom, validTo)', () => {
    expect(isWindowActiveToday(win({}))).toBe(true);
  });

  it('treats validTo as exclusive (half-open)', () => {
    // validTo === today → not active today.
    expect(isWindowActiveOn(win({ validTo: ISO(today) }), today)).toBe(false);
  });

  it('is inactive before validFrom', () => {
    expect(isWindowActiveOn(win({ validFrom: ISO(today.add(2, 'day')) }), today)).toBe(
      false,
    );
  });

  it('classifies a past-only window as historical', () => {
    const w = win({ validFrom: ISO(today.subtract(10, 'day')), validTo: ISO(today) });
    expect(isWindowHistorical(w)).toBe(true);
    expect(isWindowFuture(w)).toBe(false);
  });

  it('classifies a not-yet-started window as future', () => {
    const w = win({ validFrom: ISO(today.add(3, 'day')) });
    expect(isWindowFuture(w)).toBe(true);
    expect(isWindowHistorical(w)).toBe(false);
  });
});

describe('weeklyHours', () => {
  it('sums active windows, ignoring inactive ones', () => {
    const windows = [
      win({ dayOfWeek: 1 }), // 8h
      win({ dayOfWeek: 2, startTime: '09:00:00', endTime: '13:00:00' }), // 4h
      win({ dayOfWeek: 3, validFrom: ISO(today.add(5, 'day')) }), // future → ignored
    ];
    expect(weeklyHours(windows)).toBe(12);
  });
});

describe('patternSummary', () => {
  it('reports empty when no window is active', () => {
    expect(patternSummary([win({ validFrom: ISO(today.add(5, 'day')) })])).toEqual({
      kind: 'empty',
    });
  });

  it('reports a single active day', () => {
    const s = patternSummary([win({ dayOfWeek: 1 })]);
    expect(s).toMatchObject({ kind: 'single', dayIdx: 0 });
  });

  it('reports a contiguous block when days are adjacent with the same slot', () => {
    const s = patternSummary([
      win({ dayOfWeek: 1 }),
      win({ dayOfWeek: 2 }),
      win({ dayOfWeek: 3 }),
    ]);
    expect(s).toMatchObject({ kind: 'contiguous', firstDayIdx: 0, lastDayIdx: 2 });
  });

  it('reports variable when days are non-adjacent', () => {
    const s = patternSummary([win({ dayOfWeek: 1 }), win({ dayOfWeek: 3 })]);
    expect(s).toMatchObject({ kind: 'variable', dayIndices: [0, 2] });
  });

  it('reports variable when adjacent days carry different slots', () => {
    const s = patternSummary([
      win({ dayOfWeek: 1, endTime: '13:00:00' }),
      win({ dayOfWeek: 2, endTime: '17:00:00' }),
    ]);
    expect(s.kind).toBe('variable');
  });
});

describe('formatPatternSummary', () => {
  const labels = ['Lun', 'Mar', 'Mer', 'Gio', 'Ven', 'Sab', 'Dom'];
  const fb = { empty: 'Nessuna', variable: 'orari vari' };

  it('renders each summary kind with the supplied day labels', () => {
    expect(formatPatternSummary({ kind: 'empty' }, labels, fb)).toBe('Nessuna');
    expect(
      formatPatternSummary({ kind: 'single', dayIdx: 0, slots: ['09:00–17:00'] }, labels, fb),
    ).toBe('Lun · 09:00–17:00');
    expect(
      formatPatternSummary(
        { kind: 'contiguous', firstDayIdx: 0, lastDayIdx: 2, slots: ['09:00–17:00'] },
        labels,
        fb,
      ),
    ).toBe('Lun–Mer · 09:00–17:00');
    expect(
      formatPatternSummary({ kind: 'variable', dayIndices: [0, 2] }, labels, fb),
    ).toBe('Lun, Mer · orari vari');
  });
});
