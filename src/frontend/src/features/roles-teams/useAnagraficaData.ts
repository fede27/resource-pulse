// Reads for the "Anagrafica" view (role/team-first): people + the two
// catalogues + the tag pool, with the id→name maps the grouping helpers need.
// One GET each; TanStack dedups across the page header and the view.

import { useMemo } from 'react';
import {
  useResourcesGetAll,
} from '@/api/generated/resources/resources';
import { useRolesGetAll } from '@/api/generated/roles/roles';
import { useTeamsGetAll } from '@/api/generated/teams/teams';
import { useTagsGetAll } from '@/api/generated/tags/tags';
import {
  type LoadResult,
  type ResourceReadDto,
  type RoleReadDto,
  type TagReadDto,
  type TeamReadDto,
} from '@/api/generated/schemas';
import { nameMapById, toCategories, type Category } from './rolesTeamsModel';

export type AnagraficaData = {
  isLoading: boolean;
  people: ResourceReadDto[];
  roles: Category[];
  teams: Category[];
  tags: TagReadDto[];
  roleNameById: Map<string, string>;
  teamNameById: Map<string, string>;
};

export function useAnagraficaData(): AnagraficaData {
  const resourcesQ = useResourcesGetAll();
  const rolesQ = useRolesGetAll();
  const teamsQ = useTeamsGetAll();
  const tagsQ = useTagsGetAll();

  const people = useMemo(
    () =>
      (((resourcesQ.data as LoadResult | undefined)?.data ?? []) as ResourceReadDto[])
        .filter((r): r is ResourceReadDto & { id: string } => !!r.id)
        .sort((a, b) => (a.name ?? '').localeCompare(b.name ?? '')),
    [resourcesQ.data],
  );

  const roleRows = useMemo(
    () => ((rolesQ.data as LoadResult | undefined)?.data ?? []) as RoleReadDto[],
    [rolesQ.data],
  );
  const teamRows = useMemo(
    () => ((teamsQ.data as LoadResult | undefined)?.data ?? []) as TeamReadDto[],
    [teamsQ.data],
  );
  const tags = useMemo(
    () => ((tagsQ.data as LoadResult | undefined)?.data ?? []) as TagReadDto[],
    [tagsQ.data],
  );

  return {
    isLoading: resourcesQ.isPending || rolesQ.isPending || teamsQ.isPending,
    people,
    roles: useMemo(() => toCategories(roleRows), [roleRows]),
    teams: useMemo(() => toCategories(teamRows), [teamRows]),
    tags,
    roleNameById: useMemo(() => nameMapById(roleRows), [roleRows]),
    teamNameById: useMemo(() => nameMapById(teamRows), [teamRows]),
  };
}
