// Deterministic per-project accent for the expanded lanes: the same project
// keeps the same hue across people and sessions. Pure data derived from the
// central palette (primaries + darker text shades) — resolved from live ids at
// render time and applied as dynamic inline values, so it lives in a ts
// module, not createStyles.

import { blue, cyan, geekblue, gold, green, lime, magenta, orange, purple, volcano } from '@/app/palette';

export type ProjectHue = { accent: string; text: string };

const HUES: ProjectHue[] = [
  { accent: blue[5], text: blue[6] },
  { accent: purple[5], text: purple[6] },
  { accent: cyan[5], text: cyan[6] },
  { accent: magenta[5], text: magenta[6] },
  { accent: orange[5], text: orange[6] },
  { accent: geekblue[5], text: geekblue[6] },
  { accent: green[5], text: green[6] },
  { accent: gold[5], text: gold[6] },
  { accent: volcano[5], text: volcano[6] },
  { accent: lime[5], text: lime[6] },
];

export function projectHue(projectId: string): ProjectHue {
  let h = 0;
  for (let i = 0; i < projectId.length; i += 1) h = (h * 31 + projectId.charCodeAt(i)) >>> 0;
  return HUES[h % HUES.length]!;
}

// The sanctioned tint helper, re-exported under the name lane consumers use.
export { alpha as hueAlpha } from '@/app/palette';
