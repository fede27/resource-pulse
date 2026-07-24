import { PageContainer } from '@/components/layout/PageContainer';
import { RolesTeamsPage } from '@/features/roles-teams/RolesTeamsPage';

// Thin route boundary for the consolidated "Ruoli e team" page (anagrafica +
// disponibilità base). Wrapped in the shared PageContainer so the page gutter
// and max-width match the other routed pages (Persone / Progetti / Anagrafica).
export function RolesTeamsRoutePage() {
  return (
    <PageContainer>
      <RolesTeamsPage />
    </PageContainer>
  );
}
