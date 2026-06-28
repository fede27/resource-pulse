import { describe, it, expect, vi } from 'vitest';
import { screen } from '@testing-library/react';
import { renderWithProviders } from '@/test/render';
import type { WorkWindowDto } from '@/api/generated/schemas';
import { WorkWindowPopoverContent } from './WorkWindowPopover';

function base(over: Partial<Parameters<typeof WorkWindowPopoverContent>[0]> = {}) {
  const props = {
    initial: null,
    saving: false,
    deleting: false,
    onSubmit: vi.fn(),
    onCancel: vi.fn(),
    ...over,
  };
  return { props, ...renderWithProviders(<WorkWindowPopoverContent {...props} />) };
}

describe('<WorkWindowPopoverContent>', () => {
  it('submits the default new-window values', async () => {
    const onSubmit = vi.fn();
    const { user } = base({ onSubmit });
    await user.click(screen.getByRole('button', { name: 'Aggiungi' }));
    expect(onSubmit).toHaveBeenCalledTimes(1);
    // Default day-of-week is Monday (1); times default to 09:00–13:00.
    expect(onSubmit.mock.calls[0]![0]).toMatchObject({ dayOfWeek: 1 });
  });

  it('cancels without submitting', async () => {
    const { props, user } = base();
    await user.click(screen.getByRole('button', { name: 'Annulla' }));
    expect(props.onCancel).toHaveBeenCalled();
    expect(props.onSubmit).not.toHaveBeenCalled();
  });

  it('shows save + delete affordances in edit mode', async () => {
    const initial: WorkWindowDto = {
      id: 'w1',
      dayOfWeek: 2,
      startTime: '08:00:00',
      endTime: '16:00:00',
      validFrom: '2026-01-01',
      validTo: '2026-06-30',
    };
    const onDelete = vi.fn();
    const { user } = base({ initial, onDelete });
    expect(screen.getByRole('button', { name: 'Salva' })).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /Elimina/ }));
    expect(onDelete).toHaveBeenCalled();
  });
});
