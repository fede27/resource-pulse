import { PageContainer } from '@/components/layout/PageContainer';
import { RolesTeamsPage } from '@/features/roles-teams/RolesTeamsPage';

// Thin route boundary for the consolidated Roles & teams page (registry plus
// base availability). Wrapped in the shared PageContainer so the page gutter
// and max-width match the other routed pages.
export function RolesTeamsRoutePage() {
  return (
    <PageContainer>
      <RolesTeamsPage />
    </PageContainer>
  );
}
