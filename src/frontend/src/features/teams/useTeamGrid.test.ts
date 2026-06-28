import { describe, it, expect } from 'vitest';
import { waitFor } from '@testing-library/react';
import { renderHookWithProviders } from '@/test/render';
import { server } from '@/test/msw/server';
import { getTeamsGetAllMockHandler } from '@/api/generated/teams/teams.msw';
import { getResourcesGetAllMockHandler } from '@/api/generated/resources/resources.msw';
import { getRolesGetAllMockHandler } from '@/api/generated/roles/roles.msw';
import { getLoadBandsGetMockHandler } from '@/api/generated/load-bands/load-bands.msw';
import { getBucketingGetMockHandler } from '@/api/generated/bucketing/bucketing.msw';
import { BucketGrain } from '@/api/generated/schemas';
import type {
  LoadResult,
  ResourceReadDto,
  RoleReadDto,
  TeamReadDto,
} from '@/api/generated/schemas';
import { useTeamGrid } from './useTeamGrid';

// Deterministic plan: two teams + four resources (one unassigned, one on a
// stale team id), one role. We assert useTeamGrid's data-shaping (sorting,
// grouping, unassigned bucket, role lookup) — the part that is pure derivation
// over the server reads. (The virtualized viewport has zero width under jsdom,
// so the load series stays empty; that's fine for this hook's data contract.)
const teams: TeamReadDto[] = [
  { id: 'team-b', name: 'Beta', isActive: true },
  { id: 'team-a', name: 'Alpha', isActive: true },
];
const resources: ResourceReadDto[] = [
  { id: 'r1', name: 'Zoe', teamId: 'team-a', roleId: 'role-1' },
  { id: 'r2', name: 'Amy', teamId: 'team-a' },
  { id: 'r3', name: 'Bob', teamId: 'team-b' },
  { id: 'r4', name: 'Lou' }, // no team → unassigned
  { id: 'r5', name: 'Gus', teamId: 'ghost-team' }, // stale team id → unassigned
];
const roles: RoleReadDto[] = [{ id: 'role-1', name: 'Engineer' }];

function seedHandlers() {
  server.use(
    getTeamsGetAllMockHandler({ data: teams, totalCount: teams.length } as LoadResult),
    getResourcesGetAllMockHandler({
      data: resources,
      totalCount: resources.length,
    } as LoadResult),
    getRolesGetAllMockHandler({ data: roles, totalCount: roles.length } as LoadResult),
    getLoadBandsGetMockHandler({
      bands: [
        { label: 'Healthy', lowerBound: 0 },
        { label: 'Over', lowerBound: 100 },
      ],
    }),
    getBucketingGetMockHandler({
      primaryGrain: BucketGrain.Week,
      secondaryGrain: BucketGrain.Month,
    }),
  );
}

describe('useTeamGrid', () => {
  it('sorts teams by name and resolves config-driven grain + bands', async () => {
    seedHandlers();
    const { result } = renderHookWithProviders(() => useTeamGrid());

    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.teams.map((t) => t.name)).toEqual(['Alpha', 'Beta']);
    expect(result.current.primaryGrain).toBe('week');
    expect(result.current.secondaryGrain).toBe('month');
    expect(result.current.bands.map((b) => b.label)).toEqual(['Healthy', 'Over']);
  });

  it('groups members per team (sorted by name) and collects the unassigned', async () => {
    seedHandlers();
    const { result } = renderHookWithProviders(() => useTeamGrid());

    await waitFor(() => expect(result.current.isLoading).toBe(false));

    // team-a members sorted by name: Amy, Zoe.
    expect(result.current.membersByTeam['team-a']).toEqual(['r2', 'r1']);
    expect(result.current.membersByTeam['team-b']).toEqual(['r3']);
    // r4 (no team) + r5 (stale team id) are unassigned, sorted by name: Gus, Lou.
    expect(result.current.unassigned).toEqual(['r5', 'r4']);
  });

  it('builds a role-name lookup and a resources-by-id index', async () => {
    seedHandlers();
    const { result } = renderHookWithProviders(() => useTeamGrid());

    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.roleNameById['role-1']).toBe('Engineer');
    expect(result.current.resourcesById['r1']?.name).toBe('Zoe');
  });
});
