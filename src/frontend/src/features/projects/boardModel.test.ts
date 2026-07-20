import { describe, expect, it } from 'vitest';
import {
  AllocationStatus,
  DemandProvenance,
  ProjectNodeType,
  ProjectStatus,
  type AllocationReadDto,
  type DemandCoverageDto,
  type ProjectNodeReadDto,
} from '@/api/generated/schemas';
import { ENVELOPE_H, LANE_H } from '@/components/board';
import {
  activeFilterCount,
  allRoles,
  availableActionKinds,
  buildBoardProject,
  defaultFilters,
  demandRowStatus,
  filterProjects,
  holeIsMine,
  lifecycleOf,
  peakOf,
  portfolioHealth,
  projectRowHeight,
  projectVerdict,
  projectsExtent,
  sortProjects,
  statusChipKey,
  tentativeNotesOf,
  toCoverageBlock,
  toDemandRow,
  type BoardProject,
} from './boardModel';

const root = (over: Partial<ProjectNodeReadDto> = {}): ProjectNodeReadDto => ({
  id: 'p1',
  nodeType: ProjectNodeType.Project,
  name: 'Portale ACME',
  client: 'ACME S.p.A.',
  leadResourceId: 'anna',
  leadResourceName: 'Anna Bianchi',
  commitmentLevel: 3,
  isProposed: false,
  plannedStart: '2026-05-11',
  plannedEnd: '2026-09-07',
  ...over,
});

const coverage = (over: Partial<DemandCoverageDto> = {}): DemandCoverageDto => ({
  demandId: 'd1',
  projectNodeId: 'p1',
  roleId: 'r1',
  roleName: 'Dev senior',
  provenance: DemandProvenance.Declared,
  requiredHours: 'PT340H',
  coveredHours: 'PT300H',
  gapHours: 'PT40H',
  ownerResourceId: 'anna',
  ownerResourceName: 'Anna Bianchi',
  ...over,
});

const alloc = (over: Partial<AllocationReadDto> = {}): AllocationReadDto => ({
  id: 'a1',
  demandId: 'd1',
  resourceId: 'luca',
  resourceName: 'Luca Ferri',
  resourceRoleId: 'r1',
  resourceRoleName: 'Dev senior',
  demandRoleId: 'r1',
  demandRoleName: 'Dev senior',
  projectNodeId: 'p1',
  periodStart: '2026-06-08',
  periodEnd: '2026-09-07',
  allocationPercent: 60,
  status: AllocationStatus.Hard,
  ...over,
});

describe('toCoverageBlock', () => {
  it('flags a role mismatch when the person role differs from the demand role', () => {
    const b = toCoverageBlock(alloc({ resourceRoleName: 'Sviluppatore' }));
    expect(b.mismatch).toBe(true);
  });

  it('does not flag a mismatch when the person has no role', () => {
    const b = toCoverageBlock(alloc({ resourceRoleName: null }));
    expect(b.mismatch).toBe(false);
  });

  it('maps Hard status', () => {
    expect(toCoverageBlock(alloc()).hard).toBe(true);
    expect(toCoverageBlock(alloc({ status: AllocationStatus.Tentative })).hard).toBe(false);
  });
});

describe('demandRowStatus', () => {
  it('is noTarget for a best-effort demand (null target)', () => {
    expect(demandRowStatus(null, 120, true)).toBe('noTarget');
  });

  it('is uncovered with a target and no coverage', () => {
    expect(demandRowStatus(60, 0, false)).toBe('uncovered');
  });

  it('is partial / covered / over around the target', () => {
    expect(demandRowStatus(100, 60, true)).toBe('partial');
    expect(demandRowStatus(100, 100, true)).toBe('covered');
    expect(demandRowStatus(100, 110, true)).toBe('over');
  });
});

