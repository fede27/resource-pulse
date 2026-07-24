// Pure view-model for the "Anagrafica" (role/team-first) view.
//
// The catalog pivots between ruoli and team; both are flat name-only catalogues
// (RoleReadDto / TeamReadDto). People reference a role and an optional team.
// These helpers group people under the pivoted category and surface the "empty
// category" signal (a role with no people contributes no capacity).

import type {
  ResourceReadDto,
  RoleReadDto,
  TeamReadDto,
} from '@/api/generated/schemas';

export type Pivot = 'role' | 'team';

export type Category = { id: string; name: string };

// Normalize a catalogue to {id, name}, dropping rows missing either.
export function toCategories(rows: (RoleReadDto | TeamReadDto)[]): Category[] {
  return rows
    .filter((r): r is (RoleReadDto | TeamReadDto) & { id: string; name: string } =>
      !!r.id && !!r.name,
    )
    .map((r) => ({ id: r.id, name: r.name }))
    .sort((a, b) => a.name.localeCompare(b.name));
}

export function categoryOf(person: ResourceReadDto, pivot: Pivot): string | null {
  return (pivot === 'role' ? person.roleId : person.teamId) ?? null;
}

export function peopleInCategory(
  people: ResourceReadDto[],
  pivot: Pivot,
  categoryId: string,
): ResourceReadDto[] {
  return people.filter((p) => categoryOf(p, pivot) === categoryId);
}

export function countInCategory(
  people: ResourceReadDto[],
  pivot: Pivot,
  categoryId: string,
): number {
  return people.reduce(
    (n, p) => (categoryOf(p, pivot) === categoryId ? n + 1 : n),
    0,
  );
}

// Number of categories with zero people — the "senza persone" warning count.
export function emptyCategoryCount(
  categories: Category[],
  people: ResourceReadDto[],
  pivot: Pivot,
): number {
  return categories.reduce(
    (n, c) => (countInCategory(people, pivot, c.id) === 0 ? n + 1 : n),
    0,
  );
}

// Distinct count of the *other* axis present among a category's people
// (roles → distinct teams involved; teams → distinct roles present).
export function crossAxisCount(
  categoryPeople: ResourceReadDto[],
  pivot: Pivot,
): number {
  const other = new Set<string>();
  for (const p of categoryPeople) {
    const id = pivot === 'role' ? p.teamId : p.roleId;
    if (id) other.add(id);
  }
  return other.size;
}

// Free-text search over name / email / role / team.
export function filterPeople(
  people: ResourceReadDto[],
  query: string,
  roleNameById: ReadonlyMap<string, string>,
  teamNameById: ReadonlyMap<string, string>,
): ResourceReadDto[] {
  const q = query.trim().toLowerCase();
  if (!q) return people;
  return people.filter((p) => {
    const role = p.roleId ? (roleNameById.get(p.roleId) ?? '') : '';
    const team = p.teamId ? (teamNameById.get(p.teamId) ?? '') : '';
    return (
      (p.name ?? '').toLowerCase().includes(q) ||
      (p.email ?? '').toLowerCase().includes(q) ||
      role.toLowerCase().includes(q) ||
      team.toLowerCase().includes(q)
    );
  });
}

export function nameMapById(
  rows: (RoleReadDto | TeamReadDto)[],
): Map<string, string> {
  const map = new Map<string, string>();
  for (const r of rows) if (r.id) map.set(r.id, r.name ?? '—');
  return map;
}
