import { createRootRoute, createRoute, createRouter } from '@tanstack/react-router';
import { AppLayout } from '@/components/layout/AppLayout';
import { HomePage } from '@/app/routes/index';
import { PeopleRoutePage } from '@/app/routes/people/index';
import { PeopleRegistryRoutePage } from '@/app/routes/people/registry';
import { ProjectsRoutePage } from '@/app/routes/projects/index';
import { SettingsRoutePage } from '@/app/routes/settings/index';
import { TeamListPage } from '@/app/routes/teams/index';
import { TimeConfigRoutePage } from '@/app/routes/time-config/index';

const rootRoute = createRootRoute({ component: AppLayout });

const indexRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/',
  component: HomePage,
});

const teamListRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/teams',
  component: TeamListPage,
});

const timeConfigRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/time-config',
  component: TimeConfigRoutePage,
});

// "/people" is the Persone planning board; the registry (anagrafica) moved to
// "/people/registry" when the board took over the primary route.
const peopleRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/people',
  component: PeopleRoutePage,
});

const peopleRegistryRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/people/registry',
  component: PeopleRegistryRoutePage,
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
  teamListRoute,
  timeConfigRoute,
  peopleRoute,
  peopleRegistryRoute,
  projectsRoute,
  settingsRoute,
]);

export const router = createRouter({ routeTree });

declare module '@tanstack/react-router' {
  interface Register {
    router: typeof router;
  }
}
