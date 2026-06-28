import { describe, it, expect, vi } from 'vitest';
import { screen } from '@testing-library/react';
import { renderWithProviders } from '@/test/render';
import { PersonInlineCreate } from './PersonInlineCreate';

function base(over: Partial<Parameters<typeof PersonInlineCreate>[0]> = {}) {
  const props = {
    saving: false,
    onSubmit: vi.fn(),
    onCancel: vi.fn(),
    roleOptions: [{ id: 'role-1', label: 'Engineer' }],
    ...over,
  };
  return { props, ...renderWithProviders(<PersonInlineCreate {...props} />) };
}

describe('<PersonInlineCreate>', () => {
  it('requires a name before submitting', async () => {
    const { props, user } = base();
    await user.click(screen.getByRole('button', { name: 'Aggiungi' }));
    expect(await screen.findByText('Il nome è obbligatorio')).toBeInTheDocument();
    expect(props.onSubmit).not.toHaveBeenCalled();
  });

  it('submits the trimmed name', async () => {
    const { props, user } = base();
    await user.type(screen.getByPlaceholderText('Nome e cognome'), '  Mario Rossi  ');
    await user.click(screen.getByRole('button', { name: 'Aggiungi' }));
    expect(props.onSubmit).toHaveBeenCalledWith(
      expect.objectContaining({ name: 'Mario Rossi' }),
    );
  });

  it('rejects an invalid email', async () => {
    const { props, user } = base();
    await user.type(screen.getByPlaceholderText('Nome e cognome'), 'Mario');
    await user.type(screen.getByPlaceholderText('email@azienda.it'), 'not-an-email');
    await user.click(screen.getByRole('button', { name: 'Aggiungi' }));
    expect(await screen.findByText('Inserisci un indirizzo email valido.')).toBeInTheDocument();
    expect(props.onSubmit).not.toHaveBeenCalled();
  });

  it('cancels from the footer button', async () => {
    const { props, user } = base();
    // Both the close icon and the footer button are labelled "Annulla".
    await user.click(screen.getAllByRole('button', { name: 'Annulla' })[0]!);
    expect(props.onCancel).toHaveBeenCalled();
  });
});
