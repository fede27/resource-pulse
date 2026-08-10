import { describe, expect, it } from 'vitest';
import { screen, waitFor } from '@testing-library/react';

import { getMeGetMockHandler } from '@/api/generated/me/me.msw';
import { AppRole } from '@/api/generated/schemas/appRole';
import { AccessGate } from '@/auth/AccessGate';
import { useAccess } from '@/auth/access';
import { server } from '@/test/msw/server';
import { renderHookWithProviders, renderWithProviders } from '@/test/render';

const meAs = (isMember: boolean, accessRole?: AppRole) =>
  getMeGetMockHandler({
    isAuthenticated: true,
    sub: 'dev',
    email: 'dev@resourcepulse.local',
    name: 'Dev User',
    isMember,
    ...(accessRole === undefined ? {} : { accessRole }),
  });

describe('useAccess', () => {
  it.each([
    [AppRole.Viewer, { plan: false, administer: false }],
    [AppRole.Planner, { plan: true, administer: false }],
    [AppRole.Owner, { plan: true, administer: true }],
  ])('maps %s to its capabilities', async (role, expected) => {
    server.use(meAs(true, role));

    const { result } = renderHookWithProviders(() => useAccess());

    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.role).toBe(role);
    expect(result.current.can('plan')).toBe(expected.plan);
    expect(result.current.can('administer')).toBe(expected.administer);
  });

  // Fail-close: a member-less principal may do nothing, and neither may one whose
  // membership has not loaded yet — the server refuses either way, so a permissive
  // default would only produce buttons that error when pressed.
  it('grants nothing to a non-member', async () => {
    server.use(meAs(false));

    const { result } = renderHookWithProviders(() => useAccess());

    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.isMember).toBe(false);
    expect(result.current.role).toBeNull();
    expect(result.current.can('plan')).toBe(false);
    expect(result.current.can('administer')).toBe(false);
  });

  // A role sent alongside isMember:false is not a grant.
  it('ignores a role when membership is absent', async () => {
    server.use(meAs(false, AppRole.Owner));

    const { result } = renderHookWithProviders(() => useAccess());

    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.can('administer')).toBe(false);
  });
});

describe('AccessGate', () => {
  it('renders the application for a member', async () => {
    server.use(meAs(true, AppRole.Viewer));

    renderWithProviders(
      <AccessGate>
        <div>contenuto</div>
      </AccessGate>,
    );

    expect(await screen.findByText('contenuto')).toBeInTheDocument();
  });

  // The explanation and the address are the point: a non-member has to know what
  // to ask for and with which identity.
  it('explains the refusal to a non-member instead of rendering the app', async () => {
    server.use(meAs(false));

    renderWithProviders(
      <AccessGate>
        <div>contenuto</div>
      </AccessGate>,
    );

    expect(await screen.findByText(/Non hai accesso a questo spazio/i)).toBeInTheDocument();
    expect(screen.getByText('dev@resourcepulse.local')).toBeInTheDocument();
    expect(screen.queryByText('contenuto')).not.toBeInTheDocument();
  });
});
