import { describe, expect, it, vi } from 'vitest';
import { fireEvent, screen } from '@testing-library/react';
import { CommitmentLevel, ProjectStatus, ProjectType } from '@/api/generated/schemas';
import { buildGeo } from '@/components/board';
import { renderWithProviders } from '@/test/render';
import { ProjectRow, type ProjectRowProps } from './ProjectRow';
import type { BoardProject, CoverageBlock, DemandRow } from './boardModel';

const geo = buildGeo('2026-06-01', '2026-10-31', 'week', {
  todayISO: '2026-06-22',
  frozenEndISO: '2026-07-06',
  slushyEndISO: '2026-08-22',
});

const block = (over: Partial<CoverageBlock>): CoverageBlock => ({
  id: 'b1',
  demandId: 'd1',
  resourceId: 'luca',
  resourceName: 'Luca Ferri',
  resourceRoleName: 'Dev senior',
  demandRoleName: 'Dev senior',
  from: '2026-07-01',
  to: '2026-08-30',
  percent: 60,
  hard: true,
  mismatch: false,
  notes: null,
  nodeId: 'f2',
  startFollows: true,
  endFollows: true,
  ...over,
});

const demand = (coverage: CoverageBlock[]): DemandRow => ({
  demandId: 'd1',
  nodeId: 'f2',
  roleId: 'r1',
  roleName: 'Dev senior',
  inferred: false,
  requiredH: 240,
  coveredH: 206.4,
  gapH: 33.6,
  usefulH: 206.4,
  overH: 0,
  ownerResourceId: null,
  ownerName: null,
  coverage,
  uncovered: false,
  mismatch: false,
  status: 'partial',
});

const project = (blocks: CoverageBlock[], over: Partial<BoardProject> = {}): BoardProject => ({
  id: 'p1',
  name: 'Gara Sanità',
  client: null,
  ownerId: null,
  ownerName: null,
  critical: false,
  proposed: false,
  status: ProjectStatus.Active,
  type: ProjectType.Customer,
  commitmentLevel: CommitmentLevel.Committed,
  from: '2026-06-01',
  to: '2026-09-25',
  followCount: 0,
  phases: [{ id: 'f2', label: 'Fase 2', from: '2026-07-01', to: '2026-08-30', followCount: 2 }],
  demands: [demand(blocks)],
  holes: [],
  people: blocks.map((b) => b.resourceId),
  totals: { requiredH: 240, usefulH: 206.4, gapH: 33.6, overH: 0 },
  ...over,
});

const renderRow = (p: BoardProject, over: Partial<ProjectRowProps> = {}) =>
  renderWithProviders(
    <ProjectRow
      project={p}
      geo={geo}
      metric="pct"
      verdict={{ verdict: 'sustainable', reason: null }}
      expanded
      alt={false}
      onToggle={vi.fn()}
      onInspect={vi.fn()}
      onAction={null}
      onLaneAction={null}
      peakByPerson={() => 0}
      overloadThreshold={110}
      blockHoursOf={() => null}
      preview={null}
      onReplan={vi.fn()}
      availabilityOf={() => null}
      {...over}
    />,
  );

