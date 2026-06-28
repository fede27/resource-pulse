import { describe, it, expect } from 'vitest';
import { colorForString, initialsOf } from './initials';

const PALETTE = [
  '#1677ff',
  '#722ed1',
  '#13c2c2',
  '#52c41a',
  '#fa8c16',
  '#eb2f96',
  '#2f54eb',
  '#fa541c',
];

describe('initialsOf', () => {
  it('takes first + last initial for a multi-word name', () => {
    expect(initialsOf('Mario Rossi')).toBe('MR');
    expect(initialsOf('Anna Maria Verdi')).toBe('AV');
  });

  it('takes the first two letters of a single-word name', () => {
    expect(initialsOf('Mario')).toBe('MA');
  });

  it('collapses extra whitespace', () => {
    expect(initialsOf('  Mario   Rossi  ')).toBe('MR');
  });

  it('returns a placeholder for an empty name', () => {
    expect(initialsOf('')).toBe('?');
    expect(initialsOf('   ')).toBe('?');
  });
});

describe('colorForString', () => {
  it('returns the first palette colour for an empty seed', () => {
    expect(colorForString('')).toBe(PALETTE[0]);
  });

  it('is deterministic and stays within the palette', () => {
    const c1 = colorForString('Mario Rossi');
    const c2 = colorForString('Mario Rossi');
    expect(c1).toBe(c2);
    expect(PALETTE).toContain(c1);
  });

  it('distributes different seeds across the palette', () => {
    const colors = new Set(
      ['Alpha', 'Beta', 'Gamma', 'Delta', 'Epsilon'].map(colorForString),
    );
    expect(colors.size).toBeGreaterThan(1);
  });
});
