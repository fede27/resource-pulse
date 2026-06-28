import { describe, it, expect } from 'vitest';
import { http, HttpResponse } from 'msw';
import { screen, waitFor } from '@testing-library/react';
import { renderWithProviders } from '@/test/render';
import { server } from '@/test/msw/server';
import { seedConfig } from '@/test/fixtures/settingsConfig';
import { SettingsPage } from './SettingsPage';

describe('<SettingsPage>', () => {
  it('renders all four org-config cards once data is ready', async () => {
    seedConfig();
    renderWithProviders(<SettingsPage />);

    // One ConfigCard (with its Salva button) per aggregate.
    await waitFor(() =>
      expect(screen.getAllByRole('button', { name: 'Salva' })).toHaveLength(4),
    );
    // Spot-check unique content from individual cards.
    expect(screen.getByText('Granularità primaria')).toBeInTheDocument(); // BucketingCard
    expect(
      screen.getByText('Livelli che abilitano Status = Hard'),
    ).toBeInTheDocument(); // CommitmentPolicyCard
  });

  it('shows an error alert when a config request fails', async () => {
    seedConfig();
    server.use(
      http.get('*/api/config/load-bands', () => new HttpResponse(null, { status: 500 })),
    );
    renderWithProviders(<SettingsPage />);

    expect(
      await screen.findByText('Impossibile caricare le impostazioni.'),
    ).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Salva' })).not.toBeInTheDocument();
  });
});
