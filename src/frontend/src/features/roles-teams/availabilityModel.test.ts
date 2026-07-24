import { describe, expect, it } from 'vitest';
import {
  AdjustmentType,
  DayOfWeek,
  type BusinessCalendarReadDto,
  type CompanyClosureReadDto,
  type IndividualAdjustmentDto,
  type ResourceReadDto,
  type WorkWindowDto,
} from '@/api/generated/schemas';
import {
  baseHoursOn,
  buildBuckets,
  bucketAgg,
  dayInfo,
  groupByTeam,
  hoursToWire,
  patternFor,
  sumEffective,
  sumNominal,
  windowsWeeklyHours,
} from './availabilityModel';

// Week of Mon 2026-07-06 … Sun 2026-07-12.
const MON = '2026-07-06';
const WED = '2026-07-08';
const SAT = '2026-07-11';

const win = (dayOfWeek: DayOfWeek): WorkWindowDto => ({
  dayOfWeek,
  startTime: '09:00:00',
  endTime: '17:00:00',
  validFrom: '2020-01-01',
  validTo: null,
});

// Mon–Fri 09–17 (8h/day).
const WEEKDAYS: DayOfWeek[] = [
  DayOfWeek.Monday,
  DayOfWeek.Tuesday,
  DayOfWeek.Wednesday,
  DayOfWeek.Thursday,
  DayOfWeek.Friday,
];
const CAL: BusinessCalendarReadDto = {
  id: 'cal-1',
  name: 'Standard',
  isDefault: true,
  workWindows: WEEKDAYS.map(win),
};

const person = (over?: Partial<ResourceReadDto>): ResourceReadDto => ({
  id: 'p1',
  name: 'Tizio',
  isActive: true,
  businessCalendarId: 'cal-1',
  workWindows: [],
  adjustments: [],
  ...over,
});

const adj = (
  type: AdjustmentType,
  dateFrom: string,
  dateTo: string,
  hours: string | null,
): IndividualAdjustmentDto => ({ id: `a-${dateFrom}`, type, dateFrom, dateTo, hours, reason: 'x' });

describe('baseHoursOn / patternFor', () => {
  it('sums matching-weekday window durations', () => {
    expect(baseHoursOn(CAL.workWindows ?? [], MON)).toBe(8);
    expect(baseHoursOn(CAL.workWindows ?? [], SAT)).toBe(0);
  });

  it('respects window validity range', () => {
    const expired: WorkWindowDto = { ...win(1), validTo: '2020-12-31' };
    expect(baseHoursOn([expired], MON)).toBe(0);
  });

  it('per-resource overrides win over the calendar', () => {
    const p = person({ workWindows: [{ ...win(1), startTime: '10:00:00', endTime: '14:00:00' }] });
    expect(patternFor(p, CAL)).toHaveLength(1);
    expect(baseHoursOn(patternFor(p, CAL), MON)).toBe(4);
  });

  it('falls back to the calendar when no overrides', () => {
    expect(patternFor(person(), CAL)).toBe(CAL.workWindows);
  });
});

