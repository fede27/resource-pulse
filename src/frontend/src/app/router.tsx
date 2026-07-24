import { createRootRoute, createRoute, createRouter } from '@tanstack/react-router';
import { AppLayout } from '@/components/layout/AppLayout';
import { HomePage } from '@/app/routes/index';
import { PeopleRoutePage } from '@/app/routes/people/index';
import { ProjectsRoutePage } from '@/app/routes/projects/index';
import { RolesTeamsRoutePage } from '@/app/routes/roles-teams/index';
import { SettingsRoutePage } from '@/app/routes/settings/index';
import { TimeConfigRoutePage } from '@/app/routes/time-config/index';

const rootRoute = createRootRoute({ component: AppLayout });

const indexRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/',
  component: HomePage,
});

const timeConfigRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/time-config',
  component: TimeConfigRoutePage,
});

// "/people" is the Persone planning board. The former registry (anagrafica) and
// the Teams page were consolidated into "/roles-teams" (Ruoli e team).
const peopleRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/people',
  component: PeopleRoutePage,
});

// The consolidated "Ruoli e team" page: anagrafica (ruoli/team/persone) +
// disponibilità base (calendario assegnato + ferie/straordinari).
const rolesTeamsRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/roles-teams',
  component: RolesTeamsRoutePage,
});

const projectsRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/projects',
  component: ProjectsRoutePage,
});

const settingsRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/settings',
  component: SettingsRoutePage,
});

const routeTree = rootRoute.addChildren([
  indexRoute,
  timeConfigRoute,
  peopleRoute,
  rolesTeamsRoute,
  projectsRoute,
  settingsRoute,
]);

export const router = createRouter({ routeTree });

declare module '@tanstack/react-router' {
  interface Register {
    router: typeof router;
  }
}
