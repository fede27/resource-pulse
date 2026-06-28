import { describe, it, expect, vi } from 'vitest';
import { screen } from '@testing-library/react';
import { renderWithProviders } from '@/test/render';
import { TeamSettingsPopover } from './TeamSettingsPopover';

function base(over: Partial<Parameters<typeof TeamSettingsPopover>[0]> = {}) {
  const props = {
    team: { id: 'team-a', name: 'Alpha', isActive: true },
    onRename: vi.fn(),
    onToggleActive: vi.fn(),
    onDelete: vi.fn(),
    saving: false,
    ...over,
  };
  return { props, ...renderWithProviders(<TeamSettingsPopover {...props} />) };
}

describe('<TeamSettingsPopover>', () => {
  it('toggles active state from the popover', async () => {
    const { props, user } = base();
    await user.click(screen.getByRole('button', { name: 'Impostazioni team' }));
    await user.click(await screen.findByRole('switch'));
    expect(props.onToggleActive).toHaveBeenCalledWith(false);
  });

  it('commits a renamed team', async () => {
    const { props, user } = base();
    await user.click(screen.getByRole('button', { name: 'Impostazioni team' }));
    const input = await screen.findByDisplayValue('Alpha');
    await user.clear(input);
    await user.type(input, 'Beta');
    await user.click(screen.getByRole('button', { name: 'Salva' }));
    expect(props.onRename).toHaveBeenCalledWith('Beta');
  });
});
