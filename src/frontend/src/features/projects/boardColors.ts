// Board-specific semantic color metadata. Pure data (like LOAD_STOPS in
// @/lib/loadBands): these hexes are resolved from live data at render time and
// applied as dynamic inline values, so they live in a ts module, not in
// createStyles. AntD palette hues, aligned with the design prototype.

import type { ArischioReason, DemandRowStatus, Verdict } from './boardModel';

export type VerdictColor = {
  color: string; // text
  stripe: string; // left stripe / dot
  bg: string;
  border: string;
};

export const VERDICT_COLORS: Record<Verdict, VerdictColor> = {
  sostenibile: { color: '#389e0d', stripe: '#52c41a', bg: '#f6ffed', border: '#b7eb8f' },
  arischio: { color: '#d46b08', stripe: '#fa8c16', bg: '#fff7e6', border: '#ffd591' },
  scoperto: { color: '#ad2102', stripe: '#fa541c', bg: '#fff2e8', border: '#ffbb96' },
};

// Uncovered-demand (open role) accent — distinct from load bands; volcano.
export const HOLE_ACCENT = '#fa541c';
export const HOLE_BG = '#fff2e8';
export const HOLE_TEXT = '#ad2102';

// Coverage blocks (hard/tentative) — the blue axis.
export const BLOCK_HARD_BG = '#e6f4ff';
export const BLOCK_BORDER = '#91caff';
export const BLOCK_ACCENT = '#1677ff';
export const BLOCK_ACCENT_SOFT = '#69b1ff';
export const BLOCK_TEXT = '#0958d9';

// Mismatch ("ruolo storto") — purple axis.
export const MISMATCH_TEXT = '#722ed1';
export const MISMATCH_BG = '#f9f0ff';
export const MISMATCH_BORDER = '#d3adf7';

// Demand row status → bar palette (hours reconciliation).
export type DemandStatusColor = { color: string; bar: string; track: string };
export const DEMAND_STATUS_COLORS: Record<DemandRowStatus, DemandStatusColor> = {
  coperta: { color: '#389e0d', bar: '#52c41a', track: '#f6ffed' },
  parziale: { color: '#d46b08', bar: '#faad14', track: '#fffbe6' },
  scoperta: { color: '#ad2102', bar: '#fa541c', track: '#fff2e8' },
  sovra: { color: '#d46b08', bar: '#faad14', track: '#fffbe6' },
  senzaTarget: { color: 'rgba(0,0,0,.55)', bar: '#8c8c8c', track: '#fafafa' },
};

export const OVER_ALLOC_TEXT = '#d46b08';

// Time-fence zones moved to the shared board palette (used by every board
// timeline); re-exported here so feature-local imports keep working.
export { FENCE_FROZEN_TEXT } from '@/components/board/boardColors';

export const CRITICAL_DOT = '#cf1322';

export const REASON_KEY: Record<Exclude<ArischioReason, null>, string> = {
  overload: 'overload',
  mismatch: 'mismatch',
  both: 'both',
};
