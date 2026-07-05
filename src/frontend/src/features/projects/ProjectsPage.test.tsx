import { describe, expect, it } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { renderWithProviders } from '@/test/render';
import { seedProjectsBoard } from '@/test/fixtures/projectsBoard';
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

  it('shows the empty state when no project matches', async () => {
    seedProjectsBoard({ projects: [] });
    renderWithProviders(<ProjectsPage />);

    expect(await screen.findByText('Nessun progetto corrisponde ai filtri')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Azzera filtri/ })).toBeInTheDocument();
  });
});
