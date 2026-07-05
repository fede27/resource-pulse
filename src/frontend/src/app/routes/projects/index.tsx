import { PageContainer } from '@/components/layout/PageContainer';
import { ProjectsPage } from '@/features/projects/ProjectsPage';

export function ProjectsRoutePage() {
  // Full-bleed: the board timeline fills all available horizontal space and
  // scrolls its own axis, so no max-width cap here (same as /teams).
  return (
    <PageContainer fullBleed>
      <ProjectsPage />
    </PageContainer>
  );
}
