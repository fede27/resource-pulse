import dayjs from 'dayjs';
import { getAllocationsGetForResourceMockHandler } from '@/api/generated/allocations/allocations.msw';
import { getProjectNodesGetAllMockHandler } from '@/api/generated/project-nodes/project-nodes.msw';
import {
  getResourcesGetAllMockHandler,
  getResourcesGetCapacityMockHandler,
} from '@/api/generated/resources/resources.msw';
import { getRolesGetAllMockHandler } from '@/api/generated/roles/roles.msw';
import { getTeamsGetAllMockHandler } from '@/api/generated/teams/teams.msw';
import {
  AllocationStatus,
  ProjectNodeType,
  ProjectStatus,
  type AllocationReadDto,
  type DailyCapacityDto,
} from '@/api/generated/schemas';
import { server } from '@/test/msw/server';
import { seedConfig } from './settingsConfig';

// Two-people board fixture anchored to the real "today" (the page queries a
// rolling horizon): Luca (Dev senior, Team Alpha) covers ACME hard at 60% for
// ±1 month around today; Elena (no role, Team Beta) is idle.
const ISO = 'YYYY-MM-DD';
const today = dayjs();
const blockFrom = today.subtract(1, 'month').format(ISO);
const blockTo = today.add(1, 'month').format(ISO);

export const lucaAllocations: AllocationReadDto[] = [
  {
    id: 'a-luca',
    demandId: 'd-dev',
    resourceId: 'r-luca',
    resourceName: 'Luca Ferri',
    resourceRoleId: 'role-dev',
    resourceRoleName: 'Dev senior',
    demandRoleId: 'role-dev',
    demandRoleName: 'Dev senior',
    projectNodeId: 'p-acme',
    projectNodePath: '/p-acme',
    periodStart: blockFrom,
    periodEnd: blockTo,
    allocationPercent: 60,
    status: AllocationStatus.Hard,
  },
  {
    id: 'a-luca-beta',
    demandId: 'd-beta',
    resourceId: 'r-luca',
    resourceName: 'Luca Ferri',
    resourceRoleId: 'role-dev',
    resourceRoleName: 'Dev senior',
    demandRoleId: 'role-dev',
    demandRoleName: 'Dev senior',
    projectNodeId: 'p-beta',
    projectNodePath: '/p-beta',
    periodStart: blockFrom,
    periodEnd: blockTo,
    allocationPercent: 30,
    status: AllocationStatus.Tentative,
  },
];

// Weekday capacity 8h over a generous window around today.
function weekdayCapacity(): DailyCapacityDto[] {
  const out: DailyCapacityDto[] = [];
  let d = today.subtract(2, 'month');
  const end = today.add(4, 'month');
  while (!d.isAfter(end)) {
    const wd = (d.day() + 6) % 7;
    out.push({ date: d.format(ISO), hours: wd >= 5 ? 'PT0S' : 'PT8H' });
    d = d.add(1, 'day');
  }
  return out;
}

export function seedPeopleBoard(): void {
  seedConfig(); // load bands (Healthy@0 / Over@100), fence, bucketing (week)
  server.use(
    getResourcesGetAllMockHandler({
      data: [
        { id: 'r-luca', name: 'Luca Ferri', roleId: 'role-dev', teamId: 't-alpha', isActive: true },
        { id: 'r-elena', name: 'Elena Neri', teamId: 't-beta', isActive: true },
      ],
    }),
    getRolesGetAllMockHandler({ data: [{ id: 'role-dev', name: 'Dev senior' }] }),
    getTeamsGetAllMockHandler({
      data: [
        { id: 't-alpha', name: 'Team Alpha' },
        { id: 't-beta', name: 'Team Beta' },
      ],
    }),
    getProjectNodesGetAllMockHandler({
      data: [
        {
          id: 'p-acme',
          nodeType: ProjectNodeType.Project,
          name: 'Portale ACME',
          path: '/p-acme',
          status: ProjectStatus.Active,
        },
        {
          id: 'p-beta',
          nodeType: ProjectNodeType.Project,
          name: 'Migrazione BETA',
          path: '/p-beta',
          status: ProjectStatus.Active,
        },
      ],
    }),
    getResourcesGetCapacityMockHandler(weekdayCapacity()),
    getAllocationsGetForResourceMockHandler((info) =>
      String(info.params['resourceId']) === 'r-luca' ? lucaAllocations : [],
    ),
  );
}
