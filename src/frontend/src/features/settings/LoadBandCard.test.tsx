import { describe, it, expect } from 'vitest';
import { screen } from '@testing-library/react';
import { renderWithProviders } from '@/test/render';
import type { LoadBandConfigurationDto } from '@/api/generated/schemas';
import { LoadBandCard } from './LoadBandCard';

const committed: LoadBandConfigurationDto = {
  bands: [
    { label: 'Healthy', lowerBound: 0 },
    { label: 'Over', lowerBound: 100 },
  ],
};

describe('<LoadBandCard>', () => {
  it('renders the committed bands and starts clean', () => {
    renderWithProviders(<LoadBandCard committed={committed} />);
    expect(screen.getAllByDisplayValue('Healthy').length).toBeGreaterThan(0);
    expect(screen.getAllByDisplayValue('Over').length).toBeGreaterThan(0);
    expect(screen.getByRole('button', { name: 'Salva' })).toBeDisabled();
  });

  it('adding a band with an empty label is dirty but invalid (save disabled)', async () => {
    const { user } = renderWithProviders(<LoadBandCard committed={committed} />);
    await user.click(screen.getByRole('button', { name: /Aggiungi fascia/ }));

    expect(screen.getByText('Modifiche non salvate')).toBeInTheDocument();
    // The new band has an empty label → configuration invalid.
    expect(screen.getByRole('button', { name: 'Salva' })).toBeDisabled();
  });

  it('labels the new band, then saves a valid configuration', async () => {
    const { user } = renderWithProviders(<LoadBandCard committed={committed} />);
    await user.click(screen.getByRole('button', { name: /Aggiungi fascia/ }));

    // Label inputs are the row textboxes; the third belongs to the new band.
    const labelInputs = screen.getAllByRole('textbox');
    await user.type(labelInputs[2]!, 'Critical');

    const save = screen.getByRole('button', { name: 'Salva' });
    expect(save).toBeEnabled();
    await user.click(save);
    expect(await screen.findByText('Soglie di carico salvate')).toBeInTheDocument();
  });

  it('removes a removable (non-first) band', async () => {
    const { user } = renderWithProviders(<LoadBandCard committed={committed} />);
    expect(screen.getAllByDisplayValue('Over')).toHaveLength(1);
    await user.click(screen.getByRole('button', { name: 'Rimuovi fascia' }));
    expect(screen.queryByDisplayValue('Over')).not.toBeInTheDocument();
  });
});