describe('toDemandRow', () => {
  it('keeps the best-effort gap null — never a fake zero', () => {
    const row = toDemandRow(coverage({ requiredHours: null, gapHours: null, coveredHours: 'PT120H' }), []);
    expect(row.requiredH).toBeNull();
    expect(row.gapH).toBeNull();
    expect(row.uncovered).toBe(false); // no target ⇒ no hole
    expect(row.status).toBe('noTarget');
  });

  it('marks a targeted demand without blocks as uncovered', () => {
    const row = toDemandRow(coverage({ coveredHours: 'PT0S', gapHours: 'PT340H' }), []);
    expect(row.uncovered).toBe(true);
    expect(row.status).toBe('uncovered');
  });

  it('caps useful hours at the target and flags the excess', () => {
    const row = toDemandRow(
      coverage({ requiredHours: 'PT100H', coveredHours: 'PT130H', gapHours: '-PT30H' }),
      [toCoverageBlock(alloc())],
    );
    expect(row.usefulH).toBe(100);
    expect(row.overH).toBe(30);
    expect(row.status).toBe('over');
  });
});

function makeProject(over: {
  demands?: DemandCoverageDto[];
  allocations?: AllocationReadDto[];
  root?: Partial<ProjectNodeReadDto>;
}): BoardProject {
  return buildBoardProject(
    root(over.root ?? {}),
    [
      root(),
      root({ id: 'ph1', nodeType: ProjectNodeType.Phase, name: 'Analisi', plannedStart: '2026-05-11', plannedEnd: '2026-06-08' }),
    ],
    over.demands ?? [coverage()],
    over.allocations ?? [alloc()],
  );
}

describe('buildBoardProject', () => {
  it('groups coverage blocks under their demand and extracts phases', () => {
    const p = makeProject({});
    expect(p.name).toBe('Portale ACME');
    expect(p.client).toBe('ACME S.p.A.');
    expect(p.phases).toHaveLength(1);
    expect(p.phases[0]!.label).toBe('Analisi');
    expect(p.demands).toHaveLength(1);
    expect(p.demands[0]!.coverage).toHaveLength(1);
    expect(p.people).toEqual(['luca']);
    expect(p.holes).toHaveLength(0);
  });

  it('collects uncovered demands as holes and totals the gap', () => {
    const p = makeProject({
      demands: [
        coverage(),
        coverage({ demandId: 'd2', roleName: 'Grafico', requiredHours: 'PT60H', coveredHours: 'PT0S', gapHours: 'PT60H' }),
      ],
    });
    expect(p.holes).toHaveLength(1);
    expect(p.holes[0]!.roleName).toBe('Grafico');
    expect(p.totals.gapH).toBe(100); // 40 + 60
  });
});

describe('projectRowHeight', () => {
  it('is the envelope plus the block border when collapsed', () => {
    expect(projectRowHeight(makeProject({}), false)).toBe(ENVELOPE_H + 1);
  });

  it('adds the lanes-container border plus one LANE_H per block and hole when expanded', () => {
    // 1 coverage block + 1 hole = 2 lanes.
    const p = makeProject({
      demands: [
        coverage(),
        coverage({ demandId: 'd2', roleName: 'Grafico', requiredHours: 'PT60H', coveredHours: 'PT0S', gapHours: 'PT60H' }),
      ],
    });
    expect(projectRowHeight(p, true)).toBe(ENVELOPE_H + 1 + 2 * LANE_H + 1);
  });

  it('stays at the collapsed height when expanded with zero lanes (guard parity with ProjectRow)', () => {
    // Best-effort demand, no coverage: not a hole (no target) and no blocks.
    const p = makeProject({
      demands: [coverage({ requiredHours: null, gapHours: null, coveredHours: 'PT0S' })],
      allocations: [],
    });
    expect(projectRowHeight(p, true)).toBe(ENVELOPE_H + 1);
  });
});

