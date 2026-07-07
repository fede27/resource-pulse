import { describe, expect, it, vi } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { getLoadGetOpenDemandsMockHandler } from '@/api/generated/load/load.msw';
import { getPlanCommandsExecuteMockHandler } from '@/api/generated/plan-commands/plan-commands.msw';
import { renderWithProviders } from '@/test/render';
import { server } from '@/test/msw/server';
import { CoverPopover } from './CoverPopover';
import type { PersonData } from './peopleBoardModel';

const capacity = new Map<string, number>();
for (let d = 1; d <= 28; d += 1) {
  capacity.set(`2026-06-${String(d).padStart(2, '0')}`, 8);
}

const luca: PersonData = {
  person: { id: 'r-luca', name: 'Luca Ferri', roleId: 'role-dev', roleName: 'Dev senior', teamName: 'Alpha' },
  blocks: [],
  capacityByDay: capacity,
  weeklyCapH: 40,
};

const pending = { from: '2026-06-01', toExcl: '2026-06-15', anchorX: 100 };

describe('<CoverPopover>', () => {
  it('lists the open demands for the person role with the residual hours', async () => {
    server.use(
      getLoadGetOpenDemandsMockHandler([
        {
          demandId: 'd-acme',
          projectNodeId: 'p-acme',
          rootProjectId: 'p-acme',
          rootProjectName: 'Portale ACME',
          roleId: 'role-dev',
          roleName: 'Dev senior',
          coveredHours: 'PT20H',
          gapHours: 'PT40H',
        },
        {
          demandId: 'd-beta',
          projectNodeId: 'p-beta',
          rootProjectId: 'p-beta',
          rootProjectName: 'Migrazione BETA',
          roleId: 'role-dev',
          roleName: 'Dev senior',
          coveredHours: 'PT0S',
          gapHours: null, // best-effort
        },
      ]),
    );

    renderWithProviders(
      <CoverPopover person={luca} pending={pending} rootProjects={[]} onClose={() => undefined} />,
    );

    expect(await screen.findByText('Domande scoperte · Dev senior')).toBeInTheDocument();
    expect(await screen.findByText('Portale ACME')).toBeInTheDocument();
    expect(screen.getByText('40h scoperte · Dev senior')).toBeInTheDocument();
    expect(screen.getByText('best-effort · Dev senior')).toBeInTheDocument();
  });

  it('covers a demand through the plan-command envelope with the kind discriminator', async () => {
    const bodies: unknown[] = [];
    server.use(
      getLoadGetOpenDemandsMockHandler([
        {
          demandId: 'd-acme',
          projectNodeId: 'p-acme',
          rootProjectId: 'p-acme',
          rootProjectName: 'Portale ACME',
          roleId: 'role-dev',
          roleName: 'Dev senior',
          coveredHours: 'PT0S',
          gapHours: 'PT56H', // over 112h window capacity → 50%
        },
      ]),
      http.post('*/api/plan/commands', async ({ request }) => {
        bodies.push(await request.json());
        return HttpResponse.json({ commandKind: 'create', dryRun: false, committed: true, changes: [], demandChanges: [] });
      }),
    );

    const onClose = vi.fn();
    const user = userEvent.setup();
    renderWithProviders(<CoverPopover person={luca} pending={pending} rootProjects={[]} onClose={onClose} />);

    await user.click(await screen.findByText('Portale ACME'));

    await waitFor(() => expect(onClose).toHaveBeenCalled());
    expect(bodies).toHaveLength(1);
    expect(bodies[0]).toMatchObject({
      kind: 'create', // the System.Text.Json discriminator must be on the wire
      demandId: 'd-acme',
      resourceId: 'r-luca',
      periodStart: '2026-06-01',
      periodEnd: '2026-06-14', // inclusive end = toExcl − 1 day
      percent: 50,
      status: 0, // Tentative
    });
  });

  it('surfaces the candidate list when coverInferred is ambiguous and commits nothing', async () => {
    server.use(
      getLoadGetOpenDemandsMockHandler([]),
      getPlanCommandsExecuteMockHandler({
        commandKind: 'coverInferred',
        dryRun: false,
        committed: false,
        changes: [],
        demandChanges: [
          { kind: 3, id: 'd-1', projectNodeId: 'p-acme', roleId: 'role-dev', requiredHours: 'PT40H' },
          { kind: 3, id: 'd-2', projectNodeId: 'p-acme', roleId: 'role-dev', requiredHours: null },
        ],
      }),
    );

    const user = userEvent.setup();
    renderWithProviders(
      <CoverPopover
        person={luca}
        pending={pending}
        rootProjects={[{ id: 'p-acme', name: 'Portale ACME' }]}
        onClose={() => undefined}
      />,
    );

    await user.click(await screen.findByRole('button', { name: /Portale ACME/ }));

    expect(
      await screen.findByText('Più domande scoperte su progetto e ruolo — scegli quale coprire'),
    ).toBeInTheDocument();
    expect(screen.getByText('Domanda con target 40h')).toBeInTheDocument();
    expect(screen.getByText('Domanda best-effort')).toBeInTheDocument();
  });
});
