import { describe, it, expect, vi } from 'vitest';
import { screen } from '@testing-library/react';
import { renderWithProviders } from '@/test/render';
import { InlineEditableText } from './InlineEditableText';
import { SegmentedLevelControl } from './SegmentedLevelControl';
import { StatCard } from './StatCard';
import { StickyTabStrip } from './StickyTabStrip';
import { PageHeader } from './PageHeader';
import { InitialsAvatar } from './InitialsAvatar';
import { YearSelector } from './YearSelector';

describe('<InlineEditableText>', () => {
  it('shows the placeholder when value is empty', () => {
    renderWithProviders(<InlineEditableText value="" placeholder="Add name" onSave={vi.fn()} />);
    expect(screen.getByText('Add name')).toBeInTheDocument();
  });

  it('commits a changed value on Enter', async () => {
    const onSave = vi.fn();
    const { user } = renderWithProviders(<InlineEditableText value="Old" onSave={onSave} />);
    await user.click(screen.getByText('Old'));
    const input = screen.getByDisplayValue('Old');
    await user.clear(input);
    await user.type(input, 'New{Enter}');
    expect(onSave).toHaveBeenCalledWith('New');
  });

  it('reverts on Escape without calling onSave', async () => {
    const onSave = vi.fn();
    const { user } = renderWithProviders(<InlineEditableText value="Keep" onSave={onSave} />);
    await user.click(screen.getByText('Keep'));
    const input = screen.getByDisplayValue('Keep');
    await user.clear(input);
    await user.type(input, 'Discard{Escape}');
    expect(onSave).not.toHaveBeenCalled();
    expect(screen.getByText('Keep')).toBeInTheDocument();
  });

  it('blocks commit and shows the validation error', async () => {
    const onSave = vi.fn();
    const { user } = renderWithProviders(
      <InlineEditableText
        value="Name"
        onSave={onSave}
        validate={(v) => (v.length < 3 ? 'Too short' : null)}
      />,
    );
    await user.click(screen.getByText('Name'));
    const input = screen.getByDisplayValue('Name');
    await user.clear(input);
    await user.type(input, 'ab{Enter}');
    expect(onSave).not.toHaveBeenCalled();
    expect(screen.getByText('Too short')).toBeInTheDocument();
  });

  it('does not enter edit mode when disabled', async () => {
    const { user } = renderWithProviders(
      <InlineEditableText value="Locked" onSave={vi.fn()} disabled />,
    );
    await user.click(screen.getByText('Locked'));
    expect(screen.queryByDisplayValue('Locked')).not.toBeInTheDocument();
  });
});

describe('<SegmentedLevelControl>', () => {
  const options = [
    { value: 1, label: 'Base', description: 'entry' },
    { value: 2, label: 'Mid' },
    { value: 3, label: 'High', description: 'top' },
  ];

  it('reports the chosen value via onChange', async () => {
    const onChange = vi.fn();
    const { user } = renderWithProviders(
      <SegmentedLevelControl value={1} options={options} onChange={onChange} />,
    );
    await user.click(screen.getByText('High'));
    expect(onChange).toHaveBeenCalledWith(3);
  });
});

describe('<StatCard>', () => {
  it('renders label, value and an optional suffix', () => {
    renderWithProviders(<StatCard label="Carico" value="87%" suffix="medio" />);
    expect(screen.getByText('Carico')).toBeInTheDocument();
    expect(screen.getByText('87%')).toBeInTheDocument();
    expect(screen.getByText('medio')).toBeInTheDocument();
  });
});

describe('<StickyTabStrip>', () => {
  const items = [
    { key: 'a', label: 'Alpha', count: 3 },
    { key: 'b', label: 'Beta' },
  ];

  it('renders tabs with optional counts and reports clicks', async () => {
    const onChange = vi.fn();
    const { user } = renderWithProviders(
      <StickyTabStrip items={items} activeKey="a" onChange={onChange} extra={<span>X</span>} />,
    );
    expect(screen.getByText('3')).toBeInTheDocument(); // count chip
    expect(screen.getByText('X')).toBeInTheDocument(); // extra slot
    await user.click(screen.getByText('Beta'));
    expect(onChange).toHaveBeenCalledWith('b');
  });
});

describe('<PageHeader>', () => {
  it('renders title plus optional subtitle and actions', () => {
    renderWithProviders(
      <PageHeader title="Persone" subtitle="Gestione" actions={<button>New</button>} />,
    );
    expect(screen.getByRole('heading', { name: 'Persone' })).toBeInTheDocument();
    expect(screen.getByText('Gestione')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'New' })).toBeInTheDocument();
  });
});

describe('<InitialsAvatar>', () => {
  it('renders the computed initials', () => {
    renderWithProviders(<InitialsAvatar name="Mario Rossi" />);
    expect(screen.getByText('MR')).toBeInTheDocument();
  });
});

describe('<YearSelector>', () => {
  it('steps the year via the next/prev controls', async () => {
    const onChange = vi.fn();
    const { user } = renderWithProviders(
      <YearSelector value={2026} onChange={onChange} availableYears={[2025, 2026]} />,
    );
    await user.click(screen.getByRole('button', { name: '2027' })); // next ‹›
    expect(onChange).toHaveBeenCalledWith(2027);
  });

  it('offers a quick-pick for the visible years', async () => {
    const onChange = vi.fn();
    const { user } = renderWithProviders(
      <YearSelector value={2026} onChange={onChange} availableYears={[2024, 2025, 2026]} />,
    );
    // 2024 is within the visible range; pick it from the segmented control.
    await user.click(screen.getByText('2024'));
    expect(onChange).toHaveBeenCalledWith(2024);
  });
});
