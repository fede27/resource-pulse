// Shared board palette: time-fence zones (geekblue axis) used by every board
// timeline (Progetti, Persone). Pure data derived from the central palette —
// resolved from live geometry at render time and applied as dynamic inline
// values, so it lives in a ts module, not in createStyles.

import { alpha, geekblue } from '@/app/palette';

export const FENCE_FROZEN_BG = geekblue[2];
export const FENCE_SLUSHY_BG = geekblue[1];
export const FENCE_LIQUID_BG = geekblue[0];
export const FENCE_FROZEN_TEXT = geekblue[7];
export const FENCE_SLUSHY_TEXT = geekblue[6];
export const FENCE_LIQUID_TEXT = alpha(geekblue[5], 0.55);
export const FENCE_TINT_FROZEN = alpha(geekblue[5], 0.08);
export const FENCE_TINT_SLUSHY = alpha(geekblue[5], 0.035);
export const FENCE_EDGE_STRONG = alpha(geekblue[5], 0.5);
export const FENCE_EDGE_SOFT = alpha(geekblue[5], 0.3);
