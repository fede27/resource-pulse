// Deterministic per-project accent for the expanded lanes: the same project
// keeps the same hue across people and sessions. Pure data (AntD palette
// primaries + darker text shades) — resolved from live ids at render time and
// applied as dynamic inline values, so it lives in a ts module, not createStyles.

export type ProjectHue = { accent: string; text: string };

const HUES: ProjectHue[] = [
  { accent: '#1677ff', text: '#0958d9' }, // blue
  { accent: '#722ed1', text: '#531dab' }, // purple
  { accent: '#13c2c2', text: '#08979c' }, // cyan
  { accent: '#eb2f96', text: '#c41d7f' }, // magenta
  { accent: '#fa8c16', text: '#d46b08' }, // orange
  { accent: '#2f54eb', text: '#1d39c4' }, // geekblue
  { accent: '#52c41a', text: '#389e0d' }, // green
  { accent: '#faad14', text: '#d48806' }, // gold
  { accent: '#fa541c', text: '#d4380d' }, // volcano
  { accent: '#a0d911', text: '#7cb305' }, // lime
];

export function projectHue(projectId: string): ProjectHue {
  let h = 0;
  for (let i = 0; i < projectId.length; i += 1) h = (h * 31 + projectId.charCodeAt(i)) >>> 0;
  return HUES[h % HUES.length]!;
}

export function hueAlpha(hex: string, alpha: number): string {
  const n = parseInt(hex.slice(1), 16);
  return `rgba(${(n >> 16) & 255},${(n >> 8) & 255},${n & 255},${alpha})`;
}
