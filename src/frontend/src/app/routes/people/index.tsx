import { PageContainer } from '@/components/layout/PageContainer';
import { PeopleBoardPage } from '@/features/people-board/PeopleBoardPage';

// "/people" is the People planning board (supply-side pivot of the coverage
// timeline). The registry lives at /people/registry.
export function PeopleRoutePage() {
  return (
    <PageContainer>
      <PeopleBoardPage />
    </PageContainer>
  );
}
