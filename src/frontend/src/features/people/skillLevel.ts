import type { TFunction } from 'i18next';
import { SkillLevel } from '@/api/generated/schemas';
import type { LevelOption } from '@/components/domain/SegmentedLevelControl';

export function getSkillLevelOptions(
  t: TFunction,
): ReadonlyArray<LevelOption<SkillLevel>> {
  return [
    {
      value: SkillLevel.Basic,
      label: t('people.levels.basic'),
      description: t('people.levels.basicDesc'),
    },
    {
      value: SkillLevel.Proficient,
      label: t('people.levels.proficient'),
      description: t('people.levels.proficientDesc'),
    },
    {
      value: SkillLevel.Expert,
      label: t('people.levels.expert'),
      description: t('people.levels.expertDesc'),
    },
  ];
}

export function getSkillLevelLabel(level: SkillLevel, t: TFunction): string {
  switch (level) {
    case SkillLevel.Expert:
      return t('people.levels.expert');
    case SkillLevel.Proficient:
      return t('people.levels.proficient');
    case SkillLevel.Basic:
    default:
      return t('people.levels.basic');
  }
}
