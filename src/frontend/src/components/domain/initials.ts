// Helpers extracted from InitialsAvatar so HMR fast-refresh stays component-only.

const AVATAR_COLORS = [
  '#1677ff',
  '#722ed1',
  '#13c2c2',
  '#52c41a',
  '#fa8c16',
  '#eb2f96',
  '#2f54eb',
  '#fa541c',
];

function hashIndex(seed: string, length: number): number {
  let sum = 0;
  for (let i = 0; i < seed.length; i += 1) sum += seed.charCodeAt(i);
  return sum % length;
}

export function colorForString(seed: string): string {
  if (!seed) return AVATAR_COLORS[0]!;
  return AVATAR_COLORS[hashIndex(seed, AVATAR_COLORS.length)]!;
}

export function initialsOf(name: string): string {
  const parts = name.trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) return '?';
  if (parts.length === 1) return parts[0]!.slice(0, 2).toUpperCase();
  return ((parts[0]![0] ?? '') + (parts[parts.length - 1]![0] ?? '')).toUpperCase();
}
