import { TeamDetail } from '@/features/teams/TeamDetail';

export function TeamDetailPage({ teamId }: { teamId: string }) {
  return (
    <div style={{ padding: 24, maxWidth: 1440 }}>
      <TeamDetail teamId={teamId} />
    </div>
  );
}
