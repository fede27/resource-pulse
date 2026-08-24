import { createRootRoute, createRoute, createRouter, Outlet } from '@tanstack/react-router';
import { AuthCallbackPage } from '@/auth/AuthCallbackPage';
import { LOGIN_PATH } from '@/auth/routes';
import { LoginHelpPage } from '@/features/auth/LoginHelpPage';
import { LoginPage } from '@/features/auth/LoginPage';
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

// "/people" is the People planning board. The former registry and the Teams
// page were consolidated into "/roles-teams".
const peopleRoute = createRoute({
  getParentRoute: () => shellRoute,
  path: '/people',
  component: PeopleRoutePage,
});

// The consolidated Roles & teams page: registry (roles/teams/people) plus
// base availability (assigned calendar + absences/overtime).
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

// Our own sign-in screen (ADR-0031). Like the callback it hangs off the ROOT, not
// the shell: its caller has no token, so there is no navigation to render and no
// tenant to render it for.
const loginRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: LOGIN_PATH,
  component: LoginPage,
});

// Honest dead end for "forgot your password?" until the reset flow ships.
const loginHelpRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: `${LOGIN_PATH}/help`,
  component: LoginHelpPage,
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
  loginRoute,
  loginHelpRoute,
]);

export const router = createRouter({ routeTree });

declare module '@tanstack/react-router' {
  interface Register {
    router: typeof router;
  }
}
