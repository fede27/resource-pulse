import { useMemo, useState } from 'react';
import { App, Card, Col, Empty, Row, Skeleton, theme } from 'antd';
import { TeamOutlined } from '@ant-design/icons';
import { useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import {
  getResourcesGetAllQueryKey,
  useResourcesCreate,
  useResourcesGetAll,
} from '@/api/generated/resources/resources';
import { useRolesGetAll } from '@/api/generated/roles/roles';
import { useSkillsGetAll } from '@/api/generated/skills/skills';
import {
  SkillApprovalStatus,
  type LoadResult,
  type ResourceReadDto,
  type RoleReadDto,
  type SkillReadDto,
} from '@/api/generated/schemas';
import { PageHeader } from '@/components/domain/PageHeader';
import { StatCard } from '@/components/domain/StatCard';
import { useApiError } from '@/lib/errors';
import { PersonDetail } from './PersonDetail';
import { PersonInlineCreate, type PersonCreateValues } from './PersonInlineCreate';
import { PersonList } from './PersonList';
import { useStyles } from './PeoplePage.styles';

const EMPTY_IMAGE_STYLE = { height: 64, display: 'flex', justifyContent: 'center' } as const;

export function PeoplePage() {
  const { t } = useTranslation();
  const { token } = theme.useToken();
  const { styles } = useStyles();
  const { message } = App.useApp();
  const queryClient = useQueryClient();
  const showApiError = useApiError();

  const { data, isLoading } = useResourcesGetAll();
  const people = useMemo(
    () => ((data as LoadResult | undefined)?.data ?? []) as ResourceReadDto[],
    [data],
  );
  const { data: skillsData } = useSkillsGetAll();
  const skillsCatalogueSize = useMemo(
    () =>
      (((skillsData as LoadResult | undefined)?.data ?? []) as SkillReadDto[])
        .length,
    [skillsData],
  );
  const { data: rolesData } = useRolesGetAll();
  const allRoles = useMemo(
    () =>
      ((rolesData as LoadResult | undefined)?.data ?? []) as RoleReadDto[],
    [rolesData],
  );
  const roleNameById = useMemo(() => {
    const out: Record<string, string> = {};
    allRoles.forEach((r) => {
      if (r.id && r.name) out[r.id] = r.name;
    });
    return out;
  }, [allRoles]);

  const [search, setSearch] = useState('');
  const [creating, setCreating] = useState(false);
  const [pickedId, setPickedId] = useState<string | null>(null);
  const selectedId =
    pickedId && people.some((p) => p.id === pickedId)
      ? pickedId
      : (people[0]?.id ?? null);

  const createMutation = useResourcesCreate({
    mutation: {
      onSuccess: (created, variables) => {
        message.success(
          t('people.createSuccess', {
            name: variables.data?.name ?? '',
          }),
        );
        void queryClient.invalidateQueries({
          queryKey: getResourcesGetAllQueryKey(),
        });
        const newId = (created as ResourceReadDto | undefined)?.id;
        if (newId) setPickedId(newId);
        setCreating(false);
      },
      onError: (e) => showApiError(e),
    },
  });

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return people;
    return people.filter((p) => {
      const role = p.roleId ? roleNameById[p.roleId] ?? '' : '';
      return (
        p.name?.toLowerCase().includes(q) ||
        p.email?.toLowerCase().includes(q) ||
        role.toLowerCase().includes(q)
      );
    });
  }, [people, search, roleNameById]);

  const pendingByPerson = useMemo(() => {
    const out: Record<string, number> = {};
    people.forEach((p) => {
      if (!p.id) return;
      const personSkills = p.skills ?? [];
      out[p.id] = personSkills.filter(
        (s) => s.approvalStatus === SkillApprovalStatus.Pending,
      ).length;
    });
    return out;
  }, [people]);

  const roleNameByPerson = useMemo(() => {
    const out: Record<string, string> = {};
    people.forEach((p) => {
      if (!p.id) return;
      out[p.id] = p.roleId ? roleNameById[p.roleId] ?? '' : '';
    });
    return out;
  }, [people, roleNameById]);

  const totalPending = useMemo(
    () => Object.values(pendingByPerson).reduce((a, b) => a + b, 0),
    [pendingByPerson],
  );

  const selected = selectedId
    ? people.find((p) => p.id === selectedId) ?? null
    : null;

  const handleCreate = (values: PersonCreateValues) => {
    createMutation.mutate({
      data: {
        name: values.name,
        email: values.email ?? null,
        roleId: values.roleId ?? null,
      } as const,
    });
  };

  const roleOptions = useMemo(
    () =>
      allRoles
        .filter(
          (r): r is RoleReadDto & { id: string; name: string } =>
            !!r.id && !!r.name,
        )
        .map((r) => ({ id: r.id, label: r.name })),
    [allRoles],
  );

  if (isLoading) return <Skeleton active />;

  return (
    <div>
      <PageHeader
        title={t('people.sectionTitle')}
        subtitle={t('people.sectionSubtitle')}
      />

      <Row gutter={16} className={styles.statsRow}>
        <Col xs={24} md={8}>
          <StatCard
            label={t('people.statsPeople')}
            value={people.length}
            accentColor={token.colorPrimary}
          />
        </Col>
        <Col xs={24} md={8}>
          <StatCard
            label={t('people.statsCatalog')}
            value={skillsCatalogueSize}
          />
        </Col>
        <Col xs={24} md={8}>
          <StatCard
            label={t('people.statsPending')}
            value={totalPending}
            accentColor={
              totalPending > 0
                ? token.colorWarningText
                : token.colorTextTertiary
            }
          />
        </Col>
      </Row>

      <Row gutter={16} align="top" wrap>
        <Col xs={24} md={9} lg={8} xl={7} className={styles.col}>
          <PersonList
            people={filtered}
            selectedId={selected?.id ?? null}
            onSelect={setPickedId}
            search={search}
            onSearchChange={setSearch}
            onStartCreate={() => setCreating(true)}
            isCreating={creating}
            pendingByPerson={pendingByPerson}
            roleNameByPerson={roleNameByPerson}
            inlineSlot={
              creating ? (
                <PersonInlineCreate
                  saving={createMutation.isPending}
                  onSubmit={handleCreate}
                  onCancel={() => setCreating(false)}
                  roleOptions={roleOptions}
                />
              ) : undefined
            }
          />
        </Col>
        <Col xs={24} md={15} lg={16} xl={17} className={styles.col}>
          {selected ? (
            <PersonDetail person={selected} />
          ) : (
            <Card>
              <Empty
                image={<TeamOutlined className={styles.emptyIcon} />}
                imageStyle={EMPTY_IMAGE_STYLE}
                description={
                  <>
                    <div className={styles.noneTitle}>{t('people.noneSelectedTitle')}</div>
                    <div className={styles.noneDesc}>{t('people.noneSelectedDescription')}</div>
                  </>
                }
              />
            </Card>
          )}
        </Col>
      </Row>
    </div>
  );
}
