// Shared board palette: time-fence zones (geekblue axis) used by every board
// timeline (Progetti, Persone). Pure data — resolved from live geometry at
// render time and applied as dynamic inline values, so it lives in a ts module,
// not in createStyles.

export const FENCE_FROZEN_BG = '#adc6ff';
export const FENCE_SLUSHY_BG = '#d6e4ff';
export const FENCE_LIQUID_BG = '#f0f5ff';
export const FENCE_FROZEN_TEXT = '#10239e';
export const FENCE_SLUSHY_TEXT = '#1d39c4';
export const FENCE_LIQUID_TEXT = 'rgba(47,84,235,.55)';
export const FENCE_TINT_FROZEN = 'rgba(47,84,235,.08)';
export const FENCE_TINT_SLUSHY = 'rgba(47,84,235,.035)';
export const FENCE_EDGE_STRONG = 'rgba(47,84,235,.5)';
export const FENCE_EDGE_SOFT = 'rgba(47,84,235,.3)';
