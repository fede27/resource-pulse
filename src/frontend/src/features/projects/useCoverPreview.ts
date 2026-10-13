import { useMemo } from 'react';
import { useQueries } from '@tanstack/react-query';
import { getLoadGetResourceLoadProfilesQueryOptions } from '@/api/generated/load/load';
import { getResourcesGetCapacitiesQueryOptions } from '@/api/generated/resources/resources';
import type { LoadSegmentDto } from '@/api/generated/schemas';
import { capacityMapFromSegments, splitRange } from '@/lib/capacity';
import { coverDays, type CoverDay } from './coverModel';

export type CoverPreview = {
  isLoading: boolean;
  // The person's days over the window; an empty list while loading.
  daysOf: (resourceId: string) => CoverDay[];
};

// What the cover dialog needs to price a coverage before creating it: every
// active person's capacity and commitment profile over the demand's window.
//
// Two batch read-models, the same ones the boards read (ADR-0028), each ONE
// call per ≤366-day chunk whatever the population — so every candidate in the
// picker is priced from what is already in hand, and editing the hours costs
// no request at all.
//
// The profile is read with NO status filter. Every coverage this dialog
// creates is tentative: counting hard blocks only would show a "before" and an
// "after" that are always the same.
export function useCoverPreview(from: string | null, to: string | null): CoverPreview {
  const chunks = useMemo(() => (from && to ? splitRange(from, to) : []), [from, to]);

  const capacityQs = useQueries({
    queries: chunks.map((c) => getResourcesGetCapacitiesQueryOptions({ from: c.from, to: c.to })),
  });
  const profileQs = useQueries({
    queries: chunks.map((c) => getLoadGetResourceLoadProfilesQueryOptions({ from: c.from, to: c.to })),
  });

  const capacitySig = capacityQs.map((q) => q.dataUpdatedAt).join(',');
  const profileSig = profileQs.map((q) => q.dataUpdatedAt).join(',');

  const capacityByPerson = useMemo(() => {
    const map = new Map<string, Map<string, number>>();
    for (const q of capacityQs) {
      for (const person of q.data ?? []) {
        if (!person.resourceId) continue;
        const days = map.get(person.resourceId) ?? new Map<string, number>();
        for (const [iso, hours] of capacityMapFromSegments(person.segments ?? [])) days.set(iso, hours);
        map.set(person.resourceId, days);
      }
    }
    return map;
    // eslint-disable-next-line react-hooks/exhaustive-deps -- useQueries returns a new array every render; the signature is the dependency
  }, [capacitySig]);

  const segmentsByPerson = useMemo(() => {
    const map = new Map<string, LoadSegmentDto[]>();
    for (const q of profileQs) {
      for (const person of q.data ?? []) {
        if (!person.resourceId) continue;
        map.set(person.resourceId, [...(map.get(person.resourceId) ?? []), ...(person.segments ?? [])]);
      }
    }
    return map;
    // eslint-disable-next-line react-hooks/exhaustive-deps -- as above
  }, [profileSig]);

  const isLoading = chunks.length > 0 && [...capacityQs, ...profileQs].some((q) => q.isPending);

  const daysOf = useMemo(
    () => (resourceId: string) =>
      from && to && !isLoading
        ? coverDays(from, to, capacityByPerson.get(resourceId), segmentsByPerson.get(resourceId) ?? [])
        : [],
    [from, to, isLoading, capacityByPerson, segmentsByPerson],
  );

  return { isLoading, daysOf };
}
