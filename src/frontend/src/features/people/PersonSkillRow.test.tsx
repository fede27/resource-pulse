import { describe, it, expect, vi } from 'vitest';
import { screen } from '@testing-library/react';
import { renderWithProviders } from '@/test/render';
import { SkillApprovalStatus, SkillLevel } from '@/api/generated/schemas';
import { PersonSkillRow } from './PersonSkillRow';

function row(over: Partial<Parameters<typeof PersonSkillRow>[0]> = {}) {
  const props = {
    name: 'React',
    level: SkillLevel.Proficient,
    status: SkillApprovalStatus.Approved,
    onChangeLevel: vi.fn(),
    onRemove: vi.fn(),
    ...over,
  };
  return { props, ...renderWithProviders(<PersonSkillRow {...props} />) };
}

describe('<PersonSkillRow>', () => {
  it('shows the approved status chip', () => {
    row();
    expect(screen.getByText('Confermato')).toBeInTheDocument();
  });

  it('shows the pending chip for a pending skill', () => {
    row({ status: SkillApprovalStatus.Pending });
    expect(screen.getByText('Da confermare')).toBeInTheDocument();
  });

  it('shows the rejected chip for a rejected skill', () => {
    row({ status: SkillApprovalStatus.Rejected });
    expect(screen.getByText('Respinto')).toBeInTheDocument();
  });

  it('removes via the delete button', async () => {
    const { props, user } = row();
    await user.click(screen.getByRole('button'));
    expect(props.onRemove).toHaveBeenCalled();
  });
});
