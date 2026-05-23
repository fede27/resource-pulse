import { TeamDetail } from '@/features/teams/TeamDetail';

export function TeamDetailPage({ teamId }: { teamId: string }) {
  return <TeamDetail teamId={teamId} />;
}
