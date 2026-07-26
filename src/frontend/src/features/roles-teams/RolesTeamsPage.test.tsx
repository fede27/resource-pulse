import { describe, expect, it } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { renderWithProviders } from '@/test/render';
import { seedRolesTeams } from '@/test/fixtures/rolesTeams';
import { RolesTeamsPage } from './RolesTeamsPage';

// Roles sort alphabetically → the default-selected role is "Project Manager"
// (empty). Tests that need people select "Sviluppatore" first.
const openView = async (user: ReturnType<typeof userEvent.setup>, label: string) => {
  // "Sviluppatore" is catalog-only (not the default selection) → unique.
  await screen.findByText('Sviluppatore');
  await user.click(screen.getByText(label));
};

describe('<RolesTeamsPage> — Anagrafica', () => {
  it('lists a role’s people and flags empty roles', async () => {
    seedRolesTeams();
    const user = userEvent.setup();
    renderWithProviders(<RolesTeamsPage />);

    // Wait for the catalogue to load, then assert the empty-role signals: the
    // default-selected PM role is empty → status chip + no-capacity hint.
    await screen.findByText('Sviluppatore');
    expect(await screen.findByText('ruoli senza persone')).toBeInTheDocument();
    expect(screen.getByText(/non contribuisce alla capacità/)).toBeInTheDocument();

    // Selecting Sviluppatore reveals its two people.
    await user.click(screen.getByText('Sviluppatore'));
    expect(await screen.findByText('Ada Lovelace')).toBeInTheDocument();
    expect(screen.getByText('Bob Bit')).toBeInTheDocument();
  });

  it('pivots from roles to teams', async () => {
    seedRolesTeams();
    const user = userEvent.setup();
    renderWithProviders(<RolesTeamsPage />);

    await openView(user, 'Per team');
    // Default team (Team Alpha) has Ada + Cy.
    expect((await screen.findAllByText('Team Alpha')).length).toBeGreaterThan(0);
    expect(await screen.findByText('Ada Lovelace')).toBeInTheDocument();
    expect(screen.getByText('Cy Byte')).toBeInTheDocument();
  });

  it('opens the person inspector on a row click', async () => {
    seedRolesTeams();
    const user = userEvent.setup();
    renderWithProviders(<RolesTeamsPage />);

    await user.click(await screen.findByText('Sviluppatore'));
    await user.click(await screen.findByText('Ada Lovelace'));

    expect(await screen.findByText('Ruolo · asse di valutazione')).toBeInTheDocument();
    expect(await screen.findByText('Remote')).toBeInTheDocument();
  });
});

describe('<RolesTeamsPage> — Disponibilità', () => {
  it('renders the availability grid grouped by team with a legend', async () => {
    seedRolesTeams();
    const user = userEvent.setup();
    renderWithProviders(<RolesTeamsPage />);

    await openView(user, 'Disponibilità');

    expect(await screen.findByText('Ada Lovelace')).toBeInTheDocument();
    expect(await screen.findByText('Cy Byte')).toBeInTheDocument();
    expect((await screen.findAllByText('Team Alpha')).length).toBeGreaterThan(0);
    expect(screen.getByText('Lavorativo')).toBeInTheDocument();
    expect(screen.getByText('Straordinario')).toBeInTheDocument();
  });

  it('opens the day/period inspector on a cell click', async () => {
    seedRolesTeams();
    const user = userEvent.setup();
    renderWithProviders(<RolesTeamsPage />);

    await openView(user, 'Disponibilità');
    await screen.findByText('Cy Byte');

    const cells = await screen.findAllByTitle(/^\d+(\.\d+)?h$/);
    await user.click(cells[0]!);

    expect(await screen.findByText('Calendario assegnato')).toBeInTheDocument();
    expect(await screen.findByText('Disponibilità effettiva')).toBeInTheDocument();
  });

  it('opens the calendar picker from the name label', async () => {
    seedRolesTeams();
    const user = userEvent.setup();
    renderWithProviders(<RolesTeamsPage />);

    await openView(user, 'Disponibilità');
    await screen.findByText('Cy Byte');

    const calLinks = await screen.findAllByTitle('Cambia calendario');
    await user.click(calLinks[0]!);

    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByText('Assegna')).toBeInTheDocument();
  });

  it('switches grain through the shared time filter', async () => {
    seedRolesTeams();
    const user = userEvent.setup();
    renderWithProviders(<RolesTeamsPage />);

    await openView(user, 'Disponibilità');
    await screen.findByText('Cy Byte');

    // Same three grains as the boards — this page used to offer only two.
    await user.click(screen.getByText('Giorno'));
    await waitFor(() => expect(screen.getByText('Ada Lovelace')).toBeInTheDocument());

    await user.click(screen.getByText('Mese'));
    await waitFor(() => expect(screen.getByText('Ada Lovelace')).toBeInTheDocument());
  });

  it('offers the shared window controls (year · oggi · adatta)', async () => {
    seedRolesTeams();
    const user = userEvent.setup();
    renderWithProviders(<RolesTeamsPage />);

    await openView(user, 'Disponibilità');
    await screen.findByText('Cy Byte');

    expect(screen.getByRole('button', { name: /Anno precedente/ })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Oggi/ })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Adatta/ })).toBeInTheDocument();
  });
});
