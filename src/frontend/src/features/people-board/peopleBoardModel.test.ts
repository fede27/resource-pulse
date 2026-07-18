import { describe, expect, it } from 'vitest';
import dayjs from 'dayjs';
import { buildGeo } from '@/components/board';
import type { LoadBand } from '@/lib/loadBands';
import {
  breakdownGrain,
  bucketComposition,
  bucketStat,
  bucketsFromGeo,
  bucketsInPeriod,
  capacityInWindow,
  capacityMapFromSegments,
  groupPeople,
  matchesBands,
  matchesQuery,
  peopleKpis,
  personLanes,
  personStats,
  proposalPercent,
  resolvePeriod,
  rootIdFromPath,
  snapISO,
  sortPeople,
  subPeriods,
  toOpenDemand,
  todayBucketIdx,
  toPersonBlock,
  weeklyCapacity,
  type PersonBlock,
  type PersonData,
} from './peopleBoardModel';

const FENCE = { todayISO: '2026-06-10', frozenEndISO: '2026-06-24', slushyEndISO: '2026-08-10' };
const BANDS: LoadBand[] = [
  { label: 'Sotto', lowerBound: 0 },
  { label: 'Sano', lowerBound: 85 },
  { label: 'Pieno', lowerBound: 100 },
  { label: 'Sovraccarico', lowerBound: 110 },
];

// Weekday capacity 8h over [from, to] inclusive; weekends 0.
function weekdayCapacity(from: string, to: string): Map<string, number> {
  const map = new Map<string, number>();
  const start = new Date(from);
  const end = new Date(to);
  for (let d = new Date(start); d <= end; d.setDate(d.getDate() + 1)) {
    const iso = d.toISOString().slice(0, 10);
    const wd = (d.getDay() + 6) % 7;
    map.set(iso, wd >= 5 ? 0 : 8);
  }
  return map;
}

function block(partial: Partial<PersonBlock>): PersonBlock {
  return {
    id: 'b1',
    demandId: 'd1',
    rootProjectId: 'p-acme',
    projectName: 'ACME',
    from: '2026-06-01',
    to: '2026-06-14',
    percent: 50,
    hard: true,
    demandRoleName: 'Dev',
    resourceRoleName: 'Dev',
    mismatch: false,
    notes: null,
    ...partial,
  };
}

function person(id: string, blocks: PersonBlock[], overrides?: Partial<PersonData['person']>): PersonData {
  const capacityByDay = weekdayCapacity('2026-06-01', '2026-06-28');
  return {
    person: { id, name: id, roleId: 'role-dev', roleName: 'Dev', teamName: 'Alpha', ...overrides },
    blocks,
    capacityByDay,
    weeklyCapH: 40,
  };
}

// 2026-06-01 is a Monday; four full weeks to 2026-06-29 (exclusive edge).
const geo = buildGeo('2026-06-01', '2026-06-29', 'week', FENCE);

describe('bucketsFromGeo', () => {
  it('maps unit ticks to clamped date buckets', () => {
    const buckets = bucketsFromGeo(geo);
    expect(buckets).toHaveLength(4);
    expect(buckets[0]).toMatchObject({ from: '2026-06-01', toExcl: '2026-06-08' });
    expect(buckets[3]).toMatchObject({ from: '2026-06-22', toExcl: '2026-06-29' });
  });

  it('clamps the first week bucket when the domain starts mid-week', () => {
    const g = buildGeo('2026-06-03', '2026-06-15', 'week', FENCE);
    const buckets = bucketsFromGeo(g);
    expect(buckets[0]?.from).toBe('2026-06-03'); // not the Monday before the domain
  });
});

