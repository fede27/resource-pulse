import { Alert, App, Skeleton, Typography } from 'antd';
import { useNavigate } from '@tanstack/react-router';
import { useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import {
  useTeamsGetById,
  useTeamsUpdate,
  getTeamsGetAllQueryKey,
  getTeamsGetByIdQueryKey,
} from '@/api/generated/teams/teams';
import { useApiError } from '@/lib/errors';
import { TeamForm, type TeamFormValues } from './TeamForm';

const { Title } = Typography;

type TeamDetailProps = { teamId: string };

export function TeamDetail({ teamId }: TeamDetailProps) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { message } = App.useApp();
  const showApiError = useApiError();

  const { data: team, isLoading, isError } = useTeamsGetById(teamId);

  const updateMutation = useTeamsUpdate({
    mutation: {
      onSuccess: () => {
        message.success(t('teams.updateSuccess'));
        queryClient.invalidateQueries({ queryKey: getTeamsGetAllQueryKey() });
        queryClient.invalidateQueries({ queryKey: getTeamsGetByIdQueryKey(teamId) });
        void navigate({ to: '/teams' });
      },
      onError: (e) => showApiError(e),
    },
  });

  if (isLoading) return <Skeleton active />;

  if (isError || !team) {
    return (
      <Alert
        type="error"
        message={t('teams.notFoundTitle')}
        description={t('teams.notFoundDescription')}
        showIcon
      />
    );
  }

  const handleSubmit = (values: TeamFormValues) => {
    updateMutation.mutate({
      id: teamId,
      data: { name: values.name, isActive: values.isActive },
    });
  };

  return (
    <div>
      <Title level={2}>{t('teams.editTitle')}</Title>
      <TeamForm
        mode="edit"
        initialValues={{
          name: team.name ?? '',
          isActive: team.isActive ?? false,
        }}
        submitting={updateMutation.isPending}
        onSubmit={handleSubmit}
        onCancel={() => void navigate({ to: '/teams' })}
      />
    </div>
  );
}
