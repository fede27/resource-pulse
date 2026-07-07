import { PageContainer } from '@/components/layout/PageContainer';
import { PeopleBoardPage } from '@/features/people-board/PeopleBoardPage';

// "/people" is the Persone planning board (people pivot of the coverage
// timeline). The registry (anagrafica) lives at /people/registry.
export function PeopleRoutePage() {
  return (
    <PageContainer>
      <PeopleBoardPage />
    </PageContainer>
  );
}
