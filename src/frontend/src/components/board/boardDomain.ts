import dayjs from 'dayjs';

const ISO = 'YYYY-MM-DD';

// The window a timed view shows — and, everywhere, exactly the window it fetches.
export type BoardDomain = { minISO: string; maxISO: string };

// The widest read endpoints (load, coverage) cap the range at 366 inclusive
// days. Clamping the DOMAIN — not just the request — keeps the axis honest about
// what is actually loaded, instead of drawing months nothing was read for.
export const MAX_DOMAIN_DAYS = 366;

export function clampDomain(d: BoardDomain): BoardDomain {
  const min = dayjs(d.minISO);
  const max = dayjs(d.maxISO);
  if (max.isBefore(min)) return { minISO: d.minISO, maxISO: d.minISO };
  if (max.diff(min, 'day') + 1 <= MAX_DOMAIN_DAYS) return d;
  return { minISO: d.minISO, maxISO: min.add(MAX_DOMAIN_DAYS - 1, 'day').format(ISO) };
}
