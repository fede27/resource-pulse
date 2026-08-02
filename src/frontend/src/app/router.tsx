import { createRootRoute, createRoute, createRouter, Outlet } from '@tanstack/react-router';
import { AuthCallbackPage } from '@/auth/AuthCallbackPage';
import { AppLayout } from '@/components/layout/AppLayout';
import { HomePage } from '@/app/routes/index';
import { PeopleRoutePage } from '@/app/routes/people/index';
import { ProjectsRoutePage } from '@/app/routes/projects/index';
import { RolesTeamsRoutePage } from '@/app/routes/roles-teams/index';
import { SettingsRoutePage } from '@/app/routes/settings/index';
import { TimeConfigRoutePage } from '@/app/routes/time-config/index';

// The root is a bare pass-through so the auth routes can sit OUTSIDE the app
// shell: the sign-in callback must not render navigation and page chrome for a
// user who is not signed in yet.
const rootRoute = createRootRoute({ component: Outlet });

// Pathless layout route (id-only): everything under it gets the app shell,
// without adding a segment to any URL.
const shellRoute = createRoute({
  getParentRoute: () => rootRoute,
  id: 'shell',
  component: AppLayout,
});

const indexRoute = createRoute({
  getParentRoute: () => shellRoute,
  path: '/',
  component: HomePage,
});

const timeConfigRoute = createRoute({
  getParentRoute: () => shellRoute,
  path: '/time-config',
  component: TimeConfigRoutePage,
});

// "/people" is the Persone planning board. The former registry (anagrafica) and
// the Teams page were consolidated into "/roles-teams" (Ruoli e team).
const peopleRoute = createRoute({
  getParentRoute: () => shellRoute,
  path: '/people',
  component: PeopleRoutePage,
});

// The consolidated "Ruoli e team" page: anagrafica (ruoli/team/persone) +
// disponibilità base (calendario assegnato + ferie/straordinari).
const rolesTeamsRoute = createRoute({
  getParentRoute: () => shellRoute,
  path: '/roles-teams',
  component: RolesTeamsRoutePage,
});

const projectsRoute = createRoute({
  getParentRoute: () => shellRoute,
  path: '/projects',
  component: ProjectsRoutePage,
});

const settingsRoute = createRoute({
  getParentRoute: () => shellRoute,
  path: '/settings',
  component: SettingsRoutePage,
});

// OIDC redirect target (ADR-0029). The code exchange is performed by the auth
// provider as soon as it mounts on a URL carrying ?code=; this route exists so
// the redirect URI resolves instead of 404-ing.
const authCallbackRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/auth/callback',
  component: AuthCallbackPage,
});

const routeTree = rootRoute.addChildren([
  shellRoute.addChildren([
    indexRoute,
    timeConfigRoute,
    peopleRoute,
    rolesTeamsRoute,
    projectsRoute,
    settingsRoute,
  ]),
  authCallbackRoute,
]);

export const router = createRouter({ routeTree });

declare module '@tanstack/react-router' {
  interface Register {
    router: typeof router;
  }
}
