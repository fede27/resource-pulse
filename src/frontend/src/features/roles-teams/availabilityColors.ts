// Semantic palette for the availability day-states, derived from the canonical
// AntD ramps in app/palette.ts (rule: no hex literal outside palette.ts). These
// are resolved from live per-day data and applied as dynamic inline styles on
// the timeline cells / inspector, so they live here as data — not in a
// createStyles token map.

import { blue, neutral, orange, purple, text } from '@/app/palette';
import type { DayState } from './availabilityModel';

export type StateColors = {
  fg: string;
  bg: string;
  border: string;
  dot: string;
};

// Diagonal hatch for company-closure / off surfaces (decorative pattern).
export const CLOSURE_HATCH =
  `repeating-linear-gradient(45deg, ${neutral.hatchLight}, ${neutral.hatchLight} 4px, ${neutral.hatchDim} 4px, ${neutral.hatchDim} 8px)`;

export const STATE_COLORS: Record<DayState, StateColors> = {
  work: { fg: blue[6], bg: blue[0], border: blue[2], dot: blue[5] },
  ferie: { fg: purple[6], bg: purple[0], border: purple[2], dot: purple[5] },
  extra: { fg: orange[6], bg: orange[0], border: orange[2], dot: orange[5] },
  closure: {
    fg: text.tertiary,
    bg: CLOSURE_HATCH,
    border: neutral.fillSubtle,
    dot: neutral.disabled,
  },
  off: {
    fg: text.quaternary,
    bg: neutral.white,
    border: neutral.fill,
    dot: neutral.fillSubtle,
  },
};

// Accent dots for the adjustment markers (ferie = purple, extra = orange).
export const FERIE_DOT = purple[5];
export const EXTRA_DOT = orange[5];
