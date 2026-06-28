import { describe, it, expect, vi } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import type { UserEvent } from '@testing-library/user-event';
import { renderWithProviders } from '@/test/render';
import { SuggestCombobox, type SuggestComboboxOption } from './SuggestCombobox';

// AntD's rc-virtual-list renders a hidden measurement copy of each option, so a
// bare findByText can match twice. Click the row that is an actual dropdown
// option (the one inside `.ant-select-item-option`).
async function clickOption(user: UserEvent, text: string): Promise<void> {
  const matches = await screen.findAllByText(text);
  const option = matches.find((el) => el.closest('.ant-select-item-option'));
  expect(option, `dropdown option "${text}" should be present`).toBeDefined();
  await user.click(option!);
}

// --- Canonical component test pattern --------------------------------------
// Render through the full provider stack (i18n + AntD + theme + query) via
// renderWithProviders. Drive it with userEvent and assert observable behaviour
// (what the user sees / which callback fires) — never internal state.

const POOL: SuggestComboboxOption[] = [
  { id: '1', label: 'Alpha' },
  { id: '2', label: 'Beta' },
  { id: '3', label: 'Gamma' },
];

describe('<SuggestCombobox>', () => {
  it('renders the provided placeholder', () => {
    renderWithProviders(
      <SuggestCombobox pool={POOL} onPick={vi.fn()} placeholder="Pick one" />,
    );
    expect(screen.getByPlaceholderText('Pick one')).toBeInTheDocument();
  });

  it('suggests matching pool entries as the user types', async () => {
    const { user } = renderWithProviders(
      <SuggestCombobox pool={POOL} onPick={vi.fn()} placeholder="Pick" />,
    );
    await user.type(screen.getByPlaceholderText('Pick'), 'al');
    // (rc-virtual-list may render a measurement copy → use *AllByText.)
    const alphas = await screen.findAllByText('Alpha');
    expect(alphas.some((el) => el.closest('.ant-select-item-option'))).toBe(true);
    expect(screen.queryAllByText('Beta')).toHaveLength(0);
  });

  it('calls onPick with the chosen pool entry', async () => {
    const onPick = vi.fn();
    const { user } = renderWithProviders(
      <SuggestCombobox pool={POOL} onPick={onPick} placeholder="Pick" />,
    );
    await user.type(screen.getByPlaceholderText('Pick'), 'Beta');
    await clickOption(user, 'Beta');
    expect(onPick).toHaveBeenCalledWith({ id: '2', label: 'Beta' });
  });

  it('offers a create affordance and calls onCreate with the trimmed value', async () => {
    const onCreate = vi.fn();
    const { user } = renderWithProviders(
      <SuggestCombobox
        pool={POOL}
        onPick={vi.fn()}
        onCreate={onCreate}
        placeholder="Pick"
        createLabel="Crea"
      />,
    );
    await user.type(screen.getByPlaceholderText('Pick'), 'Delta');
    // The create row shows the typed value in guillemets.
    await clickOption(user, '«Delta»');
    expect(onCreate).toHaveBeenCalledWith('Delta');
  });

  it('excludes already-attached labels (case-insensitive) from suggestions', async () => {
    const { user } = renderWithProviders(
      <SuggestCombobox
        pool={POOL}
        exclude={['alpha']}
        onPick={vi.fn()}
        placeholder="Pick"
      />,
    );
    await user.type(screen.getByPlaceholderText('Pick'), 'a');
    // Gamma also matches "a"; it must appear while the excluded Alpha must not.
    await waitFor(() =>
      expect(screen.queryAllByText('Gamma').length).toBeGreaterThan(0),
    );
    expect(screen.queryAllByText('Alpha')).toHaveLength(0);
  });
});
