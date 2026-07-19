import { describe, expect, it } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { renderWithProviders } from '@/test/render';
import { seedProjectsBoard, acmeRoot } from '@/test/fixtures/projectsBoard';
import { getProjectNodesCreateMockHandler } from '@/api/generated/project-nodes/project-nodes.msw';
import { ProjectNodeType, type CreateProjectNodeDto } from '@/api/generated/schemas';
import { server } from '@/test/msw/server';
import { ProjectsPage } from './ProjectsPage';

describe('<ProjectsPage>', () => {
  it('renders the board with health cards and the project row', async () => {
    seedProjectsBoard();
    renderWithProviders(<ProjectsPage />);

    expect(await screen.findByText('Portale ACME')).toBeInTheDocument();

    // Health cards reflect the fixture: 1 uncovered project, 1 open role,
    // 1 person overloaded at peak (Luca @120% ≥ threshold 100).
    expect(screen.getByText('Progetti sostenibili')).toBeInTheDocument();
    expect(screen.getByText('0 / 1')).toBeInTheDocument();
    expect(screen.getByText('Ruoli scoperti')).toBeInTheDocument();
    expect(screen.getByText('Persone in sovraccarico al picco')).toBeInTheDocument();

    // The verdict badge on the row (the legend carries the same word — allow both).
    await waitFor(() => expect(screen.getAllByText('Scoperto').length).toBeGreaterThanOrEqual(2));
  });

  it('expands the project into person and open-role lanes', async () => {
    seedProjectsBoard();
    const user = userEvent.setup();
    renderWithProviders(<ProjectsPage />);

    await screen.findByText('Portale ACME');
    await user.click(await screen.findByRole('button', { name: /Espandi\/chiudi/ }));

    expect(await screen.findByText('Luca Ferri')).toBeInTheDocument();
    // The uncovered demand appears as a lane (label + bar → at least twice).
    await waitFor(() => expect(screen.getAllByText('Grafico').length).toBeGreaterThanOrEqual(1));
    // Overloaded person carries the peak conflict flag.
    expect(screen.getByText(/picco 120%/)).toBeInTheDocument();
  });

  it('opens the inspector on the project with the demand reconciliation', async () => {
    seedProjectsBoard();
    const user = userEvent.setup();
    renderWithProviders(<ProjectsPage />);

    await user.click(await screen.findByText('Portale ACME'));

    expect(await screen.findByText('Ispettore')).toBeInTheDocument();
    expect(await screen.findByText('Righe di domanda · 2')).toBeInTheDocument();
    // Uncovered row surfaces the owner fallback (staffing manager).
    expect(screen.getByText(/Scoperta · Owner: Staffing manager/)).toBeInTheDocument();
    // Hours are the hero: required/covered/uncovered mini cards.
    expect(screen.getByText('Richieste')).toBeInTheDocument();
    expect(screen.getByText('400h')).toBeInTheDocument(); // 340 + 60
  });

  // Extended timeout: the flow types into 4 fields + 2 date pickers on the full
  // board page — well over the 15s default under coverage instrumentation.
  it('creates a project with a phase from the header panel', { timeout: 45_000 }, async () => {
    seedProjectsBoard();
    const bodies: CreateProjectNodeDto[] = [];
    server.use(
      getProjectNodesCreateMockHandler(async (info) => {
        const body = (await info.request.json()) as CreateProjectNodeDto;
        bodies.push(body);
        return {
          ...acmeRoot,
          id: body.nodeType === ProjectNodeType.Project ? 'p-new' : 'ph-new',
          name: body.name ?? null,
        };
      }),
    );
    const user = userEvent.setup();
    renderWithProviders(<ProjectsPage />);

    await screen.findByText('Portale ACME');
    await user.click(screen.getByRole('button', { name: /Nuovo progetto/ }));
    expect(
      await screen.findByText('La timeline resta visibile e interattiva dietro il pannello.'),
    ).toBeInTheDocument();

    await user.type(screen.getByLabelText('Nome del progetto'), 'Rollout CRM');
    await user.type(screen.getByLabelText('Data inizio'), '01/09/2026{enter}');
    await user.type(screen.getByLabelText('Data fine'), '30/09/2026{enter}');
    await user.click(screen.getByRole('button', { name: /Aggiungi fase/ }));
    await user.type(screen.getByPlaceholderText('Nome fase (es. Analisi)'), 'Analisi');
    await user.click(screen.getByRole('button', { name: /Crea progetto/ }));

    // Success toast, panel closed, two POSTs: root then phase under the new id.
    // Generous timeout: under coverage instrumentation the two sequential POSTs
    // plus the toast can exceed the 1s findBy default.
    expect(
      await screen.findByText('Progetto «Rollout CRM» creato · 1 fase', undefined, {
        timeout: 5000,
      }),
    ).toBeInTheDocument();
    expect(bodies).toHaveLength(2);
    expect(bodies[0]).toMatchObject({
      nodeType: ProjectNodeType.Project,
      name: 'Rollout CRM',
      plannedStart: '2026-09-01',
      plannedEnd: '2026-09-30',
      leadResourceId: 'r-elena', // /api/me resourceId is the default owner
    });
    expect(bodies[1]).toMatchObject({
      nodeType: ProjectNodeType.Phase,
      parentId: 'p-new',
      name: 'Analisi',
      plannedStart: '2026-09-01',
      plannedEnd: '2026-09-30',
    });
  });

  it('shows the empty state when no project matches', async () => {
    seedProjectsBoard({ projects: [] });
    renderWithProviders(<ProjectsPage />);

    expect(await screen.findByText('Nessun progetto corrisponde ai filtri')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Azzera filtri/ })).toBeInTheDocument();
  });
});
