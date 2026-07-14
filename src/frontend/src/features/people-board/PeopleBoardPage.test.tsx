import { describe, expect, it } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { renderWithProviders } from '@/test/render';
import { seedPeopleBoard } from '@/test/fixtures/peopleBoard';
import { PeopleBoardPage } from './PeopleBoardPage';

describe('<PeopleBoardPage>', () => {
  it('renders people grouped by role with the peak band pill', async () => {
    seedPeopleBoard();
    renderWithProviders(<PeopleBoardPage />);

    expect(await screen.findByText('Luca Ferri')).toBeInTheDocument();
    expect(await screen.findByText('Elena Neri')).toBeInTheDocument();

    // Group headers: Luca under his role, Elena in the role-less tail group.
    expect(screen.getByText('Dev senior')).toBeInTheDocument();
    expect(screen.getByText('Senza ruolo')).toBeInTheDocument();

    // KPI header cards are present (bands from config: Healthy@0 / Over@100).
    expect(screen.getByText('In sovraccarico al picco')).toBeInTheDocument();
    expect(screen.getByText('Sotto-utilizzati')).toBeInTheDocument();

    // Luca's peak pill: 60% hard → Healthy band.
    await waitFor(() => expect(screen.getByText(/Healthy 60%/)).toBeInTheDocument());
  });

  it('expands a person into project lanes and the free-capacity lane', async () => {
    seedPeopleBoard();
    const user = userEvent.setup();
    renderWithProviders(<PeopleBoardPage />);

    await screen.findByText('Luca Ferri');
    const toggles = await screen.findAllByRole('button', { name: /Espandi\/chiudi/ });
    await user.click(toggles[0]!);

    // Lane label = the root project name resolved from the node catalogue.
    expect(await screen.findByText('Portale ACME')).toBeInTheDocument();
    expect(screen.getByText('Capacità libera')).toBeInTheDocument();
  });

  it('filters people with the search box', async () => {
    seedPeopleBoard();
    const user = userEvent.setup();
    renderWithProviders(<PeopleBoardPage />);

    await screen.findByText('Luca Ferri');
    await user.type(screen.getByPlaceholderText('Cerca persona o ruolo…'), 'Elena');

    await waitFor(() => expect(screen.queryByText('Luca Ferri')).not.toBeInTheDocument());
    expect(screen.getByText('Elena Neri')).toBeInTheDocument();
    expect(screen.getByText(/di 2/)).toBeInTheDocument(); // 1 persona di 2
  });

  it('opens the inspector with the utilization composition on a cell click', async () => {
    seedPeopleBoard();
    const user = userEvent.setup();
    renderWithProviders(<PeopleBoardPage />);

    // Open via the person label (visible-range target).
    await user.click(await screen.findByText('Luca Ferri'));

    expect(await screen.findByText('Ispettore persona')).toBeInTheDocument();
    // Revised inspector: the period selector answers "quando?" explicitly and
    // defaults to the current week ("Ora"), broken down by day.
    expect(await screen.findByText('Ora')).toBeInTheDocument();
    expect(screen.getByText('Tutto')).toBeInTheDocument();
    expect(await screen.findByText('Distribuzione per giorno')).toBeInTheDocument();
    // Composition row: ACME share of the average (hard-only by default), the
    // tentative BETA block is listed as a not-counted note.
    expect(await screen.findByText('Portale ACME')).toBeInTheDocument();
    expect(screen.getByText(/30% Migrazione BETA.*non conteggiate/)).toBeInTheDocument();
  });

  it('shows the coverage face with hours per project', async () => {
    seedPeopleBoard();
    const user = userEvent.setup();
    renderWithProviders(<PeopleBoardPage />);

    await user.click(await screen.findByText('Luca Ferri'));
    await screen.findByText('Ispettore persona');
    await user.click(screen.getByText('Copertura · ore'));

    expect(await screen.findByText('Totale allocato')).toBeInTheDocument();
    // Hours face always includes tentative rows, annotated.
    expect(await screen.findByText('Migrazione BETA')).toBeInTheDocument();
  });

  it('counts tentative blocks in the band when toggled', async () => {
    seedPeopleBoard();
    const user = userEvent.setup();
    renderWithProviders(<PeopleBoardPage />);

    await screen.findByText('Luca Ferri');
    await screen.findByText(/Healthy 60%/); // hard-only peak
    await user.click(screen.getByRole('switch'));

    // 60 hard + 30 tentative = 90% peak; the legend flips to the with-tent note.
    expect(await screen.findByText(/Healthy 90%/)).toBeInTheDocument();
    expect(screen.getByText(/la cella conta hard \+ tentative/)).toBeInTheDocument();
  });

  it('switches the metric to hours in the cells', async () => {
    seedPeopleBoard();
    const user = userEvent.setup();
    renderWithProviders(<PeopleBoardPage />);

    await screen.findByText('Luca Ferri');
    await user.click(screen.getByText('Ore'));

    // Full-week bucket: 60% × 40h = 24h on Luca's row.
    await waitFor(() => expect(screen.getAllByText('24').length).toBeGreaterThanOrEqual(1));
  });

  it('opens the block inspector from an expanded lane', async () => {
    seedPeopleBoard();
    const user = userEvent.setup();
    renderWithProviders(<PeopleBoardPage />);

    await screen.findByText('Luca Ferri');
    const toggles = await screen.findAllByRole('button', { name: /Espandi\/chiudi/ });
    await user.click(toggles[0]!);

    await user.click(await screen.findByTitle(/Portale ACME · 60% · hard/));

    expect(await screen.findByText('Blocco')).toBeInTheDocument();
    expect(await screen.findByText('Hard (confermato)')).toBeInTheDocument();
  });

  it('filters by band and shows the empty state when nothing matches', async () => {
    seedPeopleBoard();
    const user = userEvent.setup();
    renderWithProviders(<PeopleBoardPage />);

    await screen.findByText('Luca Ferri');
    // Config bands: Healthy@0 / Over@100 — nobody peaks ≥100 hard-only.
    await user.click(screen.getByRole('button', { name: /Over/ }));

    expect(await screen.findByText('Nessuna persona nel filtro')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Azzera filtri' }));
    expect(await screen.findByText('Luca Ferri')).toBeInTheDocument();
  });
});
