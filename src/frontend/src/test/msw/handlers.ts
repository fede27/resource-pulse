// Baseline MSW request handlers — sourced ENTIRELY from orval-generated mocks
// (`mock: true` in orval.config.ts). Never hand-write mock JSON here: each
// `getXxxMock()` returns the handler array for one backend tag, generated from
// the OpenAPI schema. Individual tests narrow specific endpoints to deterministic
// payloads via `server.use(getXxxGetAllMockHandler({ ...fixed }))`.

import type { RequestHandler } from 'msw';

import { getAllocationsMock } from '@/api/generated/allocations/allocations.msw';
import { getBucketingMock } from '@/api/generated/bucketing/bucketing.msw';
import { getBusinessCalendarsMock } from '@/api/generated/business-calendars/business-calendars.msw';
import { getCommitmentPolicyMock } from '@/api/generated/commitment-policy/commitment-policy.msw';
import { getCompanyClosuresMock } from '@/api/generated/company-closures/company-closures.msw';
import { getDemandsMock } from '@/api/generated/demands/demands.msw';
import { getDevAccessMock } from '@/api/generated/dev-access/dev-access.msw';
import { getLoadMock } from '@/api/generated/load/load.msw';
import { getLoadBandsMock } from '@/api/generated/load-bands/load-bands.msw';
import { getLoginMock } from '@/api/generated/login/login.msw';
import { getMeGetMockHandler, getMeMock } from '@/api/generated/me/me.msw';
import { AppRole } from '@/api/generated/schemas/appRole';
import { getPlanCommandsMock } from '@/api/generated/plan-commands/plan-commands.msw';
import { getProjectNodesMock } from '@/api/generated/project-nodes/project-nodes.msw';
import { getProjectsMock } from '@/api/generated/projects/projects.msw';
import { getResourcesMock } from '@/api/generated/resources/resources.msw';
import { getRolesMock } from '@/api/generated/roles/roles.msw';
import { getSkillsMock } from '@/api/generated/skills/skills.msw';
import { getTagsMock } from '@/api/generated/tags/tags.msw';
import { getTeamsMock } from '@/api/generated/teams/teams.msw';
import { getTimeFenceMock } from '@/api/generated/time-fence/time-fence.msw';

export const handlers: RequestHandler[] = [
  // FIRST, deliberately: MSW resolves with the first matching handler, and this
  // one has to win over the generated /api/me mock below.
  //
  // That mock randomizes every field, including `isMember` and `accessRole` — and
  // since ADR-0030 those two decide whether a write gesture renders at all. Left
  // random, any test touching a gated surface would pass or fail by coin flip.
  // The baseline is therefore an owner; a test that cares about a lesser role
  // narrows it with server.use(getMeGetMockHandler({ ... })).
  getMeGetMockHandler({
    isAuthenticated: true,
    sub: 'test-user',
    email: 'test@resourcepulse.local',
    name: 'Test User',
    isMember: true,
    accessRole: AppRole.Owner,
  }),
  ...getAllocationsMock(),
  ...getBucketingMock(),
  ...getBusinessCalendarsMock(),
  ...getCommitmentPolicyMock(),
  ...getCompanyClosuresMock(),
  ...getDemandsMock(),
  ...getDevAccessMock(),
  ...getLoadMock(),
  ...getLoadBandsMock(),
  ...getLoginMock(),
  ...getMeMock(),
  ...getPlanCommandsMock(),
  ...getProjectNodesMock(),
  ...getProjectsMock(),
  ...getResourcesMock(),
  ...getRolesMock(),
  ...getSkillsMock(),
  ...getTagsMock(),
  ...getTeamsMock(),
  ...getTimeFenceMock(),
];
