import { describe, it, expect } from 'vitest';
import dayjs from 'dayjs';
import { antdLocaleFor, normalizeLanguage, syncDayjsLocale } from './index';

describe('normalizeLanguage', () => {
  it('passes through supported base languages', () => {
    expect(normalizeLanguage('it')).toBe('it');
    expect(normalizeLanguage('en')).toBe('en');
  });

  it('strips region subtags', () => {
    expect(normalizeLanguage('en-GB')).toBe('en');
    expect(normalizeLanguage('it-IT')).toBe('it');
  });

  it('falls back to Italian for unsupported or empty input', () => {
    expect(normalizeLanguage('fr')).toBe('it');
    expect(normalizeLanguage('')).toBe('it');
  });
});

describe('antdLocaleFor', () => {
  it('returns distinct AntD locale objects per language', () => {
    expect(antdLocaleFor('it')).not.toBe(antdLocaleFor('en'));
    expect(antdLocaleFor('unknown')).toBe(antdLocaleFor('it')); // fallback
  });
});

describe('syncDayjsLocale', () => {
  it('switches the global dayjs locale', () => {
    syncDayjsLocale('en');
    expect(dayjs.locale()).toBe('en-gb');
    syncDayjsLocale('it');
    expect(dayjs.locale()).toBe('it');
  });
});
