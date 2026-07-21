import { describe, it, expect, vi } from 'vitest';
import { screen } from '@testing-library/react';
import { renderWithProviders } from '@/test/render';
import type { CompanyClosureReadDto } from '@/api/generated/schemas';
import { ClosureInspectPopover } from './ClosureInspectPopover';

const closure: CompanyClosureReadDto = {
  id: 'cl1',
  dateFrom: '2030-12-24',
  dateTo: '2030-12-26',
  reason: 'Chiusura natalizia',
};

function setup(over: Partial<Parameters<typeof ClosureInspectPopover>[0]> = {}) {
  const props = {
    closure,
    anchor: { x: 100, y: 100 },
    onClose: vi.fn(),
    onEdit: vi.fn(),
    onDelete: vi.fn(),
    ...over,
  };
  return { props, ...renderWithProviders(<ClosureInspectPopover {...props} />) };
}

describe('<ClosureInspectPopover>', () => {
  it('shows the closure facts', () => {
    setup();
    expect(screen.getByText('Chiusura natalizia')).toBeInTheDocument();
    // 3 inclusive days.
    expect(screen.getByText(/3 giorni/)).toBeInTheDocument();
  });

  it('routes Modifica to onEdit and closes', async () => {
    const onEdit = vi.fn();
    const onClose = vi.fn();
    const { user } = setup({ onEdit, onClose });
    await user.click(screen.getByRole('button', { name: /Modifica/ }));
    expect(onEdit).toHaveBeenCalledWith(closure);
    expect(onClose).toHaveBeenCalled();
  });

  it('routes Elimina to onDelete and closes', async () => {
    const onDelete = vi.fn();
    const onClose = vi.fn();
    const { user } = setup({ onDelete, onClose });
    await user.click(screen.getByRole('button', { name: /Elimina/ }));
    expect(onDelete).toHaveBeenCalledWith(closure);
    expect(onClose).toHaveBeenCalled();
  });
});