describe('projectVerdict', () => {
  const peaks = (map: Record<string, number>) => (id: string) => map[id] ?? 0;

  it('is uncovered when a targeted demand has no coverage', () => {
    const p = makeProject({
      demands: [coverage({ demandId: 'd2', coveredHours: 'PT0S', gapHours: 'PT340H' })],
      allocations: [],
    });
    expect(projectVerdict(p, peaks({}), 110).verdict).toBe('uncovered');
  });

  it('is atRisk on peak overload', () => {
    const p = makeProject({});
    const r = projectVerdict(p, peaks({ luca: 120 }), 110);
    expect(r.verdict).toBe('atRisk');
    expect(r.reason).toBe('overload');
  });

  it('is atRisk on a role mismatch even without overload', () => {
    const p = makeProject({ allocations: [alloc({ resourceRoleName: 'Sviluppatore' })] });
    const r = projectVerdict(p, peaks({ luca: 60 }), 110);
    expect(r.verdict).toBe('atRisk');
    expect(r.reason).toBe('mismatch');
  });

  it('is sustainable otherwise', () => {
    const p = makeProject({});
    expect(projectVerdict(p, peaks({ luca: 90 }), 110).verdict).toBe('sustainable');
  });
});

describe('lifecycle & filters', () => {
  const today = '2026-07-05';

  it('derives the lifecycle from planned dates vs today', () => {
    expect(lifecycleOf(makeProject({}), today)).toBe('active');
    expect(lifecycleOf(makeProject({ root: { plannedStart: '2026-08-01', plannedEnd: '2026-09-01' } }), today)).toBe('future');
    expect(lifecycleOf(makeProject({ root: { plannedStart: '2026-01-01', plannedEnd: '2026-02-01' } }), today)).toBe('closed');
  });

  it('domain status wins over dates: Closed/Cancelled are closed mid-window', () => {
    expect(lifecycleOf(makeProject({ root: { status: ProjectStatus.Closed } }), today)).toBe('closed');
    expect(
      lifecycleOf(
        makeProject({ root: { status: ProjectStatus.Cancelled, plannedStart: '2026-08-01', plannedEnd: '2026-09-01' } }),
        today,
      ),
    ).toBe('closed');
    // Draft/OnHold do not force a lifecycle — dates still decide.
    expect(lifecycleOf(makeProject({ root: { status: ProjectStatus.OnHold } }), today)).toBe('active');
  });

  it('holeIsMine: owner match, or ownerless + staffing manager', () => {
    const hole = toDemandRow(coverage({ coveredHours: 'PT0S', ownerResourceId: 'anna' }), []);
    expect(holeIsMine(hole, { resourceId: 'anna', isStaffingManager: false })).toBe(true);
    expect(holeIsMine(hole, { resourceId: 'luca', isStaffingManager: true })).toBe(false);
    const orphan = toDemandRow(coverage({ coveredHours: 'PT0S', ownerResourceId: null, ownerResourceName: null }), []);
    expect(holeIsMine(orphan, { resourceId: null, isStaffingManager: true })).toBe(true);
  });

  it('filters by verdict, person, role and lifecycle', () => {
    const p = makeProject({});
    const ctx = {
      verdictOf: () => 'sustainable' as const,
      me: { resourceId: null, isStaffingManager: false },
      todayISO: today,
      domain: { minISO: '2026-05-01', maxISO: '2026-10-01' },
    };
    const f = defaultFilters();
    expect(filterProjects([p], f, ctx)).toHaveLength(1);
    expect(filterProjects([p], { ...f, sustain: new Set(['uncovered']) }, ctx)).toHaveLength(0);
    expect(filterProjects([p], { ...f, people: new Set(['luca']) }, ctx)).toHaveLength(1);
    expect(filterProjects([p], { ...f, people: new Set(['giulia']) }, ctx)).toHaveLength(0);
    expect(filterProjects([p], { ...f, roles: new Set(['Dev senior']) }, ctx)).toHaveLength(1);
    expect(filterProjects([p], { ...f, roles: new Set(['Grafico']) }, ctx)).toHaveLength(0);
  });

  it('counts active facets for the badge', () => {
    const f = defaultFilters();
    expect(activeFilterCount(f)).toBe(1); // default hides closed = one restriction
    expect(activeFilterCount({ ...f, mineOwner: true, hideEmpty: true })).toBe(3);
  });

  it('sorts by sustainability severity first', () => {
    const a = makeProject({ root: { id: 'pa', name: 'A' } });
    const b = makeProject({ root: { id: 'pb', name: 'B' } });
    const verdictOf = (p: BoardProject) => (p.id === 'pb' ? 'uncovered' : 'sustainable');
    const sorted = sortProjects([a, b], 'sustain', verdictOf);
    expect(sorted[0]!.id).toBe('pb');
  });
});

