import { describe, it, expect, vi } from 'vitest';
import { screen } from '@testing-library/react';
import { renderWithProviders } from '@/test/render';
import {
  CommitmentLevel,
  ProjectStatus,
  ProjectType,
} from '@/api/generated/schemas';
import { LaneActionsMenu } from './LaneActionsMenu';
import type { BoardProject, CoverageBlock, DemandRow } from './boardModel';

function makeBlock(hard: boolean): CoverageBlock {
  return {
    id: 'a1',
    demandId: 'd1',
    resourceId: 'r1',
    resourceName: 'Luca Ferri',
    resourceRoleName: 'Dev',
    demandRoleName: 'Dev senior',
    from: '2026-01-01',
    to: '2026-06-01',
    percent: 60,
    hard,
    mismatch: false,
    notes: null,
  };
}

function makeDemand(): DemandRow {
  return {
    demandId: 'd2',
    roleId: 'role-grafico',
    roleName: 'Grafico',
    inferred: false,
    requiredH: 60,
    coveredH: 0,
    gapH: 60,
    usefulH: 0,
    overH: 0,
    ownerResourceId: null,
    ownerName: null,
    coverage: [],
    uncovered: true,
    mismatch: false,
    status: 'uncovered',
  };
}

function makeProject(over: Partial<BoardProject> = {}): BoardProject {
  return {
    id: 'p1',
    name: 'Portale ACME',
    client: null,
    ownerId: null,
    ownerName: null,
    critical: false,
    proposed: false,
    status: ProjectStatus.Active,
    type: ProjectType.Customer,
    commitmentLevel: CommitmentLevel.Committed,
    from: '2026-01-01',
    to: '2026-06-01',
    phases: [],
    demands: [],
    holes: [],
    people: [],
    totals: { requiredH: 0, usefulH: 0, gapH: 0, overH: 0 },
    ...over,
  };
}

describe('<LaneActionsMenu>', () => {
  it('person lane, hard block: shows demote (not promote) plus reassign/retarget/remove', async () => {
    const onAction = vi.fn();
    const { user } = renderWithProviders(
      <LaneActionsMenu target={{ kind: 'person', block: makeBlock(true), project: makeProject() }} onAction={onAction} />,
    );
    await user.click(screen.getByLabelText('Azioni copertura'));

    expect(await screen.findByText('Riporta a Tentative')).toBeInTheDocument();
    expect(screen.queryByText('Promuovi a Hard')).not.toBeInTheDocument();
    expect(screen.getByText('Riassegna…')).toBeInTheDocument();
    expect(screen.getByText('Sposta su un\'altra domanda…')).toBeInTheDocument();
    expect(screen.getByText('Rimuovi copertura')).toBeInTheDocument();
  });

  it('person lane, tentative block: shows promote', async () => {
    const { user } = renderWithProviders(
      <LaneActionsMenu target={{ kind: 'person', block: makeBlock(false), project: makeProject() }} onAction={vi.fn()} />,
    );
    await user.click(screen.getByLabelText('Azioni copertura'));
    expect(await screen.findByText('Promuovi a Hard')).toBeInTheDocument();
    expect(screen.queryByText('Riporta a Tentative')).not.toBeInTheDocument();
  });

  it('person lane: clicking an item dispatches the matching action', async () => {
    const onAction = vi.fn();
    const block = makeBlock(true);
    const project = makeProject();
    const { user } = renderWithProviders(
      <LaneActionsMenu target={{ kind: 'person', block, project }} onAction={onAction} />,
    );
    await user.click(screen.getByLabelText('Azioni copertura'));
    await user.click(await screen.findByText('Riassegna…'));
    expect(onAction).toHaveBeenCalledWith({ kind: 'reassign', block, project });
  });

  it('hole lane: shows cover/editDemand/deleteDemand and dispatches deleteDemand', async () => {
    const onAction = vi.fn();
    const demand = makeDemand();
    const project = makeProject();
    const { user } = renderWithProviders(
      <LaneActionsMenu target={{ kind: 'hole', demand, project }} onAction={onAction} />,
    );
    await user.click(screen.getByLabelText('Azioni domanda'));
    expect(await screen.findByText('Copri…')).toBeInTheDocument();
    expect(screen.getByText('Modifica domanda…')).toBeInTheDocument();
    await user.click(screen.getByText('Elimina domanda'));
    expect(onAction).toHaveBeenCalledWith({ kind: 'deleteDemand', demand, project });
  });

  it('hole lane: cover is disabled when the project has no planned dates', async () => {
    const { user } = renderWithProviders(
      <LaneActionsMenu
        target={{ kind: 'hole', demand: makeDemand(), project: makeProject({ from: null, to: null }) }}
        onAction={vi.fn()}
      />,
    );
    await user.click(screen.getByLabelText('Azioni domanda'));
    const cover = await screen.findByText('Copri…');
    expect(cover.closest('.ant-dropdown-menu-item-disabled')).not.toBeNull();
  });
});
