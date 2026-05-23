import { Button, Popconfirm, Space, Table, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { Link, useNavigate } from '@tanstack/react-router';
import { useQueryClient } from '@tanstack/react-query';
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
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { message } = App.useApp();
  const showApiError = useApiError();

  const { data, isLoading, isFetching } = useTeamsGetAll();
  const deleteMutation = useTeamsDelete({
    mutation: {
      onSuccess: () => {
        message.success('Team eliminato');
        queryClient.invalidateQueries({ queryKey: getTeamsGetAllQueryKey() });
      },
      onError: (e) => showApiError(e),
    },
  });

  const teams = (data?.data ?? []) as TeamReadDto[];

  const columns: ColumnsType<TeamReadDto> = [
    {
      title: 'Nome',
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
      title: 'Stato',
      dataIndex: 'isActive',
      key: 'isActive',
      width: 120,
      filters: [
        { text: 'Attivo', value: true },
        { text: 'Inattivo', value: false },
      ],
      onFilter: (value, record) => record.isActive === value,
      render: (isActive: boolean | undefined) =>
        isActive ? <Tag color="green">Attivo</Tag> : <Tag>Inattivo</Tag>,
    },
    {
      title: 'Azioni',
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
            Modifica
          </Button>
          <Popconfirm
            title="Eliminare il team?"
            description="L'operazione non è reversibile."
            okText="Elimina"
            cancelText="Annulla"
            okButtonProps={{ danger: true }}
            onConfirm={() => deleteMutation.mutate({ id: record.id ?? '' })}
          >
            <Button size="small" danger loading={deleteMutation.isPending}>
              Elimina
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
          Team
        </Title>
        <Button type="primary" onClick={() => navigate({ to: '/teams/new' })}>
          Nuovo Team
        </Button>
      </div>
      <Table<TeamReadDto>
        rowKey={(record) => record.id ?? ''}
        columns={columns}
        dataSource={teams}
        loading={isLoading || isFetching}
        pagination={{ pageSize: 20, showSizeChanger: true }}
        locale={{ emptyText: 'Nessun team' }}
      />
    </div>
  );
}
