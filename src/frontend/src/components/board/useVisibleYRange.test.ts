import { describe, expect, it } from 'vitest';
import { act, renderHook } from '@testing-library/react';
import { UNBOUNDED_Y, useVisibleYRange } from './useVisibleYRange';

const nextFrame = () => new Promise<void>((r) => requestAnimationFrame(() => r()));

function makeScroller(clientHeight: number): HTMLDivElement {
  const el = document.createElement('div');
  Object.defineProperty(el, 'clientHeight', { value: clientHeight, configurable: true });
  return el;
}

describe('useVisibleYRange', () => {
  it('stays unbounded when the viewport is not measurable (jsdom fallback)', async () => {
    const el = makeScroller(0);
    const { result } = renderHook(() => useVisibleYRange({ current: el }));
    await act(nextFrame);
    expect(result.current).toEqual(UNBOUNDED_Y);
  });

  it('windows around the scroll position, quantized with overscan', async () => {
    const el = makeScroller(700);
    const { result } = renderHook(() => useVisibleYRange({ current: el }, 600, 300));

    // Initial: scrollTop 0 → [0, ceil((700+600)/300)*300].
    expect(result.current).toEqual({ minY: 0, maxY: 1500 });

    await act(async () => {
      el.scrollTop = 4000;
      el.dispatchEvent(new Event('scroll'));
      await nextFrame();
      await nextFrame();
    });
    // min = floor(3400/300)*300 = 3300; max = ceil(5300/300)*300 = 5400.
    expect(result.current).toEqual({ minY: 3300, maxY: 5400 });
  });

  // Every board page renders a loading skeleton first, so the scroller mounts on
  // a LATER render than this hook's first effect. Keyed on the ref object, that
  // effect never re-ran and the range stayed unbounded forever — windowing was
  // silently inert on every board (Disponibilità most visibly, ~80 rows).
  it('picks up a scroller that mounts on a later render', async () => {
    const ref: { current: HTMLDivElement | null } = { current: null };
    const { result, rerender } = renderHook(() => useVisibleYRange(ref, 600, 300));
    expect(result.current).toEqual(UNBOUNDED_Y); // nothing to observe yet

    // Data arrives: the frame mounts and the ref is populated.
    ref.current = makeScroller(700);
    await act(async () => {
      rerender();
      await nextFrame();
    });
    expect(result.current).toEqual({ minY: 0, maxY: 1500 });

    // …and it is really observing that element, not a stale snapshot.
    await act(async () => {
      ref.current!.scrollTop = 4000;
      ref.current!.dispatchEvent(new Event('scroll'));
      await nextFrame();
      await nextFrame();
    });
    expect(result.current).toEqual({ minY: 3300, maxY: 5400 });
  });

  it('does not change identity on sub-step scrolls (coarse re-render steps)', async () => {
    const el = makeScroller(700);
    const { result } = renderHook(() => useVisibleYRange({ current: el }, 600, 300));
    const before = result.current;

    await act(async () => {
      el.scrollTop = 80; // within the same quantized step
      el.dispatchEvent(new Event('scroll'));
      await nextFrame();
      await nextFrame();
    });
    expect(result.current).toBe(before);
  });
});
