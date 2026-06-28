import { describe, it, expect, vi } from 'vitest';
import { screen } from '@testing-library/react';
import { renderWithProviders } from '@/test/render';
import type { CompanyClosureReadDto } from '@/api/generated/schemas';
import { ClosureInlineForm } from './ClosureInlineForm';

function base(over: Partial<Parameters<typeof ClosureInlineForm>[0]> = {}) {
  const props = {
    yearHint: 2026,
    saving: false,
    onSubmit: vi.fn(),
    onCancel: vi.fn(),
    ...over,
  };
  return { props, ...renderWithProviders(<ClosureInlineForm {...props} />) };
}

describe('<ClosureInlineForm>', () => {
  it('submits a single-day closure with the trimmed reason', async () => {
    const onSubmit = vi.fn();
    const { user } = base({ onSubmit });
    await user.type(screen.getByPlaceholderText(/Motivazione/), '  Ferie  ');
    await user.click(screen.getByRole('button', { name: 'Aggiungi' }));
    expect(onSubmit).toHaveBeenCalledTimes(1);
    expect(onSubmit.mock.calls[0]![0]).toMatchObject({ reason: 'Ferie', kind: 'single' });
  });

  it('blocks submit and shows the required-reason error', async () => {
    const { props, user } = base();
    await user.click(screen.getByRole('button', { name: 'Aggiungi' }));
    expect(await screen.findByText('La motivazione è obbligatoria')).toBeInTheDocument();
    expect(props.onSubmit).not.toHaveBeenCalled();
  });

  it('reveals the end-date picker when switching to a range', async () => {
    const { user } = base();
    // The "→" separator + second picker only appear in range mode.
    expect(screen.queryByText('→')).not.toBeInTheDocument();
    await user.click(screen.getByText('Intervallo'));
    expect(screen.getByText('→')).toBeInTheDocument();
  });

  it('shows the edit title and delete affordance for an existing closure', async () => {
    const initial: CompanyClosureReadDto = {
      id: 'cl1',
      dateFrom: '2026-12-24',
      dateTo: '2026-12-26',
      reason: 'Natale',
    };
    const onDelete = vi.fn();
    const { user } = base({ initial, onDelete });
    expect(screen.getByText('Modifica chiusura')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /Elimina/ }));
    expect(onDelete).toHaveBeenCalled();
  });
});
