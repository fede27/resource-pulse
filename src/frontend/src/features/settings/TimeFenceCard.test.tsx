import { describe, it, expect } from 'vitest';
import { screen } from '@testing-library/react';
import { renderWithProviders } from '@/test/render';
import { DurationUnit, type TimeFenceConfigurationDto } from '@/api/generated/schemas';
import { TimeFenceCard } from './TimeFenceCard';

const committed: TimeFenceConfigurationDto = {
  frozenHorizon: { value: 2, unit: DurationUnit.Weeks }, // 14d
  slushyHorizon: { value: 2, unit: DurationUnit.Months }, // 60d
};

describe('<TimeFenceCard>', () => {
  it('renders a valid fence and starts clean', () => {
    renderWithProviders(<TimeFenceCard committed={committed} />);
    expect(screen.getByRole('button', { name: 'Salva' })).toBeDisabled();
    expect(screen.queryByText('frozen deve essere < slushy')).not.toBeInTheDocument();
  });

  it('flags frozen ≥ slushy as invalid', async () => {
    const { user } = renderWithProviders(<TimeFenceCard committed={committed} />);
    // Frozen value is the first numeric input; push it past slushy (60d).
    const frozenValue = screen.getAllByRole('spinbutton')[0]!;
    await user.clear(frozenValue);
    await user.type(frozenValue, '20'); // 20 weeks = 140d > 60d

    expect(screen.getByText('frozen deve essere < slushy')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Salva' })).toBeDisabled();
  });

  it('becomes saveable after a valid edit, then saves', async () => {
    const { user } = renderWithProviders(<TimeFenceCard committed={committed} />);
    const frozenValue = screen.getAllByRole('spinbutton')[0]!;
    await user.clear(frozenValue);
    await user.type(frozenValue, '3'); // 3 weeks = 21d < 60d → valid + dirty

    const save = screen.getByRole('button', { name: 'Salva' });
    expect(save).toBeEnabled();
    await user.click(save);
    expect(await screen.findByText('Time fence salvato')).toBeInTheDocument();
  });
});
