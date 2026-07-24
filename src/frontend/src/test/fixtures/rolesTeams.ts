import dayjs from 'dayjs';
import { getBusinessCalendarsGetAllMockHandler } from '@/api/generated/business-calendars/business-calendars.msw';
import { getCompanyClosuresGetAllMockHandler } from '@/api/generated/company-closures/company-closures.msw';
import {
  getResourcesGetAllMockHandler,
  getResourcesGetCapacitiesMockHandler,
} from '@/api/generated/resources/resources.msw';
import { getRolesGetAllMockHandler } from '@/api/generated/roles/roles.msw';
import { getTagsGetAllMockHandler } from '@/api/generated/tags/tags.msw';
import { getTeamsGetAllMockHandler } from '@/api/generated/teams/teams.msw';
import {
  AdjustmentType,
  type CapacitySegmentDto,
  type ResourceReadDto,
} from '@/api/generated/schemas';
import { server } from '@/test/msw/server';

const ISO = 'YYYY-MM-DD';
const today = dayjs();

// Mon–Fri 8h calendar, run-length form anchored around today.
function weekdaySegments(): CapacitySegmentDto[] {
  const out: CapacitySegmentDto[] = [];
  let d = today.subtract(2, 'month');
  while (d.day() !== 1) d = d.add(1, 'day');
  const end = today.add(4, 'month');
  while (!d.isAfter(end)) {
    out.push({ from: d.format(ISO), to: d.add(4, 'day').format(ISO), hoursPerDay: 'PT8H' });
    d = d.add(7, 'day');
  }
  return out;
}

const win = (dayOfWeek: number) => ({
  dayOfWeek,
  startTime: '09:00:00',
  endTime: '17:00:00',
  validFrom: '2020-01-01',
  validTo: null,
});

// Ada (Dev, Team Alpha) with a full-day time-off exception; Bob (Dev, Team
// Beta); Cy (QA, Team Alpha). Role "PM" has no people (empty-category signal).
export const rolesTeamsPeople: ResourceReadDto[] = [
  {
    id: 'r-ada',
    name: 'Ada Lovelace',
    email: 'ada@x.it',
    isActive: true,
    businessCalendarId: 'cal-1',
    roleId: 'role-dev',
    teamId: 't-alpha',
    workWindows: [],
    adjustments: [
      {
        id: 'adj-1',
        type: AdjustmentType.Absence,
        dateFrom: today.add(3, 'day').format(ISO),
        dateTo: today.add(5, 'day').format(ISO),
        hours: null,
        reason: 'Ferie estive',
      },
    ],
    tags: [{ tagId: 'tag-remote' }],
  },
  {
    id: 'r-bob',
    name: 'Bob Bit',
    isActive: true,
    businessCalendarId: 'cal-1',
    roleId: 'role-dev',
    teamId: 't-beta',
    workWindows: [],
    adjustments: [],
  },
  {
    id: 'r-cy',
    name: 'Cy Byte',
    isActive: true,
    businessCalendarId: 'cal-1',
    roleId: 'role-qa',
    teamId: 't-alpha',
    workWindows: [],
    adjustments: [],
  },
];

export function seedRolesTeams(): void {
  server.use(
    getResourcesGetAllMockHandler({ data: rolesTeamsPeople }),
    getRolesGetAllMockHandler({
      data: [
        { id: 'role-dev', name: 'Sviluppatore' },
        { id: 'role-qa', name: 'QA Engineer' },
        { id: 'role-pm', name: 'Project Manager' },
      ],
    }),
    getTeamsGetAllMockHandler({
      data: [
        { id: 't-alpha', name: 'Team Alpha', isActive: true },
        { id: 't-beta', name: 'Team Beta', isActive: true },
      ],
    }),
    getTagsGetAllMockHandler({ data: [{ id: 'tag-remote', name: 'Remote' }] }),
    getBusinessCalendarsGetAllMockHandler({
      data: [
        {
          id: 'cal-1',
          name: 'Standard',
          isDefault: true,
          workWindows: [1, 2, 3, 4, 5].map(win),
        },
      ],
    }),
    getCompanyClosuresGetAllMockHandler({ data: [] }),
    getResourcesGetCapacitiesMockHandler([
      { resourceId: 'r-ada', segments: weekdaySegments() },
      { resourceId: 'r-bob', segments: weekdaySegments() },
      { resourceId: 'r-cy', segments: weekdaySegments() },
    ]),
  );
}
