import { describe, it, expect } from 'vitest';
import { screen } from '@testing-library/react';
import { renderWithProviders } from '@/test/render';
import { server } from '@/test/msw/server';
import { getBusinessCalendarsGetAllMockHandler } from '@/api/generated/business-calendars/business-calendars.msw';
import { getCompanyClosuresGetAllMockHandler } from '@/api/generated/company-closures/company-closures.msw';
import type {
  BusinessCalendarReadDto,
  CompanyClosureReadDto,
  LoadResult,
} from '@/api/generated/schemas';

import { TimeConfigPage } from './TimeConfigPage';

const calendar: BusinessCalendarReadDto = {
  id: 'c1',
  name: 'Standard',
  isDefault: true,
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

const closure: CompanyClosureReadDto = {
  id: 'cl1',
  dateFrom: '2026-12-24',
  dateTo: '2026-12-26',
  reason: 'Natale',
};

function seed() {
  server.use(
    getBusinessCalendarsGetAllMockHandler({ data: [calendar] } as LoadResult),
    getCompanyClosuresGetAllMockHandler({ data: [closure] } as LoadResult),
  );
}

describe('<TimeConfigPage>', () => {
  it('renders the calendars tab with the calendar detail by default', async () => {
    seed();
    renderWithProviders(<TimeConfigPage />);
    // 'Standard' appears in both the list and the detail header.
    expect((await screen.findAllByText('Standard')).length).toBeGreaterThan(0);
  });

  it('switches to the closures tab and lists the year closures', async () => {
    seed();
    const { user } = renderWithProviders(<TimeConfigPage />);
    await screen.findAllByText('Standard');

    await user.click(screen.getByText('Chiusure'));
    expect(await screen.findByText('Natale')).toBeInTheDocument();
    expect(screen.getByText('In arrivo')).toBeInTheDocument(); // upcoming section

    // Opening the create form reveals the inline closure editor.
    await user.click(screen.getByRole('button', { name: /Nuova chiusura/ }));
    expect(screen.getByPlaceholderText(/Motivazione/)).toBeInTheDocument();
  });

  it('opens the inline create form on the calendars tab', async () => {
    seed();
    const { user } = renderWithProviders(<TimeConfigPage />);
    await screen.findAllByText('Standard');

    await user.click(screen.getByRole('button', { name: /Nuovo/ }));
    expect(screen.getByPlaceholderText('Nome del calendario')).toBeInTheDocument();
  });
});
