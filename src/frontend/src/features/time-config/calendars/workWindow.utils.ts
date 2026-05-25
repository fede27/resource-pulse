import dayjs, { type Dayjs } from 'dayjs';
import type { WorkWindowDto, DayOfWeek } from '@/api/generated/schemas';

// Backend DayOfWeek: Sun=0..Sat=6. Display column: Mon=0..Sun=6.
export function dayOfWeekToColumnIndex(dow: DayOfWeek | number | undefined): number {
  const value = (dow ?? 0) as number;
  return value === 0 ? 6 : value - 1;
}

export function columnIndexToDayOfWeek(idx: number): DayOfWeek {
  return (idx === 6 ? 0 : idx + 1) as DayOfWeek;
}

// "HH:mm:ss" or "HH:mm" → minutes since midnight
export function timeToMinutes(t: string | undefined): number {
  if (!t) return 0;
  const [h, m] = t.split(':');
  return Number(h ?? 0) * 60 + Number(m ?? 0);
}

export function minutesToTime(min: number): string {
  const h = Math.floor(min / 60);
  const m = min % 60;
  return `${String(h).padStart(2, '0')}:${String(m).padStart(2, '0')}:00`;
}

export function formatHourMinute(t: string | undefined): string {
  return t ? t.slice(0, 5) : '';
}

function parseValidity(w: WorkWindowDto): { from: Dayjs | null; to: Dayjs | null } {
  return {
    from: w.validFrom ? dayjs(w.validFrom) : null,
    to: w.validTo ? dayjs(w.validTo) : null,
  };
}

export function isWindowActiveOn(w: WorkWindowDto, date: Dayjs): boolean {
  const { from, to } = parseValidity(w);
  if (from && from.isAfter(date, 'day')) return false;
  // validTo is exclusive (half-open)
  if (to && !to.isAfter(date, 'day')) return false;
  return true;
}

export function isWindowActiveToday(w: WorkWindowDto): boolean {
  return isWindowActiveOn(w, dayjs().startOf('day'));
}

export function isWindowHistorical(w: WorkWindowDto): boolean {
  const { to } = parseValidity(w);
  if (!to) return false;
  return !to.isAfter(dayjs().startOf('day'), 'day');
}

export function isWindowFuture(w: WorkWindowDto): boolean {
  const { from } = parseValidity(w);
  if (!from) return false;
  return from.isAfter(dayjs().startOf('day'), 'day');
}

export function weeklyHours(windows: WorkWindowDto[]): number {
  const active = windows.filter(isWindowActiveToday);
  const totalMin = active.reduce(
    (acc, w) => acc + Math.max(0, timeToMinutes(w.endTime) - timeToMinutes(w.startTime)),
    0,
  );
  return totalMin / 60;
}

/**
 * Structured weekly-pattern summary. The caller is responsible for stitching
 * day labels in the user's locale. The shape is computed from active windows
 * (validity-by-today).
 */
export type PatternSummary =
  | { kind: 'empty' }
  | { kind: 'contiguous'; firstDayIdx: number; lastDayIdx: number; slots: string[] }
  | { kind: 'single'; dayIdx: number; slots: string[] }
  | { kind: 'variable'; dayIndices: number[] };

export function patternSummary(windows: WorkWindowDto[]): PatternSummary {
  const active = windows.filter(isWindowActiveToday);
  if (active.length === 0) return { kind: 'empty' };

  const byDay = new Map<number, string[]>();
  for (const w of active) {
    const idx = dayOfWeekToColumnIndex(w.dayOfWeek);
    const slot = `${formatHourMinute(w.startTime)}–${formatHourMinute(w.endTime)}`;
    const existing = byDay.get(idx);
    if (existing) existing.push(slot);
    else byDay.set(idx, [slot]);
  }

  const dayIndices = [...byDay.keys()].sort((a, b) => a - b);
  const firstIdx = dayIndices[0];
  if (firstIdx === undefined) return { kind: 'empty' };

  if (dayIndices.length === 1) {
    return { kind: 'single', dayIdx: firstIdx, slots: byDay.get(firstIdx) ?? [] };
  }

  const signatureOf = (idx: number): string => {
    const slots = byDay.get(idx);
    if (!slots) return '';
    return [...slots].sort().join('|');
  };
  const firstSig = signatureOf(firstIdx);
  const allSame = dayIndices.every((d) => signatureOf(d) === firstSig);
  const contiguous = dayIndices.every(
    (d, i) => i === 0 || d === (dayIndices[i - 1] ?? -1) + 1,
  );

  if (allSame && contiguous) {
    const lastIdx = dayIndices[dayIndices.length - 1] ?? firstIdx;
    return {
      kind: 'contiguous',
      firstDayIdx: firstIdx,
      lastDayIdx: lastIdx,
      slots: byDay.get(firstIdx) ?? [],
    };
  }
  return { kind: 'variable', dayIndices };
}

/** Composes a `PatternSummary` into a localized string given short day labels. */
export function formatPatternSummary(
  summary: PatternSummary,
  shortDayLabels: readonly string[],
  fallbacks: { empty: string; variable: string },
): string {
  switch (summary.kind) {
    case 'empty':
      return fallbacks.empty;
    case 'single':
      return `${shortDayLabels[summary.dayIdx] ?? ''} · ${summary.slots.join(', ')}`;
    case 'contiguous':
      return `${shortDayLabels[summary.firstDayIdx] ?? ''}–${
        shortDayLabels[summary.lastDayIdx] ?? ''
      } · ${summary.slots.join(', ')}`;
    case 'variable':
      return `${summary.dayIndices.map((d) => shortDayLabels[d] ?? '').join(', ')} · ${
        fallbacks.variable
      }`;
  }
}
