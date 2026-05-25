import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';

export type DayLabels = {
  /** Length 7, Monday-first. */
  short: readonly string[];
  /** Length 7, Monday-first. */
  long: readonly string[];
};

/** Localized day-of-week labels (Monday-first), short and long forms. */
export function useDays(): DayLabels {
  const { t } = useTranslation();
  return useMemo(
    () => ({
      short: t('days.short', { returnObjects: true }) as unknown as readonly string[],
      long: t('days.long', { returnObjects: true }) as unknown as readonly string[],
    }),
    [t],
  );
}