describe('bucketStat', () => {
  const buckets = bucketsFromGeo(geo);
  const data = person('luca', [
    block({ id: 'h', percent: 50, hard: true, from: '2026-06-01', to: '2026-06-14' }),
    block({ id: 't', percent: 30, hard: false, from: '2026-06-01', to: '2026-06-07' }),
  ]);

  it('computes the hard-only bucket average as hours ÷ capacity', () => {
    const s = bucketStat(data, buckets[0]!, false);
    // 5 weekdays × 8h = 40h capacity; 50% × 40h = 20h allocated.
    expect(s.capH).toBe(40);
    expect(s.allocH).toBe(20);
    expect(s.pct).toBe(50);
  });

  it('adds tentative blocks when requested', () => {
    const s = bucketStat(data, buckets[0]!, true);
    expect(s.pct).toBe(80); // 50 hard + 30 tentative
  });

  it('is zero after the blocks end', () => {
    const s = bucketStat(data, buckets[3]!, false);
    expect(s.pct).toBe(0);
    expect(s.active).toBe(false);
  });

  it('marks active blocks on zero capacity as off-calendar (pct null, 0h — never ∞)', () => {
    const noCap: PersonData = { ...data, capacityByDay: new Map() };
    const s = bucketStat(noCap, buckets[0]!, false);
    expect(s.pct).toBeNull(); // utilization undefined, not Infinity
    expect(s.allocH).toBe(0); // hours truth: 0h counted (ADR-0026)
    expect(s.active).toBe(true);
    expect(s.offCalendar).toBe(true);
  });

  it('a zero-capacity bucket without blocks is not off-calendar', () => {
    const noCap: PersonData = { ...person('idle', []), capacityByDay: new Map() };
    const s = bucketStat(noCap, buckets[0]!, false);
    expect(s.pct).toBeNull();
    expect(s.offCalendar).toBe(false);
  });

  it('lands exactly on band floors (no 69.999… float drift at 70%)', () => {
    // 70% × 8h × 5 days accumulates float error unrounded; the pct must equal
    // the value the cell displays, or a 70-floor band pill reads "under 70%".
    const seventy = person('elena', [block({ percent: 70, from: '2026-06-01', to: '2026-06-28' })]);
    const s = bucketStat(seventy, buckets[0]!, false);
    expect(s.pct).toBe(70);

    const bandsAt70 = [
      { label: 'Under 70%', lowerBound: 0 },
      { label: '70–110%', lowerBound: 70 },
      { label: 'Over', lowerBound: 110 },
    ];
    const stats = personStats(seventy, buckets, false);
    expect(stats.peak).toBe(70);
    expect(matchesBands(seventy, buckets, new Set([1]), bandsAt70, false)).toBe(true); // 70 → 70-band
    expect(matchesBands(seventy, buckets, new Set([0]), bandsAt70, false)).toBe(false); // not "under"
  });
});

describe('personStats / KPIs', () => {
  const buckets = bucketsFromGeo(geo);

  it('finds peak and min across buckets', () => {
    const data = person('luca', [block({ percent: 120, from: '2026-06-01', to: '2026-06-07' })]);
    const s = personStats(data, buckets, false);
    expect(s.peak).toBe(120);
    expect(s.min).toBe(0);
  });

  it('off-calendar buckets never pollute the peak (no ∞ row state)', () => {
    // Capacity only in week 1: weeks 2–4 are zero-capacity but the block is
    // active there — they must not flip the person into "overloaded at peak".
    const data = person('elena', [block({ percent: 60, from: '2026-06-01', to: '2026-06-28' })]);
    const week1Only = new Map(
      [...data.capacityByDay].filter(([iso]) => iso < '2026-06-08'),
    );
    const s = personStats({ ...data, capacityByDay: week1Only }, buckets, false);
    expect(Number.isFinite(s.peak)).toBe(true);
    expect(s.peak).toBe(60);
  });

  it('counts overloaded and underused people from the configured bands', () => {
    const over = person('over', [block({ percent: 120, from: '2026-06-01', to: '2026-06-28' })]);
    const idle = person('idle', [block({ percent: 40, from: '2026-06-01', to: '2026-06-28' })]);
    const healthy = person('ok', [block({ percent: 90, from: '2026-06-01', to: '2026-06-28' })]);
    const stats = new Map(
      [over, idle, healthy].map((d) => [d.person.id, personStats(d, buckets, false)]),
    );
    const kpis = peopleKpis([over, idle, healthy], (id) => stats.get(id)!, BANDS);
    expect(kpis.overloaded).toBe(1); // ≥110
    expect(kpis.underused).toBe(1); // peak < 85
  });
});

describe('bucketComposition', () => {
  it('splits the bucket average by root project, shares summing to the total', () => {
    const buckets = bucketsFromGeo(geo);
    const data = person('luca', [
      block({ id: 'a', rootProjectId: 'p-acme', projectName: 'ACME', percent: 50 }),
      block({ id: 'b', rootProjectId: 'p-beta', projectName: 'BETA', percent: 30 }),
    ]);
    const { total, byProject } = bucketComposition(data, buckets[0]!, false);
    expect(total.pct).toBe(80);
    expect(byProject).toHaveLength(2);
    expect(byProject[0]).toMatchObject({ projectName: 'ACME', hours: 20, pct: 50 });
    expect(byProject[1]).toMatchObject({ projectName: 'BETA', hours: 12, pct: 30 });
  });
});

describe('personLanes', () => {
  it('groups blocks by root project ordered by earliest start', () => {
    const lanes = personLanes([
      block({ id: 'b1', rootProjectId: 'p-beta', projectName: 'BETA', from: '2026-06-08' }),
      block({ id: 'a1', rootProjectId: 'p-acme', projectName: 'ACME', from: '2026-06-01' }),
      block({ id: 'a2', rootProjectId: 'p-acme', projectName: 'ACME', from: '2026-06-15' }),
    ]);
    expect(lanes.map((l) => l.projectName)).toEqual(['ACME', 'BETA']);
    expect(lanes[0]?.blocks.map((b) => b.id)).toEqual(['a1', 'a2']);
  });
});

