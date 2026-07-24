import { describe, expect, it } from 'vitest';
import type { ResourceReadDto } from '@/api/generated/schemas';
import {
  categoryOf,
  countInCategory,
  crossAxisCount,
  emptyCategoryCount,
  filterPeople,
  nameMapById,
  peopleInCategory,
  toCategories,
} from './rolesTeamsModel';

const person = (over: Partial<ResourceReadDto>): ResourceReadDto => ({
  id: 'x',
  name: 'N',
  isActive: true,
  ...over,
});

const people: ResourceReadDto[] = [
  person({ id: '1', name: 'Ada', roleId: 'r1', teamId: 't1', email: 'ada@x.it' }),
  person({ id: '2', name: 'Bob', roleId: 'r1', teamId: 't2' }),
  person({ id: '3', name: 'Cy', roleId: 'r2', teamId: 't1' }),
];

describe('toCategories', () => {
  it('normalizes and sorts, dropping rows missing id/name', () => {
    const cats = toCategories([
      { id: 'b', name: 'Beta' },
      { id: 'a', name: 'Alpha' },
      { id: 'c' },
    ]);
    expect(cats.map((c) => c.name)).toEqual(['Alpha', 'Beta']);
  });
});

describe('grouping helpers', () => {
  it('categoryOf reads the pivoted id', () => {
    expect(categoryOf(people[0]!, 'role')).toBe('r1');
    expect(categoryOf(people[0]!, 'team')).toBe('t1');
  });

  it('peopleInCategory + countInCategory filter by pivot', () => {
    expect(peopleInCategory(people, 'role', 'r1').map((p) => p.id)).toEqual(['1', '2']);
    expect(countInCategory(people, 'role', 'r1')).toBe(2);
    expect(countInCategory(people, 'team', 't1')).toBe(2);
  });

  it('emptyCategoryCount counts categories with no people', () => {
    const roles = toCategories([
      { id: 'r1', name: 'Dev' },
      { id: 'r2', name: 'QA' },
      { id: 'r3', name: 'PM' },
    ]);
    expect(emptyCategoryCount(roles, people, 'role')).toBe(1); // r3 empty
  });

  it('crossAxisCount counts distinct other-axis ids', () => {
    // role r1 spans teams t1 + t2
    expect(crossAxisCount(peopleInCategory(people, 'role', 'r1'), 'role')).toBe(2);
    // team t1 spans roles r1 + r2
    expect(crossAxisCount(peopleInCategory(people, 'team', 't1'), 'team')).toBe(2);
  });
});

describe('filterPeople', () => {
  const roleNames = new Map([['r1', 'Developer'], ['r2', 'Tester']]);
  const teamNames = new Map([['t1', 'Alpha'], ['t2', 'Beta']]);

  it('returns all when the query is blank', () => {
    expect(filterPeople(people, '  ', roleNames, teamNames)).toHaveLength(3);
  });

  it('matches by name, email, role and team', () => {
    expect(filterPeople(people, 'ada', roleNames, teamNames).map((p) => p.id)).toEqual(['1']);
    expect(filterPeople(people, 'ada@x', roleNames, teamNames).map((p) => p.id)).toEqual(['1']);
    expect(filterPeople(people, 'develop', roleNames, teamNames).map((p) => p.id)).toEqual(['1', '2']);
    expect(filterPeople(people, 'beta', roleNames, teamNames).map((p) => p.id)).toEqual(['2']);
  });
});

describe('nameMapById', () => {
  it('maps id → name, defaulting missing names', () => {
    const map = nameMapById([{ id: 'a', name: 'Alpha' }, { id: 'b' }]);
    expect(map.get('a')).toBe('Alpha');
    expect(map.get('b')).toBe('—');
  });
});
