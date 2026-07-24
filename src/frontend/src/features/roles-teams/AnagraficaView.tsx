import { useMemo, useState } from 'react';
import {
  App,
  Button,
  Dropdown,
  Empty,
  Input,
  Segmented,
} from 'antd';
import {
  MoreOutlined,
  PlusOutlined,
  RightOutlined,
  TeamOutlined,
} from '@ant-design/icons';
import { useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import {
  getResourcesGetAllQueryKey,
  useResourcesCreate,
} from '@/api/generated/resources/resources';
import {
  getRolesGetAllQueryKey,
  useRolesCreate,
  useRolesDelete,
  useRolesUpdate,
} from '@/api/generated/roles/roles';
import {
  getTeamsGetAllQueryKey,
  useTeamsCreate,
  useTeamsDelete,
  useTeamsUpdate,
} from '@/api/generated/teams/teams';
import type { ResourceReadDto } from '@/api/generated/schemas';
import { InitialsAvatar } from '@/components/domain/InitialsAvatar';
import { InlineEditableText } from '@/components/domain/InlineEditableText';
import { InspectorDrawer } from '@/components/domain/InspectorDrawer';
import { useApiError } from '@/lib/errors';
import type { AnagraficaData } from './useAnagraficaData';
import {
  countInCategory,
  crossAxisCount,
  peopleInCategory,
  type Pivot,
} from './rolesTeamsModel';
import { PersonInlineCreate, type PersonCreateValues } from './PersonInlineCreate';
import { PersonInspector } from './PersonInspector';
import { useStyles } from './AnagraficaView.styles';

export type AnagraficaViewProps = {
  data: AnagraficaData;
};

export function AnagraficaView({ data }: AnagraficaViewProps) {
  const { t } = useTranslation();
  const { styles, cx } = useStyles();
  const { message, modal } = App.useApp();
  const queryClient = useQueryClient();
  const showApiError = useApiError();

  const [pivot, setPivot] = useState<Pivot>('role');
  const [pickedCatId, setPickedCatId] = useState<string | null>(null);
  const [addingCat, setAddingCat] = useState(false);
  const [newCatName, setNewCatName] = useState('');
  const [addingPerson, setAddingPerson] = useState(false);
  const [inspectId, setInspectId] = useState<string | null>(null);

  const { people, roles, teams, roleNameById, teamNameById } = data;
  const isRole = pivot === 'role';
  const categories = isRole ? roles : teams;
  const noun = isRole ? t('rolesTeams.roleNoun') : t('rolesTeams.teamNoun');

  const selectedCatId =
    pickedCatId && categories.some((c) => c.id === pickedCatId)
      ? pickedCatId
      : (categories[0]?.id ?? null);
  const selectedCat = categories.find((c) => c.id === selectedCatId) ?? null;

  const catPeople = useMemo(
    () => (selectedCat ? peopleInCategory(people, pivot, selectedCat.id) : []),
    [selectedCat, people, pivot],
  );

  const invalidateResources = () =>
    void queryClient.invalidateQueries({ queryKey: getResourcesGetAllQueryKey() });
  const invalidateCatalog = () =>
    void queryClient.invalidateQueries({
      queryKey: isRole ? getRolesGetAllQueryKey() : getTeamsGetAllQueryKey(),
    });

  // Catalog mutations (both pivots wired; dispatched by `isRole`).
  const roleCreate = useRolesCreate({ mutation: { onError: (e) => showApiError(e) } });
  const teamCreate = useTeamsCreate({ mutation: { onError: (e) => showApiError(e) } });
  const roleUpdate = useRolesUpdate({ mutation: { onError: (e) => showApiError(e) } });
  const teamUpdate = useTeamsUpdate({ mutation: { onError: (e) => showApiError(e) } });
  const roleDelete = useRolesDelete({ mutation: { onError: (e) => showApiError(e) } });
  const teamDelete = useTeamsDelete({ mutation: { onError: (e) => showApiError(e) } });
  const personCreate = useResourcesCreate({ mutation: { onError: (e) => showApiError(e) } });

  const submitNewCat = async () => {
    const name = newCatName.trim();
    if (!name) return;
    try {
      const created = isRole
        ? await roleCreate.mutateAsync({ data: { name } })
        : await teamCreate.mutateAsync({ data: { name } });
      invalidateCatalog();
      message.success(
        isRole ? t('rolesTeams.toast.roleCreated') : t('rolesTeams.toast.teamCreated'),
      );
      setNewCatName('');
      setAddingCat(false);
      if (created?.id) setPickedCatId(created.id);
    } catch {
      // handled by onError
    }
  };

  const renameCat = (id: string, name: string) => {
    const onSuccess = () => {
      invalidateCatalog();
      message.success(
        isRole ? t('rolesTeams.toast.roleRenamed') : t('rolesTeams.toast.teamRenamed'),
      );
    };
    if (isRole) roleUpdate.mutate({ id, data: { name } }, { onSuccess });
    else teamUpdate.mutate({ id, data: { name, isActive: true } }, { onSuccess });
  };

  const deleteCat = () => {
    if (!selectedCat) return;
    const inUse = countInCategory(people, pivot, selectedCat.id);
    if (inUse > 0) {
      modal.info({
        title: t('rolesTeams.delete.blockedRoleTitle', { name: selectedCat.name }),
        content: isRole
          ? t('rolesTeams.delete.blockedBodyRole')
          : t('rolesTeams.delete.blockedBodyTeam'),
        okText: t('common.confirm'),
      });
      return;
    }
    modal.confirm({
      title: isRole
        ? t('rolesTeams.delete.confirmRoleTitle', { name: selectedCat.name })
        : t('rolesTeams.delete.confirmTeamTitle', { name: selectedCat.name }),
      content: (
        <>
          {isRole
            ? t('rolesTeams.delete.confirmBodyRole')
            : t('rolesTeams.delete.confirmBodyTeam')}{' '}
          {t('common.irreversibleAction')}
        </>
      ),
      okText: t('common.delete'),
      okButtonProps: { danger: true },
      cancelText: t('common.cancel'),
      onOk: async () => {
        const onDone = () => {
          invalidateCatalog();
          message.success(
            isRole ? t('rolesTeams.toast.roleDeleted') : t('rolesTeams.toast.teamDeleted'),
          );
          setPickedCatId(null);
        };
        try {
          if (isRole) await roleDelete.mutateAsync({ id: selectedCat.id });
          else await teamDelete.mutateAsync({ id: selectedCat.id });
          onDone();
        } catch {
          // handled by onError
        }
      },
    });
  };

  const createPerson = (values: PersonCreateValues) => {
    personCreate.mutate(
      {
        data: {
          name: values.name,
          email: values.email,
          roleId: values.roleId,
          teamId: values.teamId,
        },
      },
      {
        onSuccess: (created) => {
          message.success(t('rolesTeams.toast.personCreated'));
          invalidateResources();
          setAddingPerson(false);
          const id = (created as ResourceReadDto | undefined)?.id;
          if (id) setInspectId(id);
        },
      },
    );
  };

  const changePivot = (next: Pivot) => {
    setPivot(next);
    setPickedCatId(null);
    setAddingCat(false);
    setNewCatName('');
    setAddingPerson(false);
  };

  const inspectedPerson = inspectId
    ? (people.find((p) => p.id === inspectId) ?? null)
    : null;

  const emptyCatCount = categories.reduce(
    (n, c) => (countInCategory(people, pivot, c.id) === 0 ? n + 1 : n),
    0,
  );

  return (
    <div>
      <div className={styles.statusRow}>
        <Segmented<Pivot>
          value={pivot}
          onChange={changePivot}
          options={[
            { label: t('rolesTeams.pivotRole'), value: 'role' },
            { label: t('rolesTeams.pivotTeam'), value: 'team' },
          ]}
        />
        <span className={styles.statusText}>
          {categories.length}{' '}
          {isRole ? t('rolesTeams.catalogRoles') : t('rolesTeams.catalogTeams')} ·{' '}
          {people.length} {t('rolesTeams.personPlural')}
          {emptyCatCount > 0 && (
            <>
              {' · '}
              <span className={styles.statusWarn}>
                {emptyCatCount} {t('rolesTeams.emptyRolesSuffix')}
              </span>
            </>
          )}
        </span>
      </div>

      <div className={styles.grid}>
        {/* Catalog list */}
        <div className={styles.panel}>
          <div className={styles.panelHead}>
            <div className={styles.panelTitle}>
              {isRole ? t('rolesTeams.catalogRoles') : t('rolesTeams.catalogTeams')}
            </div>
            {!addingCat && (
              <Button
                size="small"
                type="primary"
                icon={<PlusOutlined />}
                onClick={() => setAddingCat(true)}
              >
                {t('rolesTeams.catalogNew')}
              </Button>
            )}
          </div>
          {addingCat && (
            <div className={styles.addRow}>
              <Input
                autoFocus
                value={newCatName}
                onChange={(e) => setNewCatName(e.target.value)}
                onPressEnter={submitNewCat}
                onKeyDown={(e) => {
                  if (e.key === 'Escape') {
                    setAddingCat(false);
                    setNewCatName('');
                  }
                }}
                placeholder={
                  isRole
                    ? t('rolesTeams.newRolePlaceholder')
                    : t('rolesTeams.newTeamPlaceholder')
                }
              />
              <Button size="small" type="primary" onClick={submitNewCat}>
                {t('common.confirm')}
              </Button>
            </div>
          )}
          <div className={styles.list}>
            {categories.map((c) => {
              const n = countInCategory(people, pivot, c.id);
              const active = c.id === selectedCatId;
              return (
                <div
                  key={c.id}
                  className={cx(styles.catItem, active && styles.catItemActive)}
                  onClick={() => setPickedCatId(c.id)}
                >
                  {active && <span className={styles.activeBar} />}
                  <div className={styles.grow}>
                    <div className={styles.catName}>{c.name}</div>
                    <div className={cx(styles.catSub, n === 0 && styles.catSubWarn)}>
                      {n === 0
                        ? t('rolesTeams.noPeople')
                        : `${n} ${n === 1 ? t('rolesTeams.personSingular') : t('rolesTeams.personPlural')}`}
                    </div>
                  </div>
                  <span className={cx(styles.catBadge, active && styles.catBadgeActive)}>
                    {n}
                  </span>
                </div>
              );
            })}
          </div>
        </div>

        {/* Category detail */}
        {selectedCat ? (
          <div className={styles.detailStack}>
            <div className={styles.detailCard}>
              <div className={styles.detailHead}>
                <div className={styles.grow}>
                  <InlineEditableText
                    value={selectedCat.name}
                    fontSize={20}
                    fontWeight={600}
                    onSave={(v) => renameCat(selectedCat.id, v)}
                  />
                  <span className={styles.nounLabel}>{noun}</span>
                </div>
                <Dropdown
                  placement="bottomRight"
                  menu={{
                    items: [
                      {
                        key: 'delete',
                        danger: true,
                        label: t('common.delete'),
                        onClick: deleteCat,
                      },
                    ],
                  }}
                >
                  <Button icon={<MoreOutlined />} />
                </Dropdown>
              </div>
              <div className={styles.metrics}>
                <span>
                  <span className={styles.metricLabel}>{t('rolesTeams.peopleLabel')}</span> ·{' '}
                  <strong>{catPeople.length}</strong>
                </span>
                <span>
                  <span className={styles.metricLabel}>
                    {isRole ? t('rolesTeams.axisTeams') : t('rolesTeams.axisRoles')}
                  </span>{' '}
                  · <strong>{crossAxisCount(catPeople, pivot)}</strong>
                </span>
              </div>
              <div className={styles.rule}>
                {isRole ? t('rolesTeams.descRole') : t('rolesTeams.descTeam')}
              </div>
            </div>

            {/* People in this category */}
            <div className={styles.panel}>
              <div className={styles.peopleHead}>
                <div className={styles.panelTitle}>
                  {isRole ? t('rolesTeams.peopleInRole') : t('rolesTeams.peopleInTeam')}
                </div>
                {!addingPerson && (
                  <Button
                    size="small"
                    icon={<PlusOutlined />}
                    onClick={() => setAddingPerson(true)}
                  >
                    {t('rolesTeams.addPerson')}
                  </Button>
                )}
              </div>
              {addingPerson && (
                <PersonInlineCreate
                  roles={roles}
                  teams={teams}
                  {...(isRole ? { presetRoleId: selectedCat.id } : { presetTeamId: selectedCat.id })}
                  saving={personCreate.isPending}
                  onCancel={() => setAddingPerson(false)}
                  onCreate={createPerson}
                />
              )}
              {catPeople.map((p) => {
                const roleName = p.roleId ? (roleNameById.get(p.roleId) ?? '—') : '—';
                const teamName = p.teamId
                  ? (teamNameById.get(p.teamId) ?? '—')
                  : t('rolesTeams.person.noTeam');
                return (
                  <div
                    key={p.id}
                    className={styles.personRow}
                    onClick={() => setInspectId(p.id ?? null)}
                  >
                    <InitialsAvatar name={p.name ?? '?'} size={32} seed={p.id ?? ''} />
                    <div className={styles.grow}>
                      <div className={styles.personName}>{p.name}</div>
                      <div className={styles.personSub}>{isRole ? teamName : roleName}</div>
                    </div>
                    <span className={styles.personChevron}>
                      <RightOutlined />
                    </span>
                  </div>
                );
              })}
              {catPeople.length === 0 && !addingPerson && (
                <div className={styles.emptyPeople}>
                  {isRole ? t('rolesTeams.emptyRoleHint') : t('rolesTeams.emptyTeamHint')}
                </div>
              )}
            </div>
          </div>
        ) : (
          <div className={cx(styles.panel, styles.emptyPanel)}>
            <Empty
              image={<TeamOutlined className={styles.emptyIcon} />}
              description={
                isRole ? t('rolesTeams.createFirstRole') : t('rolesTeams.createFirstTeam')
              }
            />
          </div>
        )}
      </div>

      <InspectorDrawer
        open={!!inspectedPerson}
        onClose={() => setInspectId(null)}
        title={t('rolesTeams.person.title')}
      >
        {inspectedPerson && (
          <PersonInspector
            person={inspectedPerson}
            roles={roles}
            teams={teams}
            tags={data.tags}
            onDeleted={() => setInspectId(null)}
          />
        )}
      </InspectorDrawer>
    </div>
  );
}
