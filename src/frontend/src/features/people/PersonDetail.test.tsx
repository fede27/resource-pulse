import { describe, it, expect } from 'vitest';
import { screen } from '@testing-library/react';
import type { UserEvent } from '@testing-library/user-event';
import { renderWithProviders } from '@/test/render';
import { server } from '@/test/msw/server';
import { getRolesGetAllMockHandler } from '@/api/generated/roles/roles.msw';
import { getSkillsGetAllMockHandler } from '@/api/generated/skills/skills.msw';
import { getTagsGetAllMockHandler } from '@/api/generated/tags/tags.msw';
import {
  SkillApprovalStatus,
  SkillLevel,
  type LoadResult,
  type ResourceReadDto,
} from '@/api/generated/schemas';
import { PersonDetail } from './PersonDetail';

const person: ResourceReadDto = {
  id: 'r1',
  name: 'Mario Rossi',
  email: 'mario@x.io',
  isActive: true,
  roleId: 'role-1',
  skills: [
    { skillId: 's1', level: SkillLevel.Proficient, approvalStatus: SkillApprovalStatus.Pending },
    { skillId: 's2', level: SkillLevel.Expert, approvalStatus: SkillApprovalStatus.Approved },
  ],
  tags: [{ tagId: 't1' }],
};

function seed() {
  server.use(
    getRolesGetAllMockHandler({
      data: [
        { id: 'role-1', name: 'Engineer' },
        { id: 'role-2', name: 'Designer' },
      ],
    } as LoadResult),
    getSkillsGetAllMockHandler({
      data: [
        { id: 's1', name: 'React' },
        { id: 's2', name: 'Node' },
        { id: 's3', name: 'Go' },
      ],
    } as LoadResult),
    getTagsGetAllMockHandler({
      data: [
        { id: 't1', name: 'frontend' },
        { id: 't2', name: 'backend' },
      ],
    } as LoadResult),
  );
}

// SuggestCombobox option click (rc-virtual-list renders a measurement copy).
async function pickOption(user: UserEvent, text: string) {
  const matches = await screen.findAllByText(text);
  const opt = matches.find((el) => el.closest('.ant-select-item-option'));
  await user.click(opt!);
}

describe('<PersonDetail>', () => {
  it('renders attached skills, tags, status chips and the pending alert', async () => {
    seed();
    renderWithProviders(<PersonDetail person={person} />);

    expect(await screen.findByText('React')).toBeInTheDocument();
    expect(screen.getByText('Node')).toBeInTheDocument();
    expect(screen.getByText('frontend')).toBeInTheDocument();
    // Mixed approval statuses render distinct chips.
    expect(screen.getByText('Da confermare')).toBeInTheDocument();
    expect(screen.getByText('Confermato')).toBeInTheDocument();
  });

  it('renames the person via the inline editable title', async () => {
    seed();
    const { user } = renderWithProviders(<PersonDetail person={person} />);
    await screen.findByText('React');

    await user.click(screen.getByText('Mario Rossi'));
    const input = screen.getByDisplayValue('Mario Rossi');
    await user.clear(input);
    await user.type(input, 'Mario Bianchi{Enter}');

    expect(await screen.findByText('Modifiche salvate')).toBeInTheDocument();
  });

  it('attaches an existing tag from the pool', async () => {
    seed();
    const { user } = renderWithProviders(<PersonDetail person={person} />);
    await screen.findByText('React');

    await user.type(screen.getByPlaceholderText('Aggiungi un tag…'), 'backend');
    await pickOption(user, 'backend');

    expect(await screen.findByText('Tag aggiunto')).toBeInTheDocument();
  });

  it('attaches an existing skill from the pool', async () => {
    seed();
    const { user } = renderWithProviders(<PersonDetail person={person} />);
    await screen.findByText('React');

    await user.type(screen.getByPlaceholderText('Aggiungi una competenza…'), 'Go');
    await pickOption(user, 'Go');

    expect(await screen.findByText('Competenza aggiunta')).toBeInTheDocument();
  });
});
