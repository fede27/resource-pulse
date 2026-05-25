import { createRootRoute, createRoute, createRouter } from '@tanstack/react-router';
import { AppLayout } from '@/components/layout/AppLayout';
import { HomePage } from '@/app/routes/index';
import { TeamListPage } from '@/app/routes/teams/index';
import { TeamNewPage } from '@/app/routes/teams/new';
import { TeamDetailPage } from '@/app/routes/teams/$teamId';
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

const teamNewRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/teams/new',
  component: TeamNewPage,
});

const teamDetailRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/teams/$teamId',
  component: () => {
    const { teamId } = teamDetailRoute.useParams();
    return <TeamDetailPage teamId={teamId} />;
  },
});

const timeConfigRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/time-config',
  component: TimeConfigRoutePage,
});

const routeTree = rootRoute.addChildren([
  indexRoute,
  teamListRoute,
  teamNewRoute,
  teamDetailRoute,
  timeConfigRoute,
]);

export const router = createRouter({ routeTree });

declare module '@tanstack/react-router' {
  interface Register {
    router: typeof router;
  }
}
