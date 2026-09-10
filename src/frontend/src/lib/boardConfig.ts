// The org-level configuration every timed board reads, in the shape a board
// wants it (ADR-0020: bands, time fence, bucketing defaults).
//
// Projects and People both opened the same three queries and derived the same
// four values from them, in byte-identical `useMemo`s. Three copies of that
// would have been three chances for the fence to mean something different on
// two pages showing the same plan — so it is one hook, and a new timed view
// gets the whole block by calling it.
//
// This lives in `lib/` and not in `components/board/`: the shared board module
// is deliberately API-agnostic (nothing under `components/` imports the
// generated client), and `fenceEnd` is already there taking primitives rather
// than a DTO. This is the layer that knows about the endpoints.

import { useMemo } from 'react';
import { useBucketingGet } from '@/api/generated/bucketing/bucketing';
import { useLoadBandsGet } from '@/api/generated/load-bands/load-bands';
import { useTimeFenceGet } from '@/api/generated/time-fence/time-fence';
import { BucketGrain } from '@/api/generated/schemas';
import { fenceEnd, type FenceBoundaries } from '@/components/board';
import type { Grain } from '@/components/timeline';
import { normalizeBands, overloadFloor, type LoadBand } from '@/lib/loadBands';

// BucketGrain (wire) → Grain (the axis vocabulary). Week is the fallback
// because it is the seeded default of BucketingDefaults, so a board that
// renders before the config lands does not flicker through a different grain.
const grainOf = (g: BucketGrain | undefined): Grain =>
  g === BucketGrain.Day ? 'day' : g === BucketGrain.Month ? 'month' : 'week';

export type BoardConfig = {
  bands: LoadBand[];
  overloadThreshold: number;
  fence: FenceBoundaries;
  primaryGrain: Grain;
  secondaryGrain: Grain;
  // The three config reads, as one first-paint gate. Callers OR this with the
  // pending state of whatever rows they draw.
  isPending: boolean;
};

export function useBoardConfig(todayISO: string): BoardConfig {
  const bandsQ = useLoadBandsGet();
  const fenceQ = useTimeFenceGet();
  const bucketingQ = useBucketingGet();

  const bands = useMemo(() => normalizeBands(bandsQ.data?.bands), [bandsQ.data]);

  const fence = useMemo<FenceBoundaries>(
    () => ({
      todayISO,
      frozenEndISO: fenceEnd(todayISO, fenceQ.data?.frozenHorizon?.value, fenceQ.data?.frozenHorizon?.unit),
      slushyEndISO: fenceEnd(todayISO, fenceQ.data?.slushyHorizon?.value, fenceQ.data?.slushyHorizon?.unit),
    }),
    [todayISO, fenceQ.data],
  );

  return {
    bands,
    overloadThreshold: overloadFloor(bands),
    fence,
    primaryGrain: grainOf(bucketingQ.data?.primaryGrain),
    secondaryGrain: grainOf(bucketingQ.data?.secondaryGrain),
    isPending: bandsQ.isPending || fenceQ.isPending || bucketingQ.isPending,
  };
}
