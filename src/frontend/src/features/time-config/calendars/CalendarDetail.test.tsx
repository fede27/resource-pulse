import { describe, it, expect, vi } from 'vitest';
import { screen } from '@testing-library/react';
import { renderWithProviders } from '@/test/render';
import type { BusinessCalendarReadDto } from '@/api/generated/schemas';
import { CalendarDetail } from './CalendarDetail';

const calendar: BusinessCalendarReadDto = {
  id: 'c1',
  name: 'Standard',
  isDefault: false,
  workWindows: [
    {
      id: 'w1',
      dayOfWeek: 1,
      startTime: '09:00:00',
      endTime: '17:00:00',
      validFrom: '2020-01-01',
      validTo: null,
    },
  ],
};

describe('<CalendarDetail>', () => {
  it('renders the calendar header with a set-as-default action for a non-default calendar', () => {
    renderWithProviders(<CalendarDetail calendar={calendar} onDeleted={vi.fn()} />);
    expect(screen.getByRole('heading', { name: 'Standard' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Imposta come default/ })).toBeInTheDocument();
  });

  it('renames the calendar', async () => {
    const { user } = renderWithProviders(
      <CalendarDetail calendar={calendar} onDeleted={vi.fn()} />,
    );
    await user.click(screen.getByRole('button', { name: 'Rinomina' }));
    const input = screen.getByDisplayValue('Standard');
    await user.clear(input);
    await user.type(input, 'Custom');
    await user.click(screen.getByRole('button', { name: 'Salva' }));
    expect(await screen.findByText('Calendario rinominato')).toBeInTheDocument();
  });

  it('switches the week view to "all windows"', async () => {
    const { user } = renderWithProviders(
      <CalendarDetail calendar={calendar} onDeleted={vi.fn()} />,
    );
    // The "all" segmented option is enabled because there is a window; picking
    // it selects that view. (AntD Segmented routes clicks through the label, so
    // click the text but assert on the underlying radio's checked state.)
    const allRadio = screen.getByRole('radio', { name: /Tutte \(1\)/ });
    expect(allRadio).not.toBeChecked();
    await user.click(screen.getByText(/Tutte \(1\)/));
    expect(allRadio).toBeChecked();
  });
});
