import { describe, expect, it } from 'vitest';
import { http, HttpResponse } from 'msw';
import { fireEvent, screen, waitFor } from '@testing-library/react';

import { getMeGetMockHandler } from '@/api/generated/me/me.msw';
import { AppRole } from '@/api/generated/schemas/appRole';
import { DevRoleSwitcher } from '@/auth/DevRoleSwitcher';
import { server } from '@/test/msw/server';
import { renderWithProviders } from '@/test/render';

const meAs = (isMember: boolean, accessRole?: AppRole) =>
  getMeGetMockHandler({
    isAuthenticated: true,
    sub: 'dev',
    email: 'dev@resourcepulse.local',
    name: 'Dev User',
    isMember,
    ...(accessRole === undefined ? {} : { accessRole }),
  });

describe('DevRoleSwitcher', () => {
  it('reflects the role currently held', async () => {
    server.use(meAs(true, AppRole.Planner));

    renderWithProviders(<DevRoleSwitcher />);

    const planner = await screen.findByRole('radio', { name: 'Planner' });
    expect(planner).toBeChecked();
  });

  // The switch changes the REAL membership through the real endpoint, so that
  // everything downstream behaves as it will in production.
  it('posts the picked role to act-as', async () => {
    server.use(meAs(true, AppRole.Owner));

    let posted: unknown;
    server.use(
      http.post('*/api/dev/access/act-as', async ({ request }) => {
        posted = await request.json();
        return HttpResponse.json(AppRole.Viewer);
      }),
    );

    renderWithProviders(<DevRoleSwitcher />);

    // AntD's Segmented hides its radio inputs behind `pointer-events: none` and
    // drives selection from the label, so userEvent's pointer check refuses the
    // click. fireEvent still produces the change React listens for.
    fireEvent.click(await screen.findByRole('radio', { name: 'Viewer' }));

    await waitFor(() => expect(posted).toEqual({ role: AppRole.Viewer }));
  });

  // Nothing to switch when there is no membership to change.
  it('renders nothing for a non-member', async () => {
    server.use(meAs(false));

    renderWithProviders(<DevRoleSwitcher />);

    await waitFor(() => expect(screen.queryByRole('radio')).not.toBeInTheDocument());
  });
});
