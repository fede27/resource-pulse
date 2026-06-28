import { describe, it, expect } from 'vitest';
import { screen, within } from '@testing-library/react';
import { renderWithProviders } from '@/test/render';
import { BucketGrain } from '@/api/generated/schemas';
import { BucketingCard } from './BucketingCard';

const valid = { primaryGrain: BucketGrain.Week, secondaryGrain: BucketGrain.Month };

describe('<BucketingCard>', () => {
  it('starts clean: Salva is disabled and no error is shown', () => {
    renderWithProviders(<BucketingCard committed={valid} />);
    expect(screen.getByRole('button', { name: 'Salva' })).toBeDisabled();
    expect(
      screen.queryByText('Primaria e secondaria devono essere diverse.'),
    ).not.toBeInTheDocument();
  });

  it('flags primary === secondary as invalid', () => {
    renderWithProviders(
      <BucketingCard
        committed={{ primaryGrain: BucketGrain.Week, secondaryGrain: BucketGrain.Week }}
      />,
    );
    expect(
      screen.getByText('Primaria e secondaria devono essere diverse.'),
    ).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Salva' })).toBeDisabled();
  });

  it('becomes dirty + saveable after changing a grain, then saves', async () => {
    const { user } = renderWithProviders(<BucketingCard committed={valid} />);

    // The primary segmented lives next to its field label.
    const primary = screen.getByText('Granularità primaria').parentElement!;
    await user.click(within(primary).getByText('Giorno'));

    expect(screen.getByText('Modifiche non salvate')).toBeInTheDocument();
    const save = screen.getByRole('button', { name: 'Salva' });
    expect(save).toBeEnabled();

    await user.click(save);
    expect(await screen.findByText('Granularità salvata')).toBeInTheDocument();
  });
});
