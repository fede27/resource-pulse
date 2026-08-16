import { describe, expect, it, vi } from 'vitest';
import { screen, waitFor } from '@testing-library/react';

import { getMeGetMockHandler } from '@/api/generated/me/me.msw';
import { AppRole } from '@/api/generated/schemas/appRole';
import { ConfigCard } from '@/features/settings/ConfigCard';
import { server } from '@/test/msw/server';
import { renderWithProviders } from '@/test/render';

const meAs = (accessRole: AppRole) =>
  getMeGetMockHandler({
    isAuthenticated: true,
    sub: 'dev',
    email: 'dev@resourcepulse.local',
    name: 'Dev User',
    isMember: true,
    accessRole,
  });

// The card is the single choke point for the Owner gate on tenant configuration:
// all four settings cards commit through this footer (ADR-0030).
function renderCard() {
  return renderWithProviders(
    <ConfigCard title="Fasce" dirty valid onSave={vi.fn()} onReset={vi.fn()}>
      <div>corpo</div>
    </ConfigCard>,
  );
}

describe('ConfigCard', () => {
  it('offers save and reset to an owner', async () => {
    server.use(meAs(AppRole.Owner));

    renderCard();

    expect(await screen.findByRole('button', { name: /Salva/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Annulla/i })).toBeInTheDocument();
  });

  // Hidden, not disabled — and the body stays visible, because reading the
  // configuration is a Viewer right.
  it.each([AppRole.Viewer, AppRole.Planner])('hides them from %s', async (role) => {
    server.use(meAs(role));

    renderCard();

    expect(await screen.findByText('corpo')).toBeInTheDocument();
    await waitFor(() =>
      expect(screen.queryByRole('button', { name: /Salva/i })).not.toBeInTheDocument(),
    );
    expect(screen.queryByRole('button', { name: /Annulla/i })).not.toBeInTheDocument();
    expect(screen.getByText(/Sola lettura/i)).toBeInTheDocument();
  });
});
