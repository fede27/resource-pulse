import { describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { renderWithProviders } from '@/test/render';
import { defaultFilters, type BoardFilters } from './boardModel';
import { BoardToolbar, type BoardToolbarProps } from './BoardToolbar';

function makeProps(filters?: Partial<BoardFilters>): BoardToolbarProps {
  return {
    metric: 'pct',
    onMetricChange: vi.fn(),
    bucket: 'week',
    onBucketChange: vi.fn(),
    domain: { minISO: '2026-05-01', maxISO: '2026-09-30' },
    onDomainChange: vi.fn(),
    onToday: vi.fn(),
    onFit: vi.fn(),
    filters: { ...defaultFilters(), ...filters },
    onFiltersChange: vi.fn(),
    personPool: [
      { id: 'r-luca', name: 'Luca Ferri', roleName: 'Dev senior' },
      { id: 'r-anna', name: 'Anna Bianchi', roleName: null },
    ],
    roles: ['Dev senior', 'Grafico'],
    resultCount: 2,
    totalCount: 5,
  };
}

describe('<BoardToolbar>', () => {
  it('shows the result count against the total and the default-lifecycle chip', () => {
    renderWithProviders(<BoardToolbar {...makeProps()} />);

    expect(screen.getByText('2')).toBeInTheDocument();
    expect(screen.getByText(/progetti/)).toBeInTheDocument();
    expect(screen.getByText(/di 5/)).toBeInTheDocument();
    // Default filters hide closed projects → the lifecycle chip is visible.
    expect(screen.getByText(/Ciclo:/)).toBeInTheDocument();
  });

  it('renders one chip per active facet and removes it on close', async () => {
    const props = makeProps({
      mineOwner: true,
      mineHoles: true,
      hideEmpty: true,
      people: new Set(['r-luca']),
      roles: new Set(['Grafico']),
      provenance: new Set(['proposed'] as const),
      sustain: new Set(['uncovered'] as const),
    });
    const user = userEvent.setup();
    renderWithProviders(<BoardToolbar {...props} />);

    expect(screen.getByText('Di cui sono owner')).toBeInTheDocument();
    expect(screen.getByText('Miei ruoli scoperti')).toBeInTheDocument();
    expect(screen.getByText('Solo con attività nel range')).toBeInTheDocument();
    expect(screen.getByText('Luca Ferri')).toBeInTheDocument();
    expect(screen.getByText('Ruolo: Grafico')).toBeInTheDocument();
    expect(screen.getByText('Provenienza: Proposto')).toBeInTheDocument();
    expect(screen.getByText('Sostenibilità: Scoperto')).toBeInTheDocument();

    // Removing the owner chip patches only that facet.
    const chip = screen.getByText('Di cui sono owner').closest('.ant-tag')!;
    await user.click(chip.querySelector('.ant-tag-close-icon')!);
    expect(props.onFiltersChange).toHaveBeenCalledWith(
      expect.objectContaining({ mineOwner: false }),
    );
  });

  it('opens the filter panel and toggles a facet checkbox', async () => {
    const props = makeProps();
    const user = userEvent.setup();
    renderWithProviders(<BoardToolbar {...props} />);

    await user.click(screen.getByRole('button', { name: /Filtri/ }));
    expect(await screen.findByText('Ciclo di vita')).toBeInTheDocument();

    await user.click(screen.getByText('Di cui sono owner (PM)'));
    expect(props.onFiltersChange).toHaveBeenCalledWith(
      expect.objectContaining({ mineOwner: true }),
    );
  });

  it('steps the year and triggers today/fit', async () => {
    const props = makeProps();
    const user = userEvent.setup();
    renderWithProviders(<BoardToolbar {...props} />);

    await user.click(screen.getByRole('button', { name: /Anno successivo/ }));
    expect(props.onDomainChange).toHaveBeenCalledWith({ minISO: '2027-01-01', maxISO: '2027-12-31' });

    await user.click(screen.getByRole('button', { name: /Oggi/ }));
    expect(props.onToday).toHaveBeenCalled();

    await user.click(screen.getByRole('button', { name: /Adatta/ }));
    expect(props.onFit).toHaveBeenCalled();
  });

  it('resets to defaults from the clear link', async () => {
    const props = makeProps({ mineOwner: true });
    const user = userEvent.setup();
    renderWithProviders(<BoardToolbar {...props} />);

    await user.click(screen.getByRole('button', { name: /Azzera/ }));
    expect(props.onFiltersChange).toHaveBeenCalledWith(defaultFilters());
  });
});
