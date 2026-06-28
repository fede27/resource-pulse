import { describe, it, expect } from 'vitest';
import { screen } from '@testing-library/react';
import { renderWithProviders } from '@/test/render';
import { server } from '@/test/msw/server';
import { getTeamsGetAllMockHandler } from '@/api/generated/teams/teams.msw';
import { getResourcesGetAllMockHandler } from '@/api/generated/resources/resources.msw';
import { getRolesGetAllMockHandler } from '@/api/generated/roles/roles.msw';
import { getLoadBandsGetMockHandler } from '@/api/generated/load-bands/load-bands.msw';
import { getBucketingGetMockHandler } from '@/api/generated/bucketing/bucketing.msw';
import { BucketGrain, type LoadResult } from '@/api/generated/schemas';
import { TeamsPage } from './TeamsPage';

function seedConfig() {
  server.use(
    getLoadBandsGetMockHandler({
      bands: [
        { label: 'Healthy', lowerBound: 0 },
        { label: 'Overloaded', lowerBound: 100 },
      ],
    }),
    getBucketingGetMockHandler({
      primaryGrain: BucketGrain.Week,
      secondaryGrain: BucketGrain.Month,
    }),
    getRolesGetAllMockHandler({ data: [{ id: 'role-1', name: 'Engineer' }] } as LoadResult),
  );
}

describe('<TeamsPage>', () => {
  it('renders the team heatmap with team and member rows', async () => {
    seedConfig();
    server.use(
      getTeamsGetAllMockHandler({
        data: [
          { id: 'team-a', name: 'Alpha', isActive: true },
          { id: 'team-b', name: 'Beta', isActive: true },
        ],
      } as LoadResult),
      getResourcesGetAllMockHandler({
        data: [
          { id: 'r1', name: 'Zoe', teamId: 'team-a', roleId: 'role-1' },
          { id: 'r2', name: 'Bob', teamId: 'team-b' },
        ],
      } as LoadResult),
    );

    renderWithProviders(<TeamsPage />);

    // Teams (sorted) render as rows; members render under expanded teams.
    expect(await screen.findByText('Alpha')).toBeInTheDocument();
    expect(screen.getByText('Beta')).toBeInTheDocument();
    expect(screen.getByText('Zoe')).toBeInTheDocument();
    expect(screen.getByText('Bob')).toBeInTheDocument();
  });

  it('toggles values and collapses/expands the team rows', async () => {
    seedConfig();
    server.use(
      getTeamsGetAllMockHandler({
        data: [{ id: 'team-a', name: 'Alpha', isActive: true }],
      } as LoadResult),
      getResourcesGetAllMockHandler({
        data: [{ id: 'r1', name: 'Zoe', teamId: 'team-a', roleId: 'role-1' }],
      } as LoadResult),
    );

    const { user } = renderWithProviders(<TeamsPage />);
    await screen.findByText('Alpha');

    // Members are visible while expanded (the default).
    expect(screen.getByText('Zoe')).toBeInTheDocument();

    // Collapse all → member rows disappear; expand all → they return.
    await user.click(screen.getByRole('button', { name: 'Comprimi tutti' }));
    expect(screen.queryByText('Zoe')).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Espandi tutti' }));
    expect(screen.getByText('Zoe')).toBeInTheDocument();

    // The show-values switch reflects its toggled-on state.
    const valuesSwitch = screen.getByRole('switch');
    expect(valuesSwitch).not.toBeChecked();
    await user.click(valuesSwitch);
    expect(valuesSwitch).toBeChecked();
  });

  it('opens the member and settings popovers from a team row', async () => {
    seedConfig();
    server.use(
      getTeamsGetAllMockHandler({
        data: [{ id: 'team-a', name: 'Alpha', isActive: true }],
      } as LoadResult),
      getResourcesGetAllMockHandler({
        data: [
          { id: 'r1', name: 'Zoe', teamId: 'team-a', roleId: 'role-1' },
          { id: 'r2', name: 'Bob' }, // unassigned → can be added
        ],
      } as LoadResult),
    );

    const { user } = renderWithProviders(<TeamsPage />);
    await screen.findByText('Alpha');

    // Member popover lists the resource pool with add/remove actions.
    await user.click(screen.getAllByRole('button', { name: 'Gestisci membri' })[0]!);
    expect(await screen.findByText('Membri · Alpha')).toBeInTheDocument();

    // Settings popover exposes the destructive delete affordance.
    await user.click(screen.getAllByRole('button', { name: 'Impostazioni team' })[0]!);
    expect(await screen.findByText('Elimina team')).toBeInTheDocument();
  });

  it('shows the empty-state call to action when there are no teams', async () => {
    seedConfig();
    server.use(
      getTeamsGetAllMockHandler({ data: [] } as LoadResult),
      getResourcesGetAllMockHandler({ data: [] } as LoadResult),
    );

    renderWithProviders(<TeamsPage />);

    // The empty Alert + inline create affordance replaces the grid.
    expect(await screen.findByText('Nessun team')).toBeInTheDocument();
    expect(screen.queryByText('Alpha')).not.toBeInTheDocument();
  });
});
