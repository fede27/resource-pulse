import { describe, expect, it } from 'vitest';
import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { renderWithProviders } from '@/test/render';
import { seedRolesTeams } from '@/test/fixtures/rolesTeams';
import { RolesTeamsPage } from './RolesTeamsPage';

const selectDevRole = async (user: ReturnType<typeof userEvent.setup>) => {
  await user.click(await screen.findByText('Sviluppatore'));
  await screen.findByText('Ada Lovelace');
};

describe('<RolesTeamsPage> — Anagrafica interactions', () => {
  it('reveals the inline "new role" input', async () => {
    seedRolesTeams();
    const user = userEvent.setup();
    renderWithProviders(<RolesTeamsPage />);

    await screen.findByText('Sviluppatore');
    await user.click(screen.getByRole('button', { name: /Nuovo/ }));
    expect(screen.getByPlaceholderText('Nome ruolo')).toBeInTheDocument();
  });

  it('validates the inline person-create email', async () => {
    seedRolesTeams();
    const user = userEvent.setup();
    renderWithProviders(<RolesTeamsPage />);

    await selectDevRole(user);
    await user.click(screen.getByRole('button', { name: /Aggiungi persona/ }));
    await user.type(screen.getByPlaceholderText('Nome e cognome'), 'Test Person');
    await user.type(screen.getByPlaceholderText('Email'), 'not-an-email');
    await user.click(screen.getByRole('button', { name: 'Aggiungi' }));

    expect(
      await screen.findByText('Inserisci un indirizzo email valido.'),
    ).toBeInTheDocument();
  });

  it('blocks deleting a role that still has people', async () => {
    seedRolesTeams();
    const user = userEvent.setup();
    renderWithProviders(<RolesTeamsPage />);

    await selectDevRole(user);
    await user.click(screen.getByRole('button', { name: /more/i }));
    await user.click(await screen.findByText('Elimina'));

    // modal.info renders the title twice (visible + aria node).
    expect((await screen.findAllByText(/Impossibile eliminare/)).length).toBeGreaterThan(0);
  });
});

describe('<RolesTeamsPage> — adjustment editor', () => {
  const openEditor = async (user: ReturnType<typeof userEvent.setup>) => {
    await screen.findByText('Sviluppatore');
    await user.click(screen.getByText('Disponibilità'));
    await screen.findByText('Cy Byte');
    const cells = await screen.findAllByTitle(/^\d+(\.\d+)?h$/);
    await user.click(cells[0]!);
    const drawer = await screen.findByRole('dialog');
    // "Ferie" add button (plus-icon composed name) inside the drawer.
    await user.click(within(drawer).getByRole('button', { name: /Ferie/ }));
    await screen.findByText('Nuova eccezione');
    // The editor is the second dialog (over the still-open inspector drawer).
    const dialogs = screen.getAllByRole('dialog');
    return dialogs[dialogs.length - 1]!;
  };

  it('requires a reason and toggles the per-day hours field', async () => {
    seedRolesTeams();
    const user = userEvent.setup();
    renderWithProviders(<RolesTeamsPage />);

    const modal = await openEditor(user);
    const inModal = within(modal);

    // Saving without a reason surfaces the inline error.
    await user.click(inModal.getByRole('button', { name: 'Aggiungi' }));
    expect(await screen.findByText('Obbligatoria.')).toBeInTheDocument();

    // Un-checking "full day" reveals the absence-hours field.
    await user.click(inModal.getByRole('checkbox', { name: 'Giornata intera' }));
    expect(await screen.findByText('Ore di assenza al giorno')).toBeInTheDocument();

    // Switching to overtime relabels the hours field.
    await user.click(inModal.getByRole('button', { name: /Straordinario/ }));
    expect(await screen.findByText('Ore extra al giorno')).toBeInTheDocument();
  });
});
