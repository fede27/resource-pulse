// Data layer for the "Disponibilità" timeline. Read surface (no fan-out):
//   1 × GET /api/resources                      (persone + adjustments + windows + calendarId)
//   1 × GET /api/business-calendars             (pattern per calendario)
//   1 × GET /api/company-closures               (azzerano la base per tutti)
//   1 × GET /api/resources/capacity?from&to     (ore effettive, RLE batch — P1)
//   1 × GET /api/teams                          (etichette dei gruppi)
// Effective hours come from the batch capacity read (authoritative); the pure
// model adds the state/decomposition the inspector explains.

import { useMemo } from 'react';
import { useBusinessCalendarsGetAll } from '@/api/generated/business-calendars/business-calendars';
import { useCompanyClosuresGetAll } from '@/api/generated/company-closures/company-closures';
import {
  useResourcesGetAll,
  useResourcesGetCapacities,
} from '@/api/generated/resources/resources';
import { useTeamsGetAll } from '@/api/generated/teams/teams';
import {
  type BusinessCalendarReadDto,
  type CompanyClosureReadDto,
  type LoadResult,
  type ResourceReadDto,
  type TeamReadDto,
} from '@/api/generated/schemas';
import { capacityMapFromSegments, splitRange } from '@/lib/capacity';

export type AvailabilityData = {
  isLoading: boolean;
  resources: ResourceReadDto[];
  calendarsById: Map<string, BusinessCalendarReadDto>;
  calendars: BusinessCalendarReadDto[];
  closures: CompanyClosureReadDto[];
  teamNameById: Map<string, string>;
  capacityByResource: Map<string, ReadonlyMap<string, number>>;
};

const EMPTY_CAP: ReadonlyMap<string, number> = new Map();

export function useAvailabilityData(fromISO: string, toISO: string): AvailabilityData {
  const resourcesQ = useResourcesGetAll();
  const calendarsQ = useBusinessCalendarsGetAll();
  const closuresQ = useCompanyClosuresGetAll();
  const teamsQ = useTeamsGetAll();

  // The requested span is the ALIGNED BUCKET span, not the domain, so it can
  // exceed the read's 366-day cap by a few days (see `splitRange`). Two chunks
  // always suffice: the domain is clamped to the cap and bucket alignment adds
  // at most a month on each side. A single call would 400 and every cell would
  // silently read 0h.
  const chunks = splitRange(fromISO, toISO);
  const head = chunks[0] ?? { from: fromISO, to: toISO };
  const tail = chunks[1];
  const capacitiesQ = useResourcesGetCapacities({ from: head.from, to: head.to });
  const capacitiesTailQ = useResourcesGetCapacities(
    { from: tail?.from ?? head.from, to: tail?.to ?? head.to },
    { query: { enabled: !!tail } },
  );

  const resources = useMemo(() => {
    const rows = ((resourcesQ.data as LoadResult | undefined)?.data ?? []) as ResourceReadDto[];
    return rows
      .filter((r): r is ResourceReadDto & { id: string } => !!r.id && r.isActive !== false)
      .sort((a, b) => (a.name ?? '').localeCompare(b.name ?? ''));
  }, [resourcesQ.data]);

  const calendars = useMemo(
    () =>
      ((calendarsQ.data as LoadResult | undefined)?.data ?? []) as BusinessCalendarReadDto[],
    [calendarsQ.data],
  );

  const calendarsById = useMemo(() => {
    const map = new Map<string, BusinessCalendarReadDto>();
    for (const c of calendars) if (c.id) map.set(c.id, c);
    return map;
  }, [calendars]);

  const closures = useMemo(
    () =>
      ((closuresQ.data as LoadResult | undefined)?.data ?? []) as CompanyClosureReadDto[],
    [closuresQ.data],
  );

  const teamNameById = useMemo(() => {
    const rows = ((teamsQ.data as LoadResult | undefined)?.data ?? []) as TeamReadDto[];
    const map = new Map<string, string>();
    for (const t of rows) if (t.id) map.set(t.id, t.name ?? '—');
    return map;
  }, [teamsQ.data]);

  // Chunks are contiguous and disjoint, so merging is a plain union per resource.
  const capacityByResource = useMemo(() => {
    const map = new Map<string, Map<string, number>>();
    for (const rc of [...(capacitiesQ.data ?? []), ...(capacitiesTailQ.data ?? [])]) {
      if (!rc.resourceId) continue;
      const days = capacityMapFromSegments(rc.segments ?? []);
      const existing = map.get(rc.resourceId);
      if (existing) for (const [iso, hours] of days) existing.set(iso, hours);
      else map.set(rc.resourceId, days);
    }
    return map as Map<string, ReadonlyMap<string, number>>;
  }, [capacitiesQ.data, capacitiesTailQ.data]);

  return {
    isLoading: resourcesQ.isPending || calendarsQ.isPending || closuresQ.isPending,
    resources,
    calendarsById,
    calendars,
    closures,
    teamNameById,
    capacityByResource,
  };
}

export { EMPTY_CAP };
