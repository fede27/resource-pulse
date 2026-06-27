import { PageContainer } from '@/components/layout/PageContainer';
import { TeamsPage } from '@/features/teams/TeamsPage';

export function TeamListPage() {
  // Full-bleed: the load heatmap fills all available horizontal space and
  // scrolls its own timeline, so no max-width cap here.
  return (
    <PageContainer fullBleed>
      <TeamsPage />
    </PageContainer>
  );
}
