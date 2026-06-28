import { describe, it, expect } from 'vitest';
import { screen } from '@testing-library/react';
import { renderWithProviders } from '@/test/render';
import { CommitmentLevel } from '@/api/generated/schemas';
import { CommitmentPolicyCard } from './CommitmentPolicyCard';

// Checkboxes render in domain order: Exploratory, Planned, Committed, Critical.
const committed = { hardCommitLevels: [CommitmentLevel.Committed, CommitmentLevel.Critical] };

describe('<CommitmentPolicyCard>', () => {
  it('renders one checkbox per commitment level, the hard ones checked', () => {
    renderWithProviders(<CommitmentPolicyCard committed={committed} />);
    const boxes = screen.getAllByRole('checkbox');
    expect(boxes).toHaveLength(4);
    expect(boxes[2]).toBeChecked(); // Committed
    expect(boxes[3]).toBeChecked(); // Critical
    expect(boxes[0]).not.toBeChecked(); // Exploratory
  });

  it('rejects an empty selection as invalid', async () => {
    const { user } = renderWithProviders(<CommitmentPolicyCard committed={committed} />);
    const boxes = screen.getAllByRole('checkbox');
    await user.click(boxes[2]!); // uncheck Committed
    await user.click(boxes[3]!); // uncheck Critical
    expect(
      screen.getByText("Seleziona almeno un livello: l'insieme non può essere vuoto."),
    ).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Salva' })).toBeDisabled();
  });

  it('enables save after a change and persists it', async () => {
    const { user } = renderWithProviders(<CommitmentPolicyCard committed={committed} />);
    await user.click(screen.getAllByRole('checkbox')[1]!); // check Planned → dirty + valid

    expect(screen.getByText('Modifiche non salvate')).toBeInTheDocument();
    const save = screen.getByRole('button', { name: 'Salva' });
    expect(save).toBeEnabled();

    await user.click(save);
    expect(await screen.findByText('Policy di commitment salvata')).toBeInTheDocument();
  });
});
