import { useMemo, useState } from 'react';
import { App, Alert, Button, Col, Row, Segmented, Skeleton, Space, Spin, Switch } from 'antd';
import { AimOutlined } from '@ant-design/icons';
import { useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import {
  getTeamsGetAllQueryKey,
  useTeamsCreate,
  useTeamsDelete,
  useTeamsUpdate,
} from '@/api/generated/teams/teams';
import {
  getResourcesGetAllQueryKey,
  useResourcesAssignTeam,
} from '@/api/generated/resources/resources';
import type { TeamReadDto } from '@/api/generated/schemas';
import { PageHeader } from '@/components/domain/PageHeader';
import { StatCard } from '@/components/domain/StatCard';
import { useApiError } from '@/lib/errors';
import { useTeamGrid } from './useTeamGrid';
import { HeatGrid } from './HeatGrid';
import { TeamCreateInline } from './TeamCreateInline';
import { bandStop, EMPTY_LOAD, legendStops, loadColor, overloadFloor } from './loadModel';

export function TeamsPage() {
  const { t } = useTranslation();
  const { message } = App.useApp();
  const queryClient = useQueryClient();
  const showApiError = useApiError();
  const grid = useTeamGrid();

  const [expandedState, setExpandedState] = useState<Set<string> | null>(null);
  const [showValues, setShowValues] = useState(false);
  const [assigningId, setAssigningId] = useState<string | null>(null);
  const [savingTeamId, setSavingTeamId] = useState<string | null>(null);

  const allIds = useMemo(
    () => grid.teams.map((tm) => tm.id).filter(Boolean) as string[],
    [grid.teams],
  );
  const expanded = expandedState ?? new Set(allIds);
  const toggleExpand = (id: string) =>
    setExpandedState((prev) => {
      const base = new Set(prev ?? allIds);
      if (base.has(id)) base.delete(id);
      else base.add(id);
      return base;
    });

  const invalidateMembership = () => {
    void queryClient.invalidateQueries({ queryKey: getResourcesGetAllQueryKey() });
  };
  const invalidateTeams = () => {
    void queryClient.invalidateQueries({ queryKey: getTeamsGetAllQueryKey() });
  };

  const assignMutation = useResourcesAssignTeam({
    mutation: {
      onSuccess: () => invalidateMembership(),
      onError: (e) => showApiError(e),
    },
  });
  const createMutation = useTeamsCreate({
    mutation: {
      onSuccess: (created) => {
        message.success(t('teams.createSuccess'));
        invalidateTeams();
        const id = (created as TeamReadDto | undefined)?.id;
        if (id) setExpandedState(new Set([...(expandedState ?? allIds), id]));
      },
      onError: (e) => showApiError(e),
    },
  });
  const updateMutation = useTeamsUpdate({
    mutation: {
      onSuccess: () => {
        message.success(t('teams.updateSuccess'));
        invalidateTeams();
      },
      onError: (e) => showApiError(e),
    },
  });
  const deleteMutation = useTeamsDelete({
    mutation: {
      onSuccess: () => {
        message.success(t('teams.deleteSuccess'));
        invalidateTeams();
        invalidateMembership();
      },
      onError: (e) => showApiError(e),
    },
  });

  const handleAssign = (resourceId: string, teamId: string | null) => {
    setAssigningId(resourceId);
    assignMutation.mutate(
      { id: resourceId, data: { teamId } },
      { onSettled: () => setAssigningId(null) },
    );
  };
  const handleRename = (team: TeamReadDto, name: string) => {
    if (!team.id) return;
    setSavingTeamId(team.id);
    updateMutation.mutate(
      { id: team.id, data: { name, isActive: team.isActive ?? true } },
      { onSettled: () => setSavingTeamId(null) },
    );
  };
  const handleToggleActive = (team: TeamReadDto, isActive: boolean) => {
    if (!team.id) return;
    setSavingTeamId(team.id);
    updateMutation.mutate(
      { id: team.id, data: { name: team.name ?? '', isActive } },
      { onSettled: () => setSavingTeamId(null) },
    );
  };
  const handleDelete = (team: TeamReadDto) => {
    if (!team.id) return;
    deleteMutation.mutate({ id: team.id });
  };

  // ── Stats (current period, scroll-independent) ──
  const allocatedPeople = useMemo(() => {
    const set = new Set<string>();
    Object.values(grid.membersByTeam).forEach((ids) => ids.forEach((id) => set.add(id)));
    return set;
  }, [grid.membersByTeam]);
  const overallNow = grid.nowOverall;
  const overloadThreshold = useMemo(() => overloadFloor(grid.bands), [grid.bands]);
  const overloadedNow = useMemo(
    () =>
      [...allocatedPeople].filter(
        (id) => (grid.nowByPerson[id] ?? EMPTY_LOAD).pct >= overloadThreshold,
      ).length,
    [allocatedPeople, grid.nowByPerson, overloadThreshold],
  );
  const overallColor = loadColor(overallNow.pct, grid.bands);

  const grainOptions = useMemo(() => {
    const order: Array<'day' | 'week' | 'month'> = [grid.primaryGrain, grid.secondaryGrain];
    return [...new Set(order)].map((g) => ({ value: g, label: t(`teams.grain.${g}`) }));
  }, [grid.primaryGrain, grid.secondaryGrain, t]);

  const bands = useMemo(() => legendStops(grid.bands), [grid.bands]);

  if (grid.isLoading) return <Skeleton active paragraph={{ rows: 8 }} />;

  return (
    <div>
      <PageHeader title={t('teams.sectionTitle')} subtitle={t('teams.sectionSubtitle')} />

      <Row gutter={16} style={{ marginBottom: 16 }}>
        <Col xs={12} md={6}>
          <StatCard label={t('teams.stats.teams')} value={grid.teams.length} accentColor="#1677ff" />
        </Col>
        <Col xs={12} md={6}>
          <StatCard
            label={t('teams.stats.allocated')}
            value={allocatedPeople.size}
            suffix={t('teams.stats.allocatedSuffix', { total: grid.allResources.length })}
          />
        </Col>
        <Col xs={12} md={6}>
          <StatCard
            label={t('teams.stats.avgLoad')}
            value={overallNow.empty ? '—' : `${Math.round(overallNow.pct)}%`}
            accentColor={overallNow.empty ? 'rgba(0,0,0,.45)' : overallColor.solid}
          />
        </Col>
        <Col xs={12} md={6}>
          <StatCard
            label={t('teams.stats.overloaded')}
            value={overloadedNow}
            accentColor={overloadedNow ? '#cf1322' : 'rgba(0,0,0,.45)'}
          />
        </Col>
      </Row>

      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          gap: 16,
          marginBottom: 12,
          flexWrap: 'wrap',
        }}
      >
        <Space size={8} wrap>
          {grainOptions.length > 1 && (
            <Segmented
              size="small"
              value={grid.grain}
              options={grainOptions}
              onChange={(v) => grid.setGrain(v as 'day' | 'week' | 'month')}
            />
          )}
          <Button
            size="small"
            icon={<AimOutlined />}
            onClick={() => grid.viewport.scrollToIndex(0)}
          >
            {t('teams.grid.goToToday')}
          </Button>
          <Button size="small" onClick={() => setExpandedState(new Set(allIds))}>
            {t('teams.grid.expandAll')}
          </Button>
          <Button size="small" onClick={() => setExpandedState(new Set())}>
            {t('teams.grid.collapseAll')}
          </Button>
          <Space size={6} style={{ marginLeft: 4 }}>
            <Switch size="small" checked={showValues} onChange={setShowValues} />
            <span style={{ fontSize: 12, color: 'rgba(0,0,0,.5)' }}>{t('teams.grid.showValues')}</span>
          </Space>
          {grid.isSeriesFetching && <Spin size="small" />}
        </Space>

        <div style={{ display: 'flex', alignItems: 'center', gap: 12, flexWrap: 'wrap' }}>
          <span style={{ fontSize: 12, color: 'rgba(0,0,0,.45)' }}>{t('teams.grid.legendTitle')}</span>
          <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
            {bands.map((b, i) => (
              <span key={`${b.label}-${i}`} style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}>
                <span
                  style={{
                    width: 12,
                    height: 12,
                    borderRadius: 3,
                    background: bandStop(i, bands.length).solid,
                    flexShrink: 0,
                  }}
                />
                <span style={{ fontSize: 12, fontWeight: 500, color: 'rgba(0,0,0,.7)' }}>{b.label}</span>
                <span style={{ fontSize: 11, color: 'rgba(0,0,0,.4)', fontVariantNumeric: 'tabular-nums' }}>
                  {b.range}
                </span>
              </span>
            ))}
          </div>
        </div>
      </div>

      {grid.teams.length === 0 ? (
        <Alert
          type="info"
          showIcon
          message={t('teams.emptyTitle')}
          description={
            <Space direction="vertical" size={8} style={{ marginTop: 4 }}>
              <span>{t('teams.emptyHint')}</span>
              <TeamCreateInline onCreate={(name) => createMutation.mutate({ data: { name } })} saving={createMutation.isPending} />
            </Space>
          }
        />
      ) : (
        <HeatGrid
          grid={grid}
          expanded={expanded}
          onToggleExpand={toggleExpand}
          showValues={showValues}
          onAssign={handleAssign}
          assigningId={assigningId}
          onRenameTeam={handleRename}
          onToggleTeamActive={handleToggleActive}
          onDeleteTeam={handleDelete}
          savingTeamId={savingTeamId}
          createRow={
            <TeamCreateInline
              onCreate={(name) => createMutation.mutate({ data: { name } })}
              saving={createMutation.isPending}
            />
          }
        />
      )}

      <div
        style={{
          marginTop: 10,
          fontSize: 12,
          color: 'rgba(0,0,0,.4)',
          display: 'flex',
          alignItems: 'center',
          gap: 6,
        }}
      >
        <span style={{ width: 3, height: 3, borderRadius: '50%', background: 'rgba(0,0,0,.35)', display: 'inline-block' }} />
        {t('teams.grid.footnote')}
      </div>
    </div>
  );
}
