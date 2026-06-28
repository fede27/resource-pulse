import { describe, it, expect } from 'vitest';
import { screen } from '@testing-library/react';
import { renderWithProviders } from '@/test/render';
import { server } from '@/test/msw/server';
import { getResourcesGetAllMockHandler } from '@/api/generated/resources/resources.msw';
import { getRolesGetAllMockHandler } from '@/api/generated/roles/roles.msw';
import { getSkillsGetAllMockHandler } from '@/api/generated/skills/skills.msw';
import { getTagsGetAllMockHandler } from '@/api/generated/tags/tags.msw';
import {
  SkillApprovalStatus,
  SkillLevel,
  type LoadResult,
  type ResourceReadDto,
} from '@/api/generated/schemas';
import { PeoplePage } from './PeoplePage';

const person: ResourceReadDto = {
  id: 'r1',
  name: 'Mario Rossi',
  isActive: true,
  email: 'mario@x.io',
  roleId: 'role-1',
  skills: [
    {
      skillId: 's1',
      level: SkillLevel.Proficient,
      approvalStatus: SkillApprovalStatus.Pending,
    },
  ],
  tags: [],
};

function seed() {
  server.use(
    getResourcesGetAllMockHandler({ data: [person] } as LoadResult),
    getRolesGetAllMockHandler({ data: [{ id: 'role-1', name: 'Engineer' }] } as LoadResult),
    getSkillsGetAllMockHandler({ data: [{ id: 's1', name: 'React' }] } as LoadResult),
    getTagsGetAllMockHandler({ data: [{ id: 't1', name: 'frontend' }] } as LoadResult),
  );
}

describe('<PeoplePage>', () => {
  it('renders the master list and the selected person detail', async () => {
    seed();
    renderWithProviders(<PeoplePage />);

    // List header count + the person appears (in list and detail).
    expect(await screen.findByText('Persone · 1')).toBeInTheDocument();
    expect(screen.getAllByText('Mario Rossi').length).toBeGreaterThan(0);
  });

  it('opens the inline create form from the add button', async () => {
    seed();
    const { user } = renderWithProviders(<PeoplePage />);

    const add = await screen.findByRole('button', { name: /Aggiungi/ });
    await user.click(add);

    // The inline create form appears with its own title.
    expect(await screen.findByText('Nuova persona')).toBeInTheDocument();
  });
});
