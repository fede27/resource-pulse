import { describe, expect, it } from 'vitest';
import {
  capacityHours,
  convertAmount,
  coverBuckets,
  coverDays,
  demandBalance,
  freeHours,
  grainFor,
  hoursForPercent,
  hoursToTimeSpan,
  percentForHours,
  presentDays,
  rowHours,
  rowPercent,
} from './coverModel';

// The study's case: DevOps Engineer on «Test 01», 2–27 Nov 2026, 160 h.
// Four full weeks, Mon–Fri at 8 h.
const FROM = '2026-11-02';
const TO = '2026-11-27';

function capacity(closedWeekFrom?: string): Map<string, number> {
  const map = new Map<string, number>();
  for (let day = 2; day <= 27; day += 1) {
    const iso = `2026-11-${String(day).padStart(2, '0')}`;
    const weekday = new Date(`${iso}T00:00:00`).getDay();
    if (weekday === 0 || weekday === 6) continue;
    if (closedWeekFrom && iso >= closedWeekFrom && iso <= '2026-11-20') continue;
    map.set(iso, 8);
  }
  return map;
}

// Davide: 50% on another project the first two weeks, 20% the third, free the fourth.
const davide = coverDays(FROM, TO, capacity(), [
  { from: '2026-11-02', to: '2026-11-15', percent: 50 },
  { from: '2026-11-16', to: '2026-11-22', percent: 20 },
  { from: '2026-11-23', to: '2026-11-27', percent: 0 },
]);
// Chiara: 30% elsewhere, on holiday 16–20 Nov.
const chiara = coverDays(FROM, TO, capacity('2026-11-16'), [{ from: FROM, to: TO, percent: 30 }]);

describe('a person inside the window', () => {
  it('has the capacity of the days they are there', () => {
    expect(capacityHours(davide)).toBe(160);
    expect(presentDays(davide)).toBe(20);
    expect(capacityHours(chiara)).toBe(120);
    expect(presentDays(chiara)).toBe(15);
  });

  it('has free hours once what they already carry is taken out', () => {
    // 2 weeks at 50% free + 1 at 80% + 1 at 100%: 40 + 32 + 40.
    expect(freeHours(davide)).toBe(112);
    expect(freeHours(chiara)).toBe(84);
  });

  it('a day already over 100% frees nothing and owes nothing', () => {
    const swamped = coverDays(FROM, FROM, capacity(), [{ from: FROM, to: FROM, percent: 130 }]);
    expect(freeHours(swamped)).toBe(0);
  });
});

describe('hours and quota, for that person', () => {
  it('the same hours are a different quota of different people', () => {
    expect(percentForHours(160, davide)).toBe(100);
    expect(percentForHours(160, chiara)).toBe(133.33);
    expect(percentForHours(112, davide)).toBe(70);
  });

  it('the same quota is different hours of different people', () => {
    expect(hoursForPercent(50, davide)).toBe(80);
    expect(hoursForPercent(50, chiara)).toBe(60);
  });

  it('no capacity, no quota: nothing can be created on that person', () => {
    const absent = coverDays(FROM, TO, undefined, []);
    expect(percentForHours(80, absent)).toBeNull();
  });

  it('a row reads in both units whatever it was typed in', () => {
    const row = { key: 'a', resourceId: 'davide', amount: 112 };
    expect(rowHours(row, 'hours', davide)).toBe(112);
    expect(rowPercent(row, 'hours', davide)).toBe(70);
    expect(rowHours({ ...row, amount: 50 }, 'percent', chiara)).toBe(60);
    expect(rowPercent({ ...row, amount: 50 }, 'percent', chiara)).toBe(50);
  });

  it('an empty row adds nothing', () => {
    expect(rowHours({ key: 'a', resourceId: null, amount: null }, 'hours', davide)).toBe(0);
    expect(rowPercent({ key: 'a', resourceId: null, amount: null }, 'hours', davide)).toBeNull();
  });

  it('flipping the unit keeps the quantity', () => {
    expect(convertAmount(112, 'percent', davide)).toBe(70);
    expect(convertAmount(70, 'hours', davide)).toBe(112);
    expect(convertAmount(112, 'percent', null)).toBeNull();
  });
});

describe('load per week, before → after', () => {
  it('a constant quota on an uneven availability overloads some weeks', () => {
    // 112 h fit in total, yet at a constant 70% the first two weeks go to 120%.
    const weeks = coverBuckets(davide, 70, 'week');
    expect(weeks.map((w) => [w.before, w.after])).toEqual([
      [50, 120],
      [50, 120],
      [20, 90],
      [0, 70],
    ]);
    expect(weeks.map((w) => w.addedHours)).toEqual([28, 28, 28, 28]);
  });

  it('a week without capacity is undefined, not 0% and not overload', () => {
    const weeks = coverBuckets(chiara, 133.33, 'week');
    expect(weeks[2]).toMatchObject({ before: null, after: null, addedHours: 0 });
    expect(weeks[0]).toMatchObject({ before: 30, after: 163 });
  });

  it('names a week by its Monday and keeps the days it actually spans', () => {
    const weeks = coverBuckets(davide, 50, 'week');
    expect(weeks[0]).toMatchObject({ key: '2026-11-02', from: '2026-11-02', to: '2026-11-08' });
    expect(weeks[3]).toMatchObject({ from: '2026-11-23', to: '2026-11-27' });
  });

  it('switches to months when the window is long', () => {
    expect(grainFor(davide)).toBe('week');
    const year = coverDays('2026-01-01', '2026-12-31', undefined, []);
    expect(grainFor(year)).toBe('month');
    expect(coverBuckets(year, 50, 'month')).toHaveLength(12);
  });
});

describe('the demand', () => {
  it('what this dialog adds comes off what stays uncovered', () => {
    expect(demandBalance(160, 0, 80)).toMatchObject({ adding: 80, left: 80, over: 0 });
    expect(demandBalance(160, 40, 80)).toMatchObject({ covered: 40, left: 40 });
  });

  it('going over the request is shown, not clamped', () => {
    expect(demandBalance(160, 0, 180)).toMatchObject({ left: 0, over: 20 });
  });

  it('a best-effort demand has nothing left and nothing over', () => {
    expect(demandBalance(null, 0, 80)).toMatchObject({ required: null, left: 0, over: 0 });
  });
});

describe('hoursToTimeSpan', () => {
  it('writes the constant format the envelope parses, days included', () => {
    expect(hoursToTimeSpan(20)).toBe('20:00:00');
    expect(hoursToTimeSpan(160)).toBe('6.16:00:00');
    expect(hoursToTimeSpan(9.6)).toBe('09:36:00');
    expect(hoursToTimeSpan(24)).toBe('1.00:00:00');
  });
});
