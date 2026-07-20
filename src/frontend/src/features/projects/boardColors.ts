// Board-specific semantic color metadata. Pure data derived from the central
// palette (like LOAD_STOPS in @/lib/loadBands): these values are resolved from
// live data at render time and applied as dynamic inline values, so they live
// in a ts module, not in createStyles. One axis = one AntD hue ramp.

import { alpha, blue, gold, green, neutral, orange, purple, red, volcano } from '@/app/palette';
import type { AtRiskReason, DemandRowStatus, StatusChipKey, Verdict } from './boardModel';

export type VerdictColor = {
  color: string; // text
  stripe: string; // left stripe / dot
  bg: string;
  border: string;
};

export const VERDICT_COLORS: Record<Verdict, VerdictColor> = {
  sustainable: { color: green[6], stripe: green[5], bg: green[0], border: green[2] },
  atRisk: { color: orange[6], stripe: orange[5], bg: orange[0], border: orange[2] },
  uncovered: { color: volcano[7], stripe: volcano[5], bg: volcano[0], border: volcano[2] },
};

// Uncovered-demand (open role) accent — distinct from load bands; volcano.
export const HOLE_ACCENT = volcano[5];
export const HOLE_BG = volcano[0];
export const HOLE_TEXT = volcano[7];

// Coverage blocks (hard/tentative) — the blue axis.
export const BLOCK_HARD_BG = blue[0];
export const BLOCK_BORDER = blue[2];
export const BLOCK_ACCENT = blue[5];
export const BLOCK_ACCENT_SOFT = blue[3];
export const BLOCK_TEXT = blue[6];

// Mismatch ("ruolo storto") — purple axis.
export const MISMATCH_TEXT = purple[5];
export const MISMATCH_BG = purple[0];
export const MISMATCH_BORDER = purple[2];

// Demand row status → bar palette (hours reconciliation).
export type DemandStatusColor = { color: string; bar: string; track: string };
export const DEMAND_STATUS_COLORS: Record<DemandRowStatus, DemandStatusColor> = {
  covered: { color: green[6], bar: green[5], track: green[0] },
  partial: { color: orange[6], bar: gold[5], track: gold[0] },
  uncovered: { color: volcano[7], bar: volcano[5], track: volcano[0] },
  over: { color: orange[6], bar: gold[5], track: gold[0] },
  noTarget: { color: alpha(neutral.black, 0.55), bar: neutral.icon, track: neutral.bg },
};

export const OVER_ALLOC_TEXT = orange[6];

// Time-fence zones moved to the shared board palette (used by every board
// timeline); re-exported here so feature-local imports keep working.
export { FENCE_FROZEN_TEXT } from '@/components/board/boardColors';

export const CRITICAL_DOT = red[6];

// Project status chip (non-Active states only — Active is the norm, no chip).
export type StatusChipColor = { color: string; border: string; bg: string };
export const STATUS_CHIP_COLORS: Record<StatusChipKey, StatusChipColor> = {
  draft: { color: neutral.icon, border: neutral.border, bg: neutral.bg },
  onHold: { color: gold[6], border: gold[2], bg: gold[0] },
  closed: { color: neutral.textStrong, border: neutral.border, bg: neutral.fill },
  cancelled: { color: red[6], border: red[2], bg: red[0] },
};

export const REASON_KEY: Record<Exclude<AtRiskReason, null>, string> = {
  overload: 'overload',
  mismatch: 'mismatch',
  both: 'both',
};