describe('dayInfo', () => {
  it('classifies a plain working day', () => {
    const di = dayInfo(person(), CAL, [], MON);
    expect(di.state).toBe('work');
    expect(di.baseHours).toBe(8);
    expect(di.weekend).toBe(false);
  });

  it('classifies a non-working weekend day', () => {
    const di = dayInfo(person(), CAL, [], SAT);
    expect(di.state).toBe('off');
    expect(di.baseHours).toBe(0);
    expect(di.weekend).toBe(true);
  });

  it('a company closure zeros the base and sets closure state', () => {
    const closure: CompanyClosureReadDto = { id: 'c1', dateFrom: WED, dateTo: WED, reason: 'Festa' };
    const di = dayInfo(person(), CAL, [closure], WED);
    expect(di.state).toBe('closure');
    expect(di.closure?.reason).toBe('Festa');
  });

  it('a full-day absence subtracts the whole base', () => {
    const p = person({ adjustments: [adj(AdjustmentType.Absence, MON, MON, null)] });
    const di = dayInfo(p, CAL, [], MON);
    expect(di.state).toBe('ferie');
    expect(di.ferieHours).toBe(8);
  });

  it('a partial absence subtracts the given hours', () => {
    const p = person({ adjustments: [adj(AdjustmentType.Absence, MON, MON, '04:00:00')] });
    const di = dayInfo(p, CAL, [], MON);
    expect(di.state).toBe('ferie');
    expect(di.ferieHours).toBe(4);
  });

  it('extra time adds hours even on a non-working day', () => {
    const p = person({ adjustments: [adj(AdjustmentType.ExtraTime, SAT, SAT, '06:00:00')] });
    const di = dayInfo(p, CAL, [], SAT);
    expect(di.state).toBe('extra');
    expect(di.extraHours).toBe(6);
  });
});

describe('buckets', () => {
  it('builds inclusive weekly buckets from a Monday', () => {
    const buckets = buildBuckets(MON, 'week', 2);
    expect(buckets).toEqual([
      { from: '2026-07-06', to: '2026-07-12' },
      { from: '2026-07-13', to: '2026-07-19' },
    ]);
  });

  it('builds single-day buckets', () => {
    const buckets = buildBuckets(MON, 'day', 3);
    expect(buckets).toEqual([
      { from: '2026-07-06', to: '2026-07-06' },
      { from: '2026-07-07', to: '2026-07-07' },
      { from: '2026-07-08', to: '2026-07-08' },
    ]);
  });
});

describe('bucketAgg / sums', () => {
  const capacity = new Map<string, number>([
    ['2026-07-06', 8],
    ['2026-07-07', 8],
    ['2026-07-08', 8],
    ['2026-07-09', 8],
    ['2026-07-10', 8],
  ]);

  it('sums effective hours from the capacity map and rolls up state', () => {
    const agg = bucketAgg(person(), CAL, [], capacity, { from: MON, to: '2026-07-12' });
    expect(agg.hours).toBe(40);
    expect(agg.state).toBe('work');
    expect(agg.hasFerie).toBe(false);
  });

  it('flags ferie in a bucket touched by an absence', () => {
    const p = person({ adjustments: [adj(AdjustmentType.Absence, WED, WED, null)] });
    const reduced = new Map(capacity);
    reduced.set('2026-07-08', 0);
    const agg = bucketAgg(p, CAL, [], reduced, { from: MON, to: '2026-07-12' });
    expect(agg.hasFerie).toBe(true);
    expect(agg.hours).toBe(32);
  });

  it('sumEffective / sumNominal report range totals', () => {
    expect(sumEffective(capacity, MON, '2026-07-12')).toBe(40);
    expect(sumNominal(person(), CAL, MON, '2026-07-12')).toBe(40);
  });
});

describe('helpers', () => {
  it('hoursToWire renders the constant TimeSpan format', () => {
    expect(hoursToWire(4)).toBe('04:00:00');
    expect(hoursToWire(4.5)).toBe('04:30:00');
    expect(hoursToWire(0)).toBe('00:00:00');
  });

  it('windowsWeeklyHours sums a Mon–Fri pattern', () => {
    expect(windowsWeeklyHours(CAL.workWindows ?? [])).toBe(40);
  });

  it('groupByTeam preserves order and puts the null team last', () => {
    const items = [
      { id: '1', teamId: 't1' },
      { id: '2', teamId: null },
      { id: '3', teamId: 't1' },
    ];
    const groups = groupByTeam(
      items,
      (i) => i.teamId,
      new Map([['t1', 'Alpha']]),
      'Senza team',
    );
    expect(groups.map((g) => g.teamName)).toEqual(['Alpha', 'Senza team']);
    expect(groups[0]?.members).toHaveLength(2);
    expect(groups[1]?.members).toHaveLength(1);
  });
});
