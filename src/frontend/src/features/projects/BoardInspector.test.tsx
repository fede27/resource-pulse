import { describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { renderWithProviders } from '@/test/render';
import {
  AllocationStatus,
  DemandProvenance,
  type DemandCoverageDto,
} from '@/api/generated/schemas';
import type { LoadBand } from '@/lib/loadBands';
import {
  acmeAllocations,
  acmeDemandCoverage,
  acmeRoot,
  acmeSubtree,
  lucaProfile,
} from '@/test/fixtures/projectsBoard';
import { buildBoardProject, type InspectTarget } from './boardModel';
import { BoardInspector } from './BoardInspector';

const bands: LoadBand[] = [
  { label: 'Sano', lowerBound: 0 },
  { label: 'Sovraccarico', lowerBound: 100 },
];

const bestEffortDemand: DemandCoverageDto = {
  demandId: 'd-rnd',
  projectNodeId: 'p-acme',
  roleId: 'role-frontend',
  roleName: 'Frontend',
  provenance: DemandProvenance.Inferred,
  requiredHours: null,
  coveredHours: 'PT48H',
  gapHours: null,
  ownerResourceId: null,
  ownerResourceName: null,
};

const project = buildBoardProject(
  acmeRoot,
  acmeSubtree,
  [...acmeDemandCoverage, bestEffortDemand],
  [
    ...acmeAllocations,
    {
      // Giulia covers the best-effort Frontend demand off-role and tentatively.
      id: 'a-giulia',
      demandId: 'd-rnd',
      resourceId: 'r-giulia',
      resourceName: 'Giulia Russo',
      resourceRoleId: 'role-grafico',
      resourceRoleName: 'Grafico',
      demandRoleId: 'role-frontend',
      demandRoleName: 'Frontend',
      projectNodeId: 'p-acme',
      periodStart: '2026-06-01',
      periodEnd: '2026-08-01',
      allocationPercent: 15,
      status: AllocationStatus.Tentative,
    },
  ],
);

function renderInspector(target: InspectTarget) {
  const props = {
    target,
    onClose: vi.fn(),
    onAction: vi.fn(),
    onLaneAction: vi.fn(),
    onInspect: vi.fn(),
    projects: [project],
    bands,
    overloadThreshold: 100,
    todayISO: '2026-07-05',
    profileByPerson: () => lucaProfile,
    peakByPerson: () => 120,
    // Deterministic derived hours (P3): 8h/day × 5 days/week ≈ fixed stub.
    blockHoursOf: () => 300,
  };
  renderWithProviders(<BoardInspector {...props} />);
  return props;
}

describe('<BoardInspector>', () => {
  it('project target: coverage face reconciles demand in hours', async () => {
    renderInspector({ kind: 'project', project });

    expect(await screen.findByText('Righe di domanda · 3')).toBeInTheDocument();
    // Targeted demand numbers.
    expect(screen.getByText('340h')).toBeInTheDocument();
    // Uncovered row falls back to the staffing-manager owner.
    expect(screen.getByText(/Scoperta · Owner: Staffing manager/)).toBeInTheDocument();
    // Best-effort row: consumption without a target, never a fake gap.
    expect(screen.getByText(/48h consumate · nessun target/)).toBeInTheDocument();
    expect(screen.getByText('inferita')).toBeInTheDocument();
    // Mismatch is surfaced, not rewritten.
    expect(screen.getAllByText('storto').length).toBeGreaterThanOrEqual(1);
  });

  it('project target: utilization face lists people at their hard peak', async () => {
    renderInspector({ kind: 'project', project });

    await userEvent.click(await screen.findByRole('tab', { name: 'Utilizzo · %' }));

    expect(await screen.findByText('Persone · 2')).toBeInTheDocument();
    expect(screen.getAllByText('120%').length).toBeGreaterThanOrEqual(1);
  });

  it('person target: opens on the utilization face with profile, composition and state', async () => {
    const block = project.demands[0]!.coverage[0]!;
    renderInspector({ kind: 'person', project, resourceId: block.resourceId, block });

    expect(await screen.findByText('Su questo progetto')).toBeInTheDocument();
    expect(screen.getByText('Composizione del picco')).toBeInTheDocument();
    expect(screen.getByText('Migrazione BETA')).toBeInTheDocument();
    expect(screen.getByText(/120% · picco/)).toBeInTheDocument();
    expect(screen.getByText('Sovraccarico al picco')).toBeInTheDocument();
    // Giulia's tentative block is NOT Luca's — no tentative note for Luca.
    expect(screen.queryByText(/non conteggiato/)).not.toBeInTheDocument();
  });

  it('hole target: the coverage face is that one role, not the whole project', async () => {
    const hole = project.holes[0]!;
    renderInspector({ kind: 'hole', project, demand: hole });

    expect(await screen.findByRole('heading', { name: hole.roleName })).toBeInTheDocument();
    // What is asked and what is missing, for this demand alone.
    // Nothing covers it: required and uncovered read the same figure.
    expect(screen.getAllByText(`${Math.round(hole.requiredH!)}h`)).toHaveLength(2);
    expect(screen.getByText('Cosa fare')).toBeInTheDocument();
    // The project's reconciliation is not on this face.
    expect(screen.queryByText(/Righe di domanda/)).not.toBeInTheDocument();
    expect(screen.queryByText(/consumate · nessun target/)).not.toBeInTheDocument();
  });

  it('hole target: covering starts from here, and the project is one link away', async () => {
    const hole = project.holes[0]!;
    const props = renderInspector({ kind: 'hole', project, demand: hole });
    const user = userEvent.setup();

    await user.click(await screen.findByRole('button', { name: 'Copri…' }));
    expect(props.onLaneAction).toHaveBeenCalledWith({ kind: 'cover', demand: hole, project });

    await user.click(screen.getByText(`Tutte le domande di ${project.name}`));
    expect(props.onInspect).toHaveBeenCalledWith({ kind: 'project', project });
  });

  it('hole target: utilization face explains there is no profile', async () => {
    renderInspector({ kind: 'hole', project, demand: project.holes[0]! });

    await userEvent.click(await screen.findByRole('tab', { name: 'Utilizzo · %' }));

    expect(await screen.findByText(/nessun utilizzo da mostrare/)).toBeInTheDocument();
  });
});
