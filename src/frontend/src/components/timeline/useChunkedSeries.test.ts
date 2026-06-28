import { describe, it, expect, vi } from 'vitest';
import { waitFor } from '@testing-library/react';
import dayjs from 'dayjs';
import { renderHookWithProviders } from '@/test/render';
import { useChunkedSeries, type ChunkFetcher } from './useChunkedSeries';

type V = { n: number };

const FROM = dayjs('2026-06-01');
const TO = dayjs('2026-06-10');

function setup(fetchChunk: ChunkFetcher<V>, enabled = true) {
  return renderHookWithProviders(() =>
    useChunkedSeries<V>({
      namespace: 'test-series',
      entityIds: ['e1', 'e2'],
      from: FROM,
      to: TO,
      chunkDays: 90,
      fetchChunk,
      enabled,
    }),
  );
}

describe('useChunkedSeries', () => {
  it('fetches per entity and exposes a flat sample accessor', async () => {
    const fetchChunk: ChunkFetcher<V> = vi.fn(async (entityId) =>
      entityId === 'e1'
        ? new Map([['2026-06-05', { n: 1 }]])
        : new Map([['2026-06-05', { n: 2 }]]),
    );
    const { result } = setup(fetchChunk);

    await waitFor(() => expect(result.current.isFetching).toBe(false));

    expect(result.current.sample('e1', '2026-06-05')).toEqual({ n: 1 });
    expect(result.current.sample('e2', '2026-06-05')).toEqual({ n: 2 });
    expect(result.current.sample('e1', '2026-06-06')).toBeUndefined();
    expect(result.current.isChunkReady('2026-06-05')).toBe(true);
  });

  it('does not fetch when disabled', () => {
    const fetchChunk = vi.fn();
    const { result } = setup(fetchChunk as unknown as ChunkFetcher<V>, false);
    expect(fetchChunk).not.toHaveBeenCalled();
    expect(result.current.sample('e1', '2026-06-05')).toBeUndefined();
  });
});
