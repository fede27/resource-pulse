// Generic lazy, date-chunked per-entity data fetching for timeline views.
//
// The timeline asks for a date range; this hook splits the range into fixed
// calendar chunks (aligned to a stable epoch so the cache key is independent of
// the requested range), fires one query per (entity × chunk) via useQueries, and
// exposes a flat `sample(entityId, date)` accessor. Re-panning over visited
// ranges is instant (TanStack Query caches each chunk).
//
// The domain supplies `fetchChunk`, which returns a `Map<dateStr, V>` for one
// entity over one chunk — typically by calling orval-generated fetch functions.

import { useMemo } from 'react';
import { useQueries } from '@tanstack/react-query';
import dayjs, { type Dayjs } from 'dayjs';

// Fixed origin for chunk alignment — keeps chunk boundaries (and thus query
// keys) stable no matter what range the viewport asks for.
const CHUNK_EPOCH = dayjs('2020-01-01');

export type ChunkFetcher<V> = (
  entityId: string,
  fromISO: string,
  toISO: string,
  signal?: AbortSignal,
) => Promise<Map<string, V>>;

function chunkIndexOf(date: Dayjs, chunkDays: number): number {
  return Math.floor(date.startOf('day').diff(CHUNK_EPOCH, 'day') / chunkDays);
}

export type ChunkedSeries<V> = {
  sample: (entityId: string, date: string) => V | undefined;
  /** False while the chunk covering `date` is still loading (for shimmer). */
  isChunkReady: (date: string) => boolean;
  isFetching: boolean;
};

export function useChunkedSeries<V>(params: {
  namespace: string;
  entityIds: string[];
  from: Dayjs;
  to: Dayjs;
  chunkDays?: number;
  fetchChunk: ChunkFetcher<V>;
  enabled?: boolean;
  staleTime?: number;
}): ChunkedSeries<V> {
  const {
    namespace,
    entityIds,
    from,
    to,
    chunkDays = 90,
    fetchChunk,
    enabled = true,
    staleTime = 60_000,
  } = params;

  const fromMs = from.valueOf();
  const toMs = to.valueOf();

  const chunkIdxs = useMemo(() => {
    const lo = chunkIndexOf(dayjs(fromMs), chunkDays);
    const hi = chunkIndexOf(dayjs(toMs), chunkDays);
    const out: number[] = [];
    for (let ci = lo; ci <= hi; ci += 1) out.push(ci);
    return out;
  }, [fromMs, toMs, chunkDays]);

  const pairs = useMemo(() => {
    const out: Array<{ entityId: string; ci: number }> = [];
    for (const e of entityIds) for (const ci of chunkIdxs) out.push({ entityId: e, ci });
    return out;
  }, [entityIds, chunkIdxs]);

  const results = useQueries({
    queries: pairs.map(({ entityId, ci }) => {
      const start = CHUNK_EPOCH.add(ci * chunkDays, 'day');
      const fromISO = start.format('YYYY-MM-DD');
      const toISO = start.add(chunkDays - 1, 'day').format('YYYY-MM-DD');
      return {
        queryKey: [namespace, entityId, 'chunk', chunkDays, ci] as const,
        queryFn: ({ signal }: { signal: AbortSignal }) =>
          fetchChunk(entityId, fromISO, toISO, signal),
        enabled: enabled && !!entityId,
        staleTime,
      };
    }),
  });

  const sig = results.map((r) => r.dataUpdatedAt).join(',');
  const pendingSig = results.map((r) => (r.isPending ? '1' : '0')).join('');

  const store = useMemo(() => {
    const map = new Map<string, Map<string, V>>();
    pairs.forEach(({ entityId }, i) => {
      const data = results[i]?.data as Map<string, V> | undefined;
      if (!data) return;
      let m = map.get(entityId);
      if (!m) {
        m = new Map();
        map.set(entityId, m);
      }
      data.forEach((v, d) => m!.set(d, v));
    });
    return map;
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [pairs, sig]);

  // A chunk is "ready" once no entity's query for it is still in its first load.
  const pendingChunks = useMemo(() => {
    const set = new Set<number>();
    pairs.forEach(({ ci }, i) => {
      if (results[i]?.isPending) set.add(ci);
    });
    return set;
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [pairs, pendingSig]);

  return useMemo(
    () => ({
      sample: (entityId: string, date: string) => store.get(entityId)?.get(date),
      isChunkReady: (date: string) => !pendingChunks.has(chunkIndexOf(dayjs(date), chunkDays)),
      isFetching: results.some((r) => r.isFetching),
    }),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [store, pendingChunks, chunkDays, sig, pendingSig],
  );
}