describe('filters, grouping, sorting', () => {
  const buckets = bucketsFromGeo(geo);
  const luca = person('luca', [block({ percent: 120 })], { name: 'Luca Ferri' });
  const elena = person('elena', [], { name: 'Elena Neri', roleId: null, roleName: null, teamName: 'Beta' });

  it('matchesQuery searches name and role', () => {
    expect(matchesQuery(luca.person, 'luc')).toBe(true);
    expect(matchesQuery(luca.person, 'dev')).toBe(true);
    expect(matchesQuery(luca.person, 'grafico')).toBe(false);
    expect(matchesQuery(luca.person, '')).toBe(true);
  });

  it('matchesBands hits when any bucket falls in a selected band', () => {
    expect(matchesBands(luca, buckets, new Set([3]), BANDS, false)).toBe(true); // 120 → overload band
    expect(matchesBands(elena, buckets, new Set([3]), BANDS, false)).toBe(false);
    expect(matchesBands(elena, buckets, new Set(), BANDS, false)).toBe(true); // empty = no filter
  });

  it('matchesBands ignores off-calendar buckets (no band, not overload)', () => {
    // Active block, zero capacity everywhere: no bucket has a band, so the
    // person matches no band selection (previously leaked into overload).
    const offCal = { ...luca, capacityByDay: new Map<string, number>() };
    for (let i = 0; i < BANDS.length; i += 1) {
      expect(matchesBands(offCal, buckets, new Set([i]), BANDS, false)).toBe(false);
    }
    expect(matchesBands(offCal, buckets, new Set(), BANDS, false)).toBe(true);
  });

  it('groups by role with the role-less group last', () => {
    const groups = groupPeople([luca, elena], 'role');
    expect(groups.map((g) => g.label)).toEqual(['Dev', null]);
  });

  it('sorts by severity (peak desc) and by idle (min asc)', () => {
    const stats = new Map(
      [luca, elena].map((d) => [d.person.id, personStats(d, buckets, false)]),
    );
    const statsOf = (id: string) => stats.get(id)!;
    expect(sortPeople([elena, luca], 'severity', statsOf)[0]?.person.id).toBe('luca');
    expect(sortPeople([luca, elena], 'idle', statsOf)[0]?.person.id).toBe('elena');
  });
});

describe('drag helpers', () => {
  it('snaps to Monday for the week grain', () => {
    expect(snapISO('2026-06-03', 'week')).toBe('2026-06-01'); // Wed → its Monday
    expect(snapISO('2026-06-05', 'week')).toBe('2026-06-08'); // Fri → next Monday
    expect(snapISO('2026-06-03', 'day')).toBe('2026-06-03');
  });

  it('computes the proposal percent from the residual over the window capacity', () => {
    expect(proposalPercent(60, 120)).toBe(50);
    expect(proposalPercent(null, 120)).toBe(40); // best-effort → default
    expect(proposalPercent(500, 100)).toBe(100); // clamped
    expect(proposalPercent(2, 100)).toBe(10); // floor
  });

  it('sums inclusive-window capacity', () => {
    const cap = weekdayCapacity('2026-06-01', '2026-06-14');
    expect(capacityInWindow(cap, '2026-06-01', '2026-06-07')).toBe(40);
  });
});

describe('subPeriods / breakdownGrain', () => {
  it('decomposes a short period into days and a long one into weeks', () => {
    const days = subPeriods('2026-06-01', '2026-06-08', 'day');
    expect(days).toHaveLength(7);
    expect(days[0]).toMatchObject({ from: '2026-06-01', toExcl: '2026-06-02' });

    const weeks = subPeriods('2026-06-01', '2026-07-01', 'week');
    expect(weeks.length).toBeGreaterThanOrEqual(4);
    expect(weeks[0]?.label).toMatch(/^S\d+/);
  });

  it('picks the breakdown grain from the span (adaptive, revised design)', () => {
    expect(breakdownGrain('2026-06-01', '2026-06-02')).toBeNull(); // single day
    expect(breakdownGrain('2026-06-01', '2026-06-08')).toBe('day'); // one week
    expect(breakdownGrain('2026-06-01', '2026-07-01')).toBe('week'); // a month
  });
});

