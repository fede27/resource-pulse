import dayjs from 'dayjs';
import { getAllocationsGetInRangeMockHandler } from '@/api/generated/allocations/allocations.msw';
import { getProjectNodesGetAllMockHandler } from '@/api/generated/project-nodes/project-nodes.msw';
import {
  getResourcesGetAllMockHandler,
  getResourcesGetCapacitiesMockHandler,
} from '@/api/generated/resources/resources.msw';
import { getRolesGetAllMockHandler } from '@/api/generated/roles/roles.msw';
import { getTeamsGetAllMockHandler } from '@/api/generated/teams/teams.msw';
import {
  AllocationStatus,
  ProjectNodeType,
  ProjectStatus,
  type AllocationReadDto,
  type CapacitySegmentDto,
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
    rootProjectId: 'p-acme',
    rootProjectName: 'Portale ACME',
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
    rootProjectId: 'p-beta',
    rootProjectName: 'Migrazione BETA',
    periodStart: blockFrom,
    periodEnd: blockTo,
    allocationPercent: 30,
    status: AllocationStatus.Tentative,
  },
];

// Weekday capacity 8h over a generous window around today, in the batch
// endpoint's run-length form: one Mon–Fri segment per week, weekends as gaps.
function weekdaySegments(): CapacitySegmentDto[] {
  const out: CapacitySegmentDto[] = [];
  let d = today.subtract(2, 'month');
  while (d.day() !== 1) d = d.add(1, 'day'); // align to Monday
  const end = today.add(4, 'month');
  while (!d.isAfter(end)) {
    out.push({ from: d.format(ISO), to: d.add(4, 'day').format(ISO), hoursPerDay: 'PT8H' });
    d = d.add(7, 'day');
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
    getResourcesGetCapacitiesMockHandler([
      { resourceId: 'r-luca', segments: weekdaySegments() },
      { resourceId: 'r-elena', segments: weekdaySegments() },
    ]),
    // One flat plan-slice read (P3): Luca's blocks; Elena has none, so the
    // client-side pivot leaves her lane empty.
    getAllocationsGetInRangeMockHandler(lucaAllocations),
  );
}
