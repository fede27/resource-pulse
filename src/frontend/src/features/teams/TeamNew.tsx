import { App, Typography } from 'antd';
import { useNavigate } from '@tanstack/react-router';
import { useQueryClient } from '@tanstack/react-query';
import {
  useTeamsCreate,
  getTeamsGetAllQueryKey,
} from '@/api/generated/teams/teams';
import { useApiError } from '@/lib/errors';
import { TeamForm, type TeamFormValues } from './TeamForm';

const { Title } = Typography;

export function TeamNew() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { message } = App.useApp();
  const showApiError = useApiError();

  const createMutation = useTeamsCreate({
    mutation: {
      onSuccess: () => {
        message.success('Team creato');
        queryClient.invalidateQueries({ queryKey: getTeamsGetAllQueryKey() });
        void navigate({ to: '/teams' });
      },
      onError: (e) => showApiError(e),
    },
  });

  const handleSubmit = (values: TeamFormValues) => {
    createMutation.mutate({ data: { name: values.name } });
  };

  return (
    <div>
      <Title level={2}>Nuovo Team</Title>
      <TeamForm
        mode="create"
        submitting={createMutation.isPending}
        onSubmit={handleSubmit}
        onCancel={() => void navigate({ to: '/teams' })}
      />
    </div>
  );
}
