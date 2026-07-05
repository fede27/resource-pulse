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
import { getLoadMock } from '@/api/generated/load/load.msw';
import { getLoadBandsMock } from '@/api/generated/load-bands/load-bands.msw';
import { getMeMock } from '@/api/generated/me/me.msw';
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
  ...getAllocationsMock(),
  ...getBucketingMock(),
  ...getBusinessCalendarsMock(),
  ...getCommitmentPolicyMock(),
  ...getCompanyClosuresMock(),
  ...getDemandsMock(),
  ...getLoadMock(),
  ...getLoadBandsMock(),
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
