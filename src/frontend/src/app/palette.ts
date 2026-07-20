// Tier 1.5 — the single home for RAW colour values outside the AntD theme.
//
// Two chromatic tiers already exist: the AntD theme (`theme.ts`, consumed as
// `token.*` inside `createStyles`) for static styles, and per-feature semantic
// palettes (e.g. `features/projects/boardColors.ts`) for values resolved from
// live data and applied as dynamic inline styles. This module is what the
// semantic palettes DERIVE from: the canonical AntD colour ramps plus the
// neutral scale — named once, never re-typed as hex at a call site.
//
// Rule: **no hex literal outside this file** (see CLAUDE.md → Styling).
// Feature palettes pick shades from here; components import the feature
// palette, not this module's ramps directly (except one-off accents).
//
// Ramps are 10-shade AntD presets, 0-based: `blue[5]` is the palette's
// "blue-6" primary (#1677ff), `[0]` the lightest background, `[6]`/`[7]` the
// text-grade shades.

import {
  blue as antBlue,
  cyan as antCyan,
  geekblue as antGeekblue,
  gold as antGold,
  green as antGreen,
  lime as antLime,
  magenta as antMagenta,
  orange as antOrange,
  purple as antPurple,
  red as antRed,
  volcano as antVolcano,
} from '@ant-design/colors';

// Tuple view of a preset so indexing is non-nullable under
// noUncheckedIndexedAccess (the presets are guaranteed 10 shades).
type Ramp = readonly [
  string, string, string, string, string, string, string, string, string, string,
];
const ramp = (preset: string[]): Ramp => preset as unknown as Ramp;

export const blue = ramp(antBlue);
export const cyan = ramp(antCyan);
export const geekblue = ramp(antGeekblue);
export const gold = ramp(antGold);
export const green = ramp(antGreen);
export const lime = ramp(antLime);
export const magenta = ramp(antMagenta);
export const orange = ramp(antOrange);
export const purple = ramp(antPurple);
export const red = ramp(antRed);
export const volcano = ramp(antVolcano);

// AntD's NEUTRAL ramp (the presets don't export the UI grey scale) plus the
// few off-ramp neutrals the boards use, named by appearance.
export const neutral = {
  white: '#fff',
  black: '#000',
  /** Area/card background (gray-3). */
  bg: '#fafafa',
  /** Barely-off-white empty-cell background. */
  bgFaint: '#fbfbfb',
  /** Fill for muted surfaces (gray-4). */
  fill: '#f5f5f5',
  /** Subtle fill / hairlines on white (gray-5). */
  fillSubtle: '#f0f0f0',
  /** Standard border (gray-6). */
  border: '#d9d9d9',
  /** Disabled/soft marker (gray-7). */
  disabled: '#bfbfbf',
  /** Icon/soft-label grey (gray-8). */
  icon: '#8c8c8c',
  /** Strong neutral text (gray-9). */
  textStrong: '#595959',
  /** Cool light grey used by the load ramp's neutral band background. */
  mist: '#eef0f2',
  /** Hatch pair for "closed/off" surfaces (teams heat grid). */
  hatchLight: '#f7f7f7',
  hatchDim: '#f3f3f3',
} as const;

// The black-alpha text scale (matches AntD's colorText/… tokens for use in
// dynamic inline styles, where `token.*` is out of reach).
export const text = {
  primary: 'rgba(0,0,0,.88)',
  secondary: 'rgba(0,0,0,.65)',
  tertiary: 'rgba(0,0,0,.45)',
  quaternary: 'rgba(0,0,0,.25)',
} as const;

// Off-palette accents that are deliberate design choices (not AntD shades).
/** Steel blue KPI accent (people board "under-used"). */
export const steelBlue = '#3a6ea5';

/** `#rrggbb` → `rgba(r,g,b,a)` — the one sanctioned way to tint a shade. */
export function alpha(hex: string, a: number): string {
  const h = hex.length === 4 ? `#${hex[1]}${hex[1]}${hex[2]}${hex[2]}${hex[3]}${hex[3]}` : hex;
  const n = parseInt(h.slice(1), 16);
  return `rgba(${(n >> 16) & 255},${(n >> 8) & 255},${n & 255},${a})`;
}