describe('contextual actions availability', () => {
  it('mirrors the domain state machine per status', () => {
    expect(availableActionKinds(ProjectStatus.Draft)).toEqual(['start', 'setCommitment', 'cancel']);
    expect(availableActionKinds(ProjectStatus.Active)).toEqual(['complete', 'suspend', 'setCommitment', 'cancel']);
    expect(availableActionKinds(ProjectStatus.OnHold)).toEqual(['resume', 'setCommitment', 'cancel']);
    expect(availableActionKinds(ProjectStatus.Closed)).toEqual([]);
    expect(availableActionKinds(ProjectStatus.Cancelled)).toEqual([]);
  });

  it('statusChipKey marks every non-Active state and only those', () => {
    expect(statusChipKey(ProjectStatus.Active)).toBeNull();
    expect(statusChipKey(ProjectStatus.Draft)).toBe('draft');
    expect(statusChipKey(ProjectStatus.OnHold)).toBe('onHold');
    expect(statusChipKey(ProjectStatus.Closed)).toBe('closed');
    expect(statusChipKey(ProjectStatus.Cancelled)).toBe('cancelled');
  });
});

describe('portfolio & profile helpers', () => {
  it('computes the health counts over all projects', () => {
    const covered = makeProject({ root: { id: 'pa', name: 'A' } });
    const uncovered = makeProject({
      root: { id: 'pb', name: 'B' },
      demands: [coverage({ demandId: 'd2', coveredHours: 'PT0S', gapHours: 'PT340H' })],
      allocations: [],
    });
    const health = portfolioHealth([covered, uncovered], (p) => (p.id === 'pb' ? 'uncovered' : 'sustainable'), () => 0, 110);
    expect(health).toMatchObject({ total: 2, sustainable: 1, uncovered: 1, totalHoles: 1, overloadedPeople: 0 });
  });

  it('peakOf is the max segment percent', () => {
    expect(peakOf([{ percent: 50 }, { percent: 120 }, { percent: 90 }])).toBe(120);
    expect(peakOf([])).toBe(0);
    expect(peakOf(undefined)).toBe(0);
  });

  it('tentativeNotesOf lists only tentative blocks', () => {
    const p = makeProject({
      allocations: [alloc(), alloc({ id: 'a2', status: AllocationStatus.Tentative, allocationPercent: 30 })],
    });
    expect(tentativeNotesOf([p], 'luca')).toEqual([{ project: 'Portale ACME', percent: 30 }]);
  });

  it('allRoles collects distinct demand roles', () => {
    const p = makeProject({
      demands: [coverage(), coverage({ demandId: 'd2', roleName: 'Grafico', coveredHours: 'PT0S' })],
    });
    expect(allRoles([p])).toEqual(['Dev senior', 'Grafico']);
  });

  it('projectsExtent spans the dated projects', () => {
    const a = makeProject({ root: { id: 'pa', plannedStart: '2026-02-01', plannedEnd: '2026-04-01' } });
    const b = makeProject({ root: { id: 'pb', plannedStart: '2026-03-01', plannedEnd: '2026-08-01' } });
    expect(projectsExtent([a, b], { minISO: 'x', maxISO: 'y' })).toEqual({
      minISO: '2026-02-01',
      maxISO: '2026-08-01',
    });
  });
});
