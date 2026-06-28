import { describe, it, expect } from 'vitest';
import type { TFunction } from 'i18next';
import { SkillLevel } from '@/api/generated/schemas';
import { getSkillLevelLabel, getSkillLevelOptions } from './skillLevel';

const t = ((key: string) => key) as unknown as TFunction;

describe('getSkillLevelOptions', () => {
  it('lists the three levels in ascending order with labels', () => {
    const opts = getSkillLevelOptions(t);
    expect(opts.map((o) => o.value)).toEqual([
      SkillLevel.Basic,
      SkillLevel.Proficient,
      SkillLevel.Expert,
    ]);
    expect(opts[0]).toMatchObject({ label: 'people.levels.basic' });
    expect(opts.every((o) => !!o.description)).toBe(true);
  });
});

describe('getSkillLevelLabel', () => {
  it('returns the level-specific label', () => {
    expect(getSkillLevelLabel(SkillLevel.Expert, t)).toBe('people.levels.expert');
    expect(getSkillLevelLabel(SkillLevel.Proficient, t)).toBe('people.levels.proficient');
    expect(getSkillLevelLabel(SkillLevel.Basic, t)).toBe('people.levels.basic');
  });
});
