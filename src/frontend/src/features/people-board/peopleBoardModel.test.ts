import { describe, expect, it } from 'vitest';
import { buildGeo } from '@/components/board';
import type { LoadBand } from '@/lib/loadBands';
import {
  bucketComposition,
  bucketStat,
  bucketsFromGeo,
  capacityInWindow,
  groupPeople,
  matchesBands,
  matchesQuery,
  peopleKpis,
  personLanes,
  personStats,
  proposalPercent,
  rootIdFromPath,
  snapISO,
  sortPeople,
  subBuckets,
  toOpenDemand,
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

  it('mirrors the zero-capacity sentinel as Infinity', () => {
    const noCap: PersonData = { ...data, capacityByDay: new Map() };
    const s = bucketStat(noCap, buckets[0]!, false);
    expect(s.pct).toBe(Number.POSITIVE_INFINITY);
    expect(s.active).toBe(true);
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

describe('subBuckets', () => {
  it('decomposes a week into days and a month into weeks', () => {
    const days = subBuckets('2026-06-01', '2026-06-08', 'week');
    expect(days).toHaveLength(7);
    expect(days[0]).toMatchObject({ from: '2026-06-01', toExcl: '2026-06-02' });

    const weeks = subBuckets('2026-06-01', '2026-07-01', 'month');
    expect(weeks.length).toBeGreaterThanOrEqual(4);
    expect(weeks[0]?.label).toMatch(/^S\d+/);
  });
});

describe('normalization', () => {
  it('resolves the root project from the materialized path', () => {
    expect(rootIdFromPath('/p-acme/p-phase')).toBe('p-acme');
    expect(rootIdFromPath('/p-acme')).toBe('p-acme');
    expect(rootIdFromPath(undefined)).toBe('');
  });

  it('maps an allocation to a block with the project name joined client-side', () => {
    const b = toPersonBlock(
      {
        id: 'a1',
        demandId: 'd1',
        resourceId: 'r1',
        projectNodePath: '/p-acme/p-phase',
        periodStart: '2026-06-01',
        periodEnd: '2026-06-14',
        allocationPercent: 60,
        status: 1,
        resourceRoleName: 'Dev',
        demandRoleName: 'Grafico',
      },
      new Map([['p-acme', 'Portale ACME']]),
    );
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
});
