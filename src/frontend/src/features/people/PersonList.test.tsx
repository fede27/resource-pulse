import { describe, it, expect, vi } from 'vitest';
import { screen } from '@testing-library/react';
import { renderWithProviders } from '@/test/render';
import type { ResourceReadDto } from '@/api/generated/schemas';
import { PersonList } from './PersonList';

const people: ResourceReadDto[] = [
  { id: 'r1', name: 'Mario Rossi', isActive: true, email: 'mario@x.io' },
  { id: 'r2', name: 'Anna Verdi', isActive: false },
];

function defaults() {
  return {
    people,
    selectedId: 'r1',
    onSelect: vi.fn(),
    search: '',
    onSearchChange: vi.fn(),
    onStartCreate: vi.fn(),
    isCreating: false,
    pendingByPerson: { r1: 2 } as Record<string, number>,
    roleNameByPerson: { r1: 'Engineer' } as Record<string, string>,
  };
}

describe('<PersonList>', () => {
  it('renders people with role/secondary line and a pending badge', () => {
    renderWithProviders(<PersonList {...defaults()} />);
    expect(screen.getByText('Mario Rossi')).toBeInTheDocument();
    expect(screen.getByText('Engineer')).toBeInTheDocument(); // role as secondary
    expect(screen.getByText('Inattiva')).toBeInTheDocument(); // inactive secondary
    expect(screen.getByText('2')).toBeInTheDocument(); // pending badge
  });

  it('selects a person on click', async () => {
    const props = defaults();
    const { user } = renderWithProviders(<PersonList {...props} />);
    await user.click(screen.getByText('Anna Verdi'));
    expect(props.onSelect).toHaveBeenCalledWith('r2');
  });

  it('forwards search input changes', async () => {
    const props = defaults();
    const { user } = renderWithProviders(<PersonList {...props} />);
    await user.type(screen.getByPlaceholderText(/Cerca/), 'mar');
    expect(props.onSearchChange).toHaveBeenCalled();
  });

  it('starts create from the add button', async () => {
    const props = defaults();
    const { user } = renderWithProviders(<PersonList {...props} />);
    await user.click(screen.getByRole('button', { name: /Aggiungi/ }));
    expect(props.onStartCreate).toHaveBeenCalled();
  });

  it('shows the search-empty message when the list is empty with a query', () => {
    renderWithProviders(<PersonList {...defaults()} people={[]} search="zzz" />);
    expect(screen.getByText('Nessuna persona corrisponde alla ricerca.')).toBeInTheDocument();
  });

  it('shows the no-people message when the list is empty without a query', () => {
    renderWithProviders(<PersonList {...defaults()} people={[]} search="" />);
    expect(screen.getByText('Aggiungi la prima persona al team.')).toBeInTheDocument();
  });
});
