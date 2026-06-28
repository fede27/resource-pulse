import { describe, it, expect } from 'vitest';
import { waitFor } from '@testing-library/react';
import { useTeamsGetAll } from '@/api/generated/teams/teams';
import { getTeamsGetAllMockHandler } from '@/api/generated/teams/teams.msw';
import type { LoadResult, TeamReadDto } from '@/api/generated/schemas';
import { renderHookWithProviders } from '@/test/render';
import { server } from '@/test/msw/server';

// Validates the whole network harness: a generated react-query hook → axios
// mutator → MSW handler (sourced from orval, narrowed to a deterministic payload
// for this test). If this is green, every data hook/component test can rely on
// the same wiring. Not about coverage — generated code is excluded — it's about
// proving the infrastructure once.

describe('MSW + orval + react-query harness', () => {
  it('round-trips a generated query hook against an orval handler', async () => {
    const teams: TeamReadDto[] = [{ id: 't1', name: 'Platform', isActive: true }];
    server.use(
      getTeamsGetAllMockHandler({ data: teams, totalCount: 1 } satisfies LoadResult),
    );

    const { result } = renderHookWithProviders(() => useTeamsGetAll());

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    const payload = result.current.data as LoadResult;
    expect(payload.totalCount).toBe(1);
    expect((payload.data as TeamReadDto[])[0]).toMatchObject({ name: 'Platform' });
  });
});
