import { Button, Popconfirm, Space, Table, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { Link, useNavigate } from '@tanstack/react-router';
import { useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import {
  useTeamsGetAll,
  useTeamsDelete,
  getTeamsGetAllQueryKey,
} from '@/api/generated/teams/teams';
import type { TeamReadDto } from '@/api/generated/schemas';
import { useApiError } from '@/lib/errors';
import { App } from 'antd';

const { Title } = Typography;

export function TeamList() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { message } = App.useApp();
  const showApiError = useApiError();

  const { data, isLoading, isFetching } = useTeamsGetAll();
  const deleteMutation = useTeamsDelete({
    mutation: {
      onSuccess: () => {
        message.success(t('teams.deleteSuccess'));
        queryClient.invalidateQueries({ queryKey: getTeamsGetAllQueryKey() });
      },
      onError: (e) => showApiError(e),
    },
  });

  const teams = (data?.data ?? []) as TeamReadDto[];

  const columns: ColumnsType<TeamReadDto> = [
    {
      title: t('common.name'),
      dataIndex: 'name',
      key: 'name',
      sorter: (a, b) => (a.name ?? '').localeCompare(b.name ?? ''),
      render: (name: string | null | undefined, record) => (
        <Link to="/teams/$teamId" params={{ teamId: record.id ?? '' }}>
          {name ?? '—'}
        </Link>
      ),
    },
    {
      title: t('common.state'),
      dataIndex: 'isActive',
      key: 'isActive',
      width: 120,
      filters: [
        { text: t('teams.active'), value: true },
        { text: t('teams.inactive'), value: false },
      ],
      onFilter: (value, record) => record.isActive === value,
      render: (isActive: boolean | undefined) =>
        isActive ? (
          <Tag color="green">{t('teams.active')}</Tag>
        ) : (
          <Tag>{t('teams.inactive')}</Tag>
        ),
    },
    {
      title: t('common.actions'),
      key: 'actions',
      width: 200,
      render: (_, record) => (
        <Space>
          <Button
            size="small"
            onClick={() =>
              navigate({ to: '/teams/$teamId', params: { teamId: record.id ?? '' } })
            }
          >
            {t('common.edit')}
          </Button>
          <Popconfirm
            title={t('teams.deletePrompt')}
            description={t('common.irreversibleAction')}
            okText={t('common.delete')}
            cancelText={t('common.cancel')}
            okButtonProps={{ danger: true }}
            onConfirm={() => deleteMutation.mutate({ id: record.id ?? '' })}
          >
            <Button size="small" danger loading={deleteMutation.isPending}>
              {t('common.delete')}
            </Button>
          </Popconfirm>
        </Space>
      ),
    },
  ];

  return (
    <div>
      <div
        style={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          marginBottom: 16,
        }}
      >
        <Title level={2} style={{ margin: 0 }}>
          {t('teams.listTitle')}
        </Title>
        <Button type="primary" onClick={() => navigate({ to: '/teams/new' })}>
          {t('teams.newTitle')}
        </Button>
      </div>
      <Table<TeamReadDto>
        rowKey={(record) => record.id ?? ''}
        columns={columns}
        dataSource={teams}
        loading={isLoading || isFetching}
        pagination={{ pageSize: 20, showSizeChanger: true }}
        locale={{ emptyText: t('teams.emptyText') }}
      />
    </div>
  );
}
