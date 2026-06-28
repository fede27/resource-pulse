import { describe, it, expect, vi } from 'vitest';
import { screen } from '@testing-library/react';
import { renderWithProviders } from '@/test/render';
import { TeamCreateInline } from './TeamCreateInline';

describe('<TeamCreateInline>', () => {
  it('expands into an input and creates a trimmed team name', async () => {
    const onCreate = vi.fn();
    const { user } = renderWithProviders(<TeamCreateInline onCreate={onCreate} saving={false} />);

    await user.click(screen.getByRole('button', { name: /Nuovo team/ }));
    await user.type(screen.getByPlaceholderText('es. Core Platform'), '  Platform  ');
    await user.click(screen.getByRole('button', { name: 'Crea' }));

    expect(onCreate).toHaveBeenCalledWith('Platform');
  });

  it('ignores an empty submission', async () => {
    const onCreate = vi.fn();
    const { user } = renderWithProviders(<TeamCreateInline onCreate={onCreate} saving={false} />);

    await user.click(screen.getByRole('button', { name: /Nuovo team/ }));
    await user.click(screen.getByRole('button', { name: 'Crea' }));

    expect(onCreate).not.toHaveBeenCalled();
  });
});