describe('<ProjectRow> — boundaries and the node move', () => {
  it('a following block is silent; a pinned edge carries the mark', () => {
    renderRow(project([block({ id: 'b1' }), block({ id: 'b2', resourceId: 'elena', endFollows: false })]));

    expect(screen.getAllByTitle('Fine fissata: non segue il nodo')).toHaveLength(1);
    expect(screen.queryByTitle('Inizio fissato: non segue il nodo')).not.toBeInTheDocument();
  });

  it('the phase carries the count of boundaries that follow it', () => {
    renderRow(project([block({})]));
    expect(screen.getByTitle('2 bordi di copertura seguono questo nodo')).toHaveTextContent('2');
  });

  it('every node has a grip on each edge — the root and its phases', () => {
    renderRow(project([block({})]));
    expect(screen.getByRole('separator', { name: 'Sposta fine di Fase 2' })).toBeInTheDocument();
    expect(screen.getByRole('separator', { name: 'Sposta inizio di Fase 2' })).toBeInTheDocument();
    expect(screen.getByRole('separator', { name: 'Sposta fine di Gara Sanità' })).toBeInTheDocument();
  });

  it('nudging a grip hands over the gesture: one edge moved, the other untouched', () => {
    const onReplan = vi.fn();
    renderRow(project([block({})]), { onReplan });

    fireEvent.keyDown(screen.getByRole('separator', { name: 'Sposta fine di Fase 2' }), { key: 'ArrowRight' });

    expect(onReplan).toHaveBeenCalledWith({
      projectId: 'p1',
      nodeId: 'f2',
      nodeName: 'Fase 2',
      edge: 'end',
      oldFrom: '2026-07-01',
      oldTo: '2026-08-30',
      newFrom: '2026-07-01',
      newTo: '2026-08-31',
    });
  });

  it('Shift nudges by a week, and a start edge moves the start', () => {
    const onReplan = vi.fn();
    renderRow(project([block({})]), { onReplan });

    fireEvent.keyDown(screen.getByRole('separator', { name: 'Sposta inizio di Fase 2' }), {
      key: 'ArrowLeft',
      shiftKey: true,
    });

    expect(onReplan.mock.calls[0]![0]).toMatchObject({ edge: 'start', newFrom: '2026-06-24', newTo: '2026-08-30' });
  });

  it('a project ending on the last day of the window keeps its grip inside the timeline', () => {
    renderRow(project([block({})], { to: '2026-10-31' }));
    const grip = screen.getByRole('separator', { name: 'Sposta fine di Gara Sanità' });
    expect(parseFloat(grip.style.left)).toBeLessThanOrEqual(geo.contentW - 10);
  });

  it('the project kebab adds a demand — the only way in for a project that has none', async () => {
    const onLaneAction = vi.fn();
    const empty = project([], { demands: [], people: [] });
    const { user } = renderRow(empty, { onAction: vi.fn(), onLaneAction });

    await user.click(screen.getByLabelText('Azioni progetto'));
    await user.click(await screen.findByText('Aggiungi domanda…'));

    expect(onLaneAction).toHaveBeenCalledWith({ kind: 'addDemand', project: empty });
  });

  it('a role asked for on one phase is drawn on that phase, not across the project', () => {
    const hole: DemandRow = { ...demand([]), demandId: 'd9', nodeId: 'f2', roleName: 'QA Engineer', uncovered: true };
    renderRow(project([], { demands: [hole], holes: [hole], people: [] }));

    const bar = screen.getByTitle('34h scoperte nel range');
    expect(parseFloat(bar.style.left)).toBe(geo.xPx('2026-07-01'));
    expect(parseFloat(bar.style.width)).toBe(geo.wPxInclusive('2026-07-01', '2026-08-30'));
    expect(screen.getByText(/Fase 2 ·/)).toBeInTheDocument();
  });

  it('a role asked for on the project itself still spans the project', () => {
    const hole: DemandRow = { ...demand([]), demandId: 'd9', nodeId: 'p1', roleName: 'PM', uncovered: true };
    renderRow(project([], { demands: [hole], holes: [hole], people: [] }));

    expect(parseFloat(screen.getByTitle('34h scoperte nel range').style.left)).toBe(geo.xPx('2026-06-01'));
  });

  it('a phase cannot be pushed past the end of its project', () => {
    const onReplan = vi.fn();
    const tight = project([block({})], { to: '2026-08-30' });
    renderRow(tight, { onReplan });

    fireEvent.keyDown(screen.getByRole('separator', { name: 'Sposta fine di Fase 2' }), { key: 'ArrowRight' });

    // Clamped back onto its own date: the gesture is a no-op the page drops.
    expect(onReplan.mock.calls[0]![0]).toMatchObject({ newTo: '2026-08-30' });
  });

  it('a project cannot be pulled in past one of its phases', () => {
    const onReplan = vi.fn();
    const tight = project([block({})], { to: '2026-08-30' });
    renderRow(tight, { onReplan });

    fireEvent.keyDown(screen.getByRole('separator', { name: 'Sposta fine di Gara Sanità' }), { key: 'ArrowLeft' });

    expect(onReplan.mock.calls[0]![0]).toMatchObject({ nodeId: 'p1', newTo: '2026-08-30' });
  });

  it('a viewer gets no grips', () => {
    renderRow(project([block({})]), { onReplan: null });
    expect(screen.queryByRole('separator')).not.toBeInTheDocument();
  });

  it('a closed project gets no grips: it refuses every plan mutation', () => {
    renderRow(project([block({})], { status: ProjectStatus.Closed }));
    expect(screen.queryByRole('separator')).not.toBeInTheDocument();
  });

  it('an edge awaiting confirmation shows the counter with what would follow', () => {
    renderRow(project([block({ id: 'b1' }), block({ id: 'b2', resourceId: 'elena', endFollows: false })]), {
      preview: { projectId: 'p1', nodeId: 'f2', edge: 'end', newFrom: '2026-07-01', newTo: '2026-09-09' },
    });
    // Two blocks sit on the phase; only the one whose END follows is dragged.
    expect(screen.getByText('9 set · +10 gg · 1 coperture')).toBeInTheDocument();
  });

  it('a block that crosses the declared availability is hatched where it does', () => {
    renderRow(project([block({})]), {
      availabilityOf: () => ({ from: null, until: '2026-07-31' }),
    });
    expect(screen.getByTitle('Oltre la disponibilità dichiarata')).toBeInTheDocument();
  });

  it('a block inside the availability is not marked', () => {
    renderRow(project([block({})]), {
      availabilityOf: () => ({ from: null, until: '2026-12-31' }),
    });
    expect(screen.queryByTitle('Oltre la disponibilità dichiarata')).not.toBeInTheDocument();
  });
});