describe('resolvePeriod', () => {
  // Week buckets over June 2026 (Mon 2026-06-01 + 4 weeks); today mid-week 2.
  const buckets = [0, 1, 2, 3, 4].map((i) => ({
    from: dayjs('2026-06-01').add(i * 7, 'day').format('YYYY-MM-DD'),
    toExcl: dayjs('2026-06-01').add((i + 1) * 7, 'day').format('YYYY-MM-DD'),
    x: 0,
    w: 0,
    label: `S${23 + i}`,
  }));
  const today = '2026-06-10'; // inside bucket 1

  it('finds the bucket containing today (or the first after)', () => {
    expect(todayBucketIdx(buckets, today)).toBe(1);
    expect(todayBucketIdx(buckets, '2026-05-01')).toBe(0); // before horizon → first upcoming
    expect(todayBucketIdx(buckets, '2027-01-01')).toBe(0); // fully past horizon → fallback
  });

  it("'current' = today's bucket, 'next' = current + 3, 'all' = the horizon", () => {
    expect(resolvePeriod('current', buckets, today, null, null)).toEqual({
      from: '2026-06-08',
      toExcl: '2026-06-15',
    });
    expect(resolvePeriod('next', buckets, today, null, null)).toEqual({
      from: '2026-06-08',
      toExcl: '2026-07-06', // buckets 1..4 (clamped at the last)
    });
    expect(resolvePeriod('all', buckets, today, null, null)).toEqual({
      from: '2026-06-01',
      toExcl: '2026-07-06',
    });
  });

  it("'cell' uses the clicked bucket and 'custom' the picked range", () => {
    expect(resolvePeriod('cell', buckets, today, null, buckets[3]!)).toEqual({
      from: '2026-06-22',
      toExcl: '2026-06-29',
    });
    const custom = { from: '2026-06-03', toExcl: '2026-06-20' };
    expect(resolvePeriod('custom', buckets, today, custom, null)).toEqual(custom);
  });

  it('counts the board buckets a period spans (the "media N" note)', () => {
    expect(bucketsInPeriod(buckets, { from: '2026-06-08', toExcl: '2026-07-06' })).toBe(4);
    expect(bucketsInPeriod(buckets, { from: '2026-06-10', toExcl: '2026-06-11' })).toBe(1);
  });
});

describe('normalization', () => {
  it('resolves the root project from the materialized path', () => {
    expect(rootIdFromPath('/p-acme/p-phase')).toBe('p-acme');
    expect(rootIdFromPath('/p-acme')).toBe('p-acme');
    expect(rootIdFromPath(undefined)).toBe('');
  });

  it('maps an allocation to a block with the DTO-resolved root project (P3)', () => {
    const b = toPersonBlock({
      id: 'a1',
      demandId: 'd1',
      resourceId: 'r1',
      projectNodePath: '/p-acme/p-phase',
      rootProjectId: 'p-acme',
      rootProjectName: 'Portale ACME',
      periodStart: '2026-06-01',
      periodEnd: '2026-06-14',
      allocationPercent: 60,
      status: 1,
      resourceRoleName: 'Dev',
      demandRoleName: 'Grafico',
    });
    expect(b.rootProjectId).toBe('p-acme');
    expect(b.projectName).toBe('Portale ACME');
    expect(b.hard).toBe(true);
    expect(b.mismatch).toBe(true); // Dev covering a Grafico demand
  });

  it('parses open demands with a null gap as best-effort', () => {
    const d = toOpenDemand({
      demandId: 'd1',
      rootProjectId: 'p',
      rootProjectName: 'ACME',
      roleName: 'Dev',
      coveredHours: 'PT10H',
      gapHours: null,
    });
    expect(d.residualH).toBeNull();
    expect(d.coveredH).toBe(10);
  });

  it('computes the average weekly capacity over the fetched range', () => {
    const cap = weekdayCapacity('2026-06-01', '2026-06-14');
    expect(weeklyCapacity(cap, '2026-06-01', '2026-06-14')).toBe(40);
  });

  it('expands run-length capacity segments to a per-day map, gaps as absent keys', () => {
    // Two Mon–Fri 8h runs (batch endpoint form); the weekend between is a gap.
    const map = capacityMapFromSegments([
      { from: '2026-06-01', to: '2026-06-05', hoursPerDay: 'PT8H' },
      { from: '2026-06-08', to: '2026-06-12', hoursPerDay: '04:00:00' },
    ]);

    expect(map.get('2026-06-01')).toBe(8);
    expect(map.get('2026-06-05')).toBe(8);
    expect(map.has('2026-06-06')).toBe(false); // weekend gap = zero capacity
    expect(map.get('2026-06-08')).toBe(4); // constant-format duration parses too
    expect(map.size).toBe(10);

    // Parity with the old daily form: the derived weekly average is unchanged.
    expect(weeklyCapacity(map, '2026-06-01', '2026-06-14')).toBe(30);
  });
});
