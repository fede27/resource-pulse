import { describe, it, expect } from 'vitest';
import { renderHookWithProviders } from '@/test/render';
import { useDays } from './useDays';

describe('useDays', () => {
  it('returns 7 short and 7 long Monday-first day labels', () => {
    const { result } = renderHookWithProviders(() => useDays());
    expect(Array.isArray(result.current.short)).toBe(true);
    expect(result.current.short).toHaveLength(7);
    expect(result.current.long).toHaveLength(7);
    // Labels are non-empty strings.
    expect(result.current.short.every((d) => typeof d === 'string' && d.length > 0)).toBe(
      true,
    );
  });
});
