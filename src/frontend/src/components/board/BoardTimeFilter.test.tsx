import { describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';
import { renderWithProviders } from '@/test/render';
import { BoardTimeFilter } from './BoardTimeFilter';
import { clampDomain, MAX_DOMAIN_DAYS } from './boardDomain';

const makeProps = () => ({
  grain: 'week' as const,
  onGrainChange: vi.fn(),
  domain: { minISO: '2026-05-01', maxISO: '2026-09-30' },
  onDomainChange: vi.fn(),
  onToday: vi.fn(),
  onFit: vi.fn(),
});

describe('<BoardTimeFilter>', () => {
  it('offers the three grains and reports the pick', async () => {
    const props = makeProps();
    const { user } = renderWithProviders(<BoardTimeFilter {...props} />);

    expect(screen.getByText('Grana')).toBeInTheDocument();
    await user.click(screen.getByText('Giorno'));
    expect(props.onGrainChange).toHaveBeenCalledWith('day');
    await user.click(screen.getByText('Mese'));
    expect(props.onGrainChange).toHaveBeenCalledWith('month');
  });

  it('steps the year to a whole-year domain', async () => {
    const props = makeProps();
    const { user } = renderWithProviders(<BoardTimeFilter {...props} />);

    expect(screen.getByText('2026')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /Anno successivo/ }));
    expect(props.onDomainChange).toHaveBeenCalledWith({
      minISO: '2027-01-01',
      maxISO: '2027-12-31',
    });
  });

  it('raises today and fit', async () => {
    const props = makeProps();
    const { user } = renderWithProviders(<BoardTimeFilter {...props} />);

    await user.click(screen.getByRole('button', { name: /Oggi/ }));
    expect(props.onToday).toHaveBeenCalled();
    await user.click(screen.getByRole('button', { name: /Adatta/ }));
    expect(props.onFit).toHaveBeenCalled();
  });
});

describe('clampDomain', () => {
  it('leaves a domain within the API range cap untouched', () => {
    const d = { minISO: '2026-01-01', maxISO: '2026-06-30' };
    expect(clampDomain(d)).toBe(d);
  });

  it('caps an over-wide domain at the range the reads support', () => {
    const capped = clampDomain({ minISO: '2026-01-01', maxISO: '2029-12-31' });
    expect(capped.minISO).toBe('2026-01-01');
    // 366 INCLUSIVE days from Jan 1 of a 365-day year lands on Jan 1 the next.
    expect(capped.maxISO).toBe('2027-01-01');
  });

  it('collapses an inverted domain onto its start', () => {
    expect(clampDomain({ minISO: '2026-05-10', maxISO: '2026-05-01' })).toEqual({
      minISO: '2026-05-10',
      maxISO: '2026-05-10',
    });
  });

  it('MAX_DOMAIN_DAYS is the documented cap', () => {
    expect(MAX_DOMAIN_DAYS).toBe(366);
  });
});
