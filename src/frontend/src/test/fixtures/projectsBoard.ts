import dayjs from 'dayjs';
import { getAllocationsGetForProjectNodeMockHandler } from '@/api/generated/allocations/allocations.msw';
import {
  getLoadGetProjectNodeDemandCoverageMockHandler,
  getLoadGetResourceLoadProfileMockHandler,
} from '@/api/generated/load/load.msw';
import { getMeGetMockHandler } from '@/api/generated/me/me.msw';
import { getProjectNodesGetSubtreeMockHandler } from '@/api/generated/project-nodes/project-nodes.msw';
import { getProjectsGetActiveInRangeMockHandler } from '@/api/generated/projects/projects.msw';
import { getResourcesGetAllMockHandler } from '@/api/generated/resources/resources.msw';
import { getRolesGetAllMockHandler } from '@/api/generated/roles/roles.msw';
import {
  AllocationStatus,
  DemandProvenance,
  ProjectNodeType,
  type AllocationReadDto,
  type DemandCoverageDto,
  type LoadSegmentDto,
  type ProjectNodeReadDto,
} from '@/api/generated/schemas';
import { server } from '@/test/msw/server';
import { seedConfig } from './settingsConfig';

// One-project board fixture, anchored to the real "today" (the page queries a
// rolling horizon): ACME spans ±2 months around today, Luca covers the Dev
// senior demand, the Grafico demand is uncovered (→ verdict "Scoperto").
const ISO = 'YYYY-MM-DD';
const today = dayjs();
const from = today.subtract(1, 'month').format(ISO);
const to = today.add(2, 'month').format(ISO);
const phaseTo = today.add(2, 'week').format(ISO);

export const acmeRoot: ProjectNodeReadDto = {
  id: 'p-acme',
  nodeType: ProjectNodeType.Project,
  name: 'Portale ACME',
  path: '/p-acme',
  client: 'ACME S.p.A.',
  leadResourceId: 'r-anna',
  leadResourceName: 'Anna Bianchi',
  commitmentLevel: 3,
  isProposed: false,
  plannedStart: from,
  plannedEnd: to,
};

export const acmeSubtree: ProjectNodeReadDto[] = [
  acmeRoot,
  {
    id: 'p-acme-analisi',
    parentId: 'p-acme',
    nodeType: ProjectNodeType.Phase,
    name: 'Analisi',
    path: '/p-acme/p-acme-analisi',
    plannedStart: from,
    plannedEnd: phaseTo,
  },
];

export const acmeDemandCoverage: DemandCoverageDto[] = [
  {
    demandId: 'd-dev',
    projectNodeId: 'p-acme',
    roleId: 'role-dev',
    roleName: 'Dev senior',
    provenance: DemandProvenance.Declared,
    requiredHours: 'PT340H',
    coveredHours: 'PT300H',
    gapHours: 'PT40H',
    ownerResourceId: 'r-anna',
    ownerResourceName: 'Anna Bianchi',
  },
  {
    demandId: 'd-grafico',
    projectNodeId: 'p-acme',
    roleId: 'role-grafico',
    roleName: 'Grafico',
    provenance: DemandProvenance.Declared,
    requiredHours: 'PT60H',
    coveredHours: 'PT0S',
    gapHours: 'PT60H',
    ownerResourceId: null,
    ownerResourceName: null,
  },
];

export const acmeAllocations: AllocationReadDto[] = [
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
    periodStart: from,
    periodEnd: to,
    allocationPercent: 60,
    status: AllocationStatus.Hard,
  },
];

export const lucaProfile: LoadSegmentDto[] = [
  {
    from,
    to,
    percent: 120,
    byProject: [
      { projectNodeId: 'p-acme', projectName: 'Portale ACME', percent: 60 },
      { projectNodeId: 'p-beta', projectName: 'Migrazione BETA', percent: 60 },
    ],
  },
];

export function seedProjectsBoard(overrides?: { projects?: ProjectNodeReadDto[] }): void {
  seedConfig(); // load bands (overload ≥100), fence, bucketing
  server.use(
    getProjectsGetActiveInRangeMockHandler(overrides?.projects ?? [acmeRoot]),
    getProjectNodesGetSubtreeMockHandler(acmeSubtree),
    getLoadGetProjectNodeDemandCoverageMockHandler(acmeDemandCoverage),
    getAllocationsGetForProjectNodeMockHandler(acmeAllocations),
    getLoadGetResourceLoadProfileMockHandler(lucaProfile),
    getMeGetMockHandler({
      isAuthenticated: true,
      sub: 'dev',
      name: 'Elena M.',
      resourceId: 'r-elena',
      isStaffingManager: true,
    }),
    getResourcesGetAllMockHandler({
      data: [
        { id: 'r-luca', name: 'Luca Ferri', roleId: 'role-dev' },
        { id: 'r-anna', name: 'Anna Bianchi', roleId: 'role-pm' },
      ],
    }),
    getRolesGetAllMockHandler({
      data: [
        { id: 'role-dev', name: 'Dev senior' },
        { id: 'role-pm', name: 'Project Manager' },
        { id: 'role-grafico', name: 'Grafico' },
      ],
    }),
  );
}
