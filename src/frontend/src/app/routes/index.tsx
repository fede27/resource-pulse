import { DashboardPage } from '@/features/dashboard/DashboardPage';

// "/" is the triage surface (ADR-0032): what needs attention today and what
// changed — deliberately not a reporting hub.
export function HomePage() {
  return <DashboardPage />;
}
