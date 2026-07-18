import { describe, expect, it } from 'vitest';
import { act, renderHook } from '@testing-library/react';
import { UNBOUNDED_X, useVisibleXRange } from './useVisibleXRange';

const nextFrame = () => new Promise<void>((r) => requestAnimationFrame(() => r()));

function makeScroller(clientWidth: number): HTMLDivElement {
  const el = document.createElement('div');
  Object.defineProperty(el, 'clientWidth', { value: clientWidth, configurable: true });
  return el;
}

describe('useVisibleXRange', () => {
  it('stays unbounded when the viewport is not measurable (jsdom fallback)', async () => {
    const el = makeScroller(0);
    const { result } = renderHook(() => useVisibleXRange({ current: el }));
    await act(nextFrame);
    expect(result.current).toEqual(UNBOUNDED_X);
  });

  it('windows around the scroll position, quantized with overscan', async () => {
    const el = makeScroller(800);
    const { result } = renderHook(() => useVisibleXRange({ current: el }, 1000, 400));

    // Initial: scrollLeft 0 → [0, ceil(1800/400)*400].
    expect(result.current).toEqual({ minX: 0, maxX: 2000 });

    await act(async () => {
      el.scrollLeft = 5000;
      el.dispatchEvent(new Event('scroll'));
      await nextFrame();
      await nextFrame();
    });
    expect(result.current).toEqual({ minX: 4000, maxX: 6800 });
  });

  it('does not change identity on sub-step scrolls (coarse re-render steps)', async () => {
    const el = makeScroller(800);
    const { result } = renderHook(() => useVisibleXRange({ current: el }, 1000, 400));
    const before = result.current;

    await act(async () => {
      el.scrollLeft = 100; // within the same quantized step
      el.dispatchEvent(new Event('scroll'));
      await nextFrame();
      await nextFrame();
    });
    expect(result.current).toBe(before);
  });
});
