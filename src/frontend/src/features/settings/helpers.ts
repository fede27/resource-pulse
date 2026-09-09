import dayjs, { type Dayjs } from 'dayjs';
import type { TFunction } from 'i18next';
import { BucketGrain, DurationUnit } from '@/api/generated/schemas';
import { blue, gold, green, neutral, red } from '@/app/palette';

// Strict local shape. The generated DurationDto has optional value/unit
// (Swashbuckle marks reference types nullable); cards normalize into this.
export type Duration = { value: number; unit: DurationUnit };

// Comparison basis only (months ≈ 30d) — mirrors the domain's ApproximateDays.
const UNIT_DAYS: Record<DurationUnit, number> = {
  [DurationUnit.Days]: 1,
  [DurationUnit.Weeks]: 7,
  [DurationUnit.Months]: 30,
};

export const durationToDays = (d: Duration): number => d.value * UNIT_DAYS[d.unit];

// Upper bound on the span a horizon can project, over any start date — months are
// calendar arithmetic, so 12 of them run to 367 days inclusive across a leap
// February while the 30-day approximation above says 360. Mirrors the domain's
// Duration.LongestProjectedDays; used only against the hard cap, never for
// ordering.
const UNIT_LONGEST_DAYS: Record<DurationUnit, number> = {
  [DurationUnit.Days]: 1,
  [DurationUnit.Weeks]: 7,
  [DurationUnit.Months]: 31,
};

export const longestProjectedDays = (d: Duration): number => d.value * UNIT_LONGEST_DAYS[d.unit];

// TimeFenceConfiguration.MaxHorizonDays. Past it the detector cannot read the
// committing horizon at all and the triage queue silently empties, so the server
// refuses — this mirror only spares the Owner a round trip.
export const MAX_HORIZON_DAYS = 366;

export const horizonFitsTheReadCap = (d: Duration): boolean =>
  longestProjectedDays(d) + 1 <= MAX_HORIZON_DAYS;

export const durationUnitKey = (u: DurationUnit): 'days' | 'weeks' | 'months' =>
  u === DurationUnit.Days ? 'days' : u === DurationUnit.Weeks ? 'weeks' : 'months';

export const durationLabel = (d: Duration, t: TFunction): string =>
  `${d.value} ${t(`settings.fence.units.${durationUnitKey(d.unit)}`)}`;

// Calendar projection of the rolling horizon onto a concrete date.
export const addDuration = (from: Dayjs, d: Duration): Dayjs => {
  switch (d.unit) {
    case DurationUnit.Days:
      return from.add(d.value, 'day');
    case DurationUnit.Weeks:
      return from.add(d.value * 7, 'day');
    default:
      return from.add(d.value, 'month');
  }
};

// Semantic ramp neutral→green→blue→amber→red along the bands. Bands stay
// configurable; this is only a visual aid.
const BAND_RAMP = [neutral.icon, green[5], blue[5], gold[5], red[4]];
export const bandColor = (idx: number, total: number): string => {
  if (total <= 1) return BAND_RAMP[2]!;
  const t = idx / (total - 1);
  const pos = Math.round(t * (BAND_RAMP.length - 1));
  return BAND_RAMP[pos]!;
};

export const grainKey = (g: BucketGrain): 'day' | 'week' | 'month' =>
  g === BucketGrain.Day ? 'day' : g === BucketGrain.Week ? 'week' : 'month';

export const today = (): Dayjs => dayjs();
