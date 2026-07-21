import { describe, it, expect, vi } from 'vitest';
import { screen } from '@testing-library/react';
import { renderWithProviders } from '@/test/render';
import type { WorkWindowDto } from '@/api/generated/schemas';
import { DayPatternEditor } from './DayPatternEditor';

// A single Monday window, active today (open-ended validity).
const mondayWindow: WorkWindowDto = {
  id: 'w1',
  dayOfWeek: 1,
  startTime: '09:00:00',
  endTime: '13:00:00',
  validFrom: '2020-01-01',
  validTo: null,
};

// A future Monday change (valid from far in the future).
const futureWindow: WorkWindowDto = {
  id: 'w2',
  dayOfWeek: 1,
  startTime: '14:00:00',
  endTime: '18:00:00',
  validFrom: '2999-01-01',
  validTo: null,
};

function setup(over: Partial<Parameters<typeof DayPatternEditor>[0]> = {}) {
  const props = {
    windows: [mondayWindow],
    view: 'today' as const,
    saving: false,
    deleting: false,
    onCreate: vi.fn(),
    onUpdate: vi.fn(),
    onDelete: vi.fn(),
    onCopyDay: vi.fn(),
    ...over,
  };
  return { props, ...renderWithProviders(<DayPatternEditor {...props} />) };
}

describe('<DayPatternEditor>', () => {
  it('renders a chip and the day total for an active window', () => {
    setup();
    expect(screen.getByRole('button', { name: '09:00–13:00' })).toBeInTheDocument();
    expect(screen.getByText('4h')).toBeInTheDocument();
  });

  it('opens the edit popover when a chip is clicked', async () => {
    const { user } = setup();
    await user.click(screen.getByRole('button', { name: '09:00–13:00' }));
    // Existing window → the popover opens on the edit form (Salva present).
    expect(await screen.findByRole('button', { name: 'Salva' })).toBeInTheDocument();
  });

  it('opens the create popover from "+ fascia"', async () => {
    const { user } = setup();
    const addButtons = screen.getAllByRole('button', { name: /fascia/ });
    await user.click(addButtons[0]!);
    expect(await screen.findByRole('button', { name: 'Aggiungi' })).toBeInTheDocument();
  });

  it('copies the Monday pattern to the weekdays (today scope)', async () => {
    const onCopyDay = vi.fn();
    const { user } = setup({ onCopyDay });
    // Monday is the only day with windows → its copy trigger is the first.
    const copyTriggers = screen.getAllByTitle(/Applica il pattern/);
    await user.click(copyTriggers[0]!);
    await user.click(await screen.findByText('Applica a Lun–Ven'));
    // Source = Monday (1), targets = Tue..Fri (2,3,4,5), scoped to the today view.
    expect(onCopyDay).toHaveBeenCalledWith(1, [2, 3, 4, 5], 'today');
  });

  it('in the future view shows future windows and keeps add/copy available', async () => {
    const { user } = setup({ windows: [mondayWindow, futureWindow], view: 'future' });
    // The future Monday change is shown; the active one is not.
    expect(screen.getByRole('button', { name: '14:00–18:00' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: '09:00–13:00' })).not.toBeInTheDocument();
    // Add/copy stay available so a future pattern can be planned across days.
    expect(screen.getAllByRole('button', { name: /fascia/ }).length).toBeGreaterThan(0);
    expect(screen.getAllByTitle(/Applica il pattern/).length).toBeGreaterThan(0);
    // The future pill opens inspect-first (no Save until Modifica).
    await user.click(screen.getByRole('button', { name: '14:00–18:00' }));
    expect(await screen.findByRole('button', { name: /Modifica/ })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Salva' })).not.toBeInTheDocument();
  });

  it('copies a future window across days from the future view', async () => {
    const onCopyDay = vi.fn();
    const { user } = setup({ windows: [futureWindow], view: 'future', onCopyDay });
    // Monday holds the only future window → its copy trigger is the first.
    const copyTriggers = screen.getAllByTitle(/Applica il pattern/);
    await user.click(copyTriggers[0]!);
    await user.click(await screen.findByText('Applica a Lun–Ven'));
    expect(onCopyDay).toHaveBeenCalledWith(1, [2, 3, 4, 5], 'future');
  });
});
