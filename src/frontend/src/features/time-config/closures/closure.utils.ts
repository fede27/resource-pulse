import dayjs, { type Dayjs } from 'dayjs';
import type { CompanyClosureReadDto } from '@/api/generated/schemas';

export type ClosureStatus = 'ongoing' | 'upcoming' | 'past';

/** Ongoing = today falls within [dateFrom, dateTo] (both inclusive). */
export function closureStatus(
  c: CompanyClosureReadDto,
  today: Dayjs = dayjs().startOf('day'),
): ClosureStatus {
  const from = c.dateFrom ? dayjs(c.dateFrom) : null;
  const to = c.dateTo ? dayjs(c.dateTo) : from;
  if (
    from &&
    to &&
    (today.isSame(from, 'day') ||
      (today.isAfter(from) && (today.isSame(to, 'day') || today.isBefore(to))))
  ) {
    return 'ongoing';
  }
  if (from && from.isAfter(today, 'day')) return 'upcoming';
  return 'past';
}

/** Inclusive day count of a closure ([from, to] both ends counted). */
export function closureDays(
  fromIso: string | undefined,
  toIso: string | undefined,
): number {
  if (!fromIso) return 0;
  const from = dayjs(fromIso).startOf('day');
  const to = toIso ? dayjs(toIso).startOf('day') : from;
  return Math.max(0, to.diff(from, 'day') + 1);
}

/** Human-friendly inclusive range, collapsing shared month/year. */
export function formatClosureRange(
  fromIso: string | undefined,
  toIso: string | undefined,
): string {
  if (!fromIso) return '—';
  const from = dayjs(fromIso);
  const to = toIso ? dayjs(toIso) : from;
  if (from.isSame(to, 'day')) return from.format('DD MMMM YYYY');
  if (from.isSame(to, 'year') && from.isSame(to, 'month')) {
    return `${from.format('D')}–${to.format('D MMMM YYYY')}`;
  }
  if (from.isSame(to, 'year')) {
    return `${from.format('D MMM')} – ${to.format('D MMM YYYY')}`;
  }
  return `${from.format('D MMM YYYY')} – ${to.format('D MMM YYYY')}`;
}
