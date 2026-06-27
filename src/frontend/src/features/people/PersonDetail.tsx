import { useMemo, useState } from 'react';
import {
  Alert,
  App,
  Button,
  Card,
  Divider,
  Dropdown,
  Input,
  Select,
  Space,
  Tag,
  theme,
  Typography,
} from 'antd';
import {
  ExclamationCircleOutlined,
  MoreOutlined,
  PlusOutlined,
  WarningFilled,
} from '@ant-design/icons';
import { useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import {
  SkillApprovalStatus,
  SkillLevel,
  type LoadResult,
  type ResourceReadDto,
  type RoleReadDto,
  type SkillReadDto,
  type TagReadDto,
} from '@/api/generated/schemas';
import {
  getResourcesGetAllQueryKey,
  getResourcesGetByIdQueryKey,
  useResourcesAddSkill,
  useResourcesAddTag,
  useResourcesAssignRole,
  useResourcesDelete,
  useResourcesRemoveSkill,
  useResourcesRemoveTag,
  useResourcesUpdate,
  useResourcesUpdateSkillLevel,
} from '@/api/generated/resources/resources';
import {
  getRolesGetAllQueryKey,
  useRolesCreate,
  useRolesGetAll,
} from '@/api/generated/roles/roles';
import {
  getSkillsGetAllQueryKey,
  useSkillsCreate,
  useSkillsGetAll,
} from '@/api/generated/skills/skills';
import {
  getTagsGetAllQueryKey,
  useTagsCreate,
  useTagsGetAll,
} from '@/api/generated/tags/tags';
import { InitialsAvatar } from '@/components/domain/InitialsAvatar';
import { InlineEditableText } from '@/components/domain/InlineEditableText';
import {
  SuggestCombobox,
  type SuggestComboboxOption,
} from '@/components/domain/SuggestCombobox';
import { useApiError } from '@/lib/errors';
import { PersonSkillRow } from './PersonSkillRow';
import { useStyles } from './PersonDetail.styles';

const { Text, Title } = Typography;

const EMAIL_REGEX = /^[^@\s]+@[^@\s]+\.[^@\s]+$/;

export type PersonDetailProps = {
  person: ResourceReadDto;
};

export function PersonDetail({ person }: PersonDetailProps) {
  const { t } = useTranslation();
  // token is still read directly for the two icon-colour props handed to
  // imperative/prop slots (modal.confirm icon, Alert icon) that can't take a
  // className.
  const { token } = theme.useToken();
  const { styles } = useStyles();
  const { message, modal } = App.useApp();
  const queryClient = useQueryClient();
  const showApiError = useApiError();

  const personId = person.id ?? '';
  const personName = person.name ?? '';

  // Centralized pools (skills + tags + roles catalogues live on the server).
  const { data: tagsData } = useTagsGetAll();
  const { data: skillsData } = useSkillsGetAll();
  const { data: rolesData } = useRolesGetAll();
  const allTags = useMemo(
    () =>
      ((tagsData as LoadResult | undefined)?.data ?? []) as TagReadDto[],
    [tagsData],
  );
  const allSkills = useMemo(
    () =>
      ((skillsData as LoadResult | undefined)?.data ?? []) as SkillReadDto[],
    [skillsData],
  );
  const allRoles = useMemo(
    () =>
      ((rolesData as LoadResult | undefined)?.data ?? []) as RoleReadDto[],
    [rolesData],
  );

  const invalidatePerson = () => {
    void queryClient.invalidateQueries({
      queryKey: getResourcesGetByIdQueryKey(personId),
    });
    void queryClient.invalidateQueries({
      queryKey: getResourcesGetAllQueryKey(),
    });
  };

  const updateMutation = useResourcesUpdate({
    mutation: {
      onSuccess: () => {
        message.success(t('people.updateSuccess'));
        invalidatePerson();
      },
      onError: (e) => showApiError(e),
    },
  });

  const assignRoleMutation = useResourcesAssignRole({
    mutation: {
      onSuccess: (_, vars) => {
        message.success(
          vars.data?.roleId
            ? t('people.roleAssignSuccess')
            : t('people.roleClearedSuccess'),
        );
        invalidatePerson();
      },
      onError: (e) => showApiError(e),
    },
  });

  const deleteMutation = useResourcesDelete({
    mutation: {
      onSuccess: () => {
        message.success(t('people.deleteSuccess', { name: personName }));
        void queryClient.invalidateQueries({
          queryKey: getResourcesGetAllQueryKey(),
        });
      },
      onError: (e) => showApiError(e),
    },
  });

  const tagCreateMutation = useTagsCreate({
    mutation: { onError: (e) => showApiError(e) },
  });
  const skillCreateMutation = useSkillsCreate({
    mutation: { onError: (e) => showApiError(e) },
  });
  const roleCreateMutation = useRolesCreate({
    mutation: { onError: (e) => showApiError(e) },
  });

  const addTagMutation = useResourcesAddTag({
    mutation: {
      onSuccess: () => {
        message.success(t('people.tags.addedSuccess'));
        invalidatePerson();
        void queryClient.invalidateQueries({
          queryKey: getTagsGetAllQueryKey(),
        });
      },
      onError: (e) => showApiError(e),
    },
  });
  const removeTagMutation = useResourcesRemoveTag({
    mutation: {
      onSuccess: () => {
        message.success(t('people.tags.removedSuccess'));
        invalidatePerson();
      },
      onError: (e) => showApiError(e),
    },
  });
  const addSkillMutation = useResourcesAddSkill({
    mutation: {
      onSuccess: () => {
        message.success(t('people.skills.addedSuccess'));
        invalidatePerson();
        void queryClient.invalidateQueries({
          queryKey: getSkillsGetAllQueryKey(),
        });
      },
      onError: (e) => showApiError(e),
    },
  });
  const removeSkillMutation = useResourcesRemoveSkill({
    mutation: {
      onSuccess: () => {
        message.success(t('people.skills.removedSuccess'));
        invalidatePerson();
      },
      onError: (e) => showApiError(e),
    },
  });
  const updateSkillLevelMutation = useResourcesUpdateSkillLevel({
    mutation: {
      onSuccess: () => {
        message.success(t('people.skills.levelUpdatedSuccess'));
        invalidatePerson();
      },
      onError: (e) => showApiError(e),
    },
  });

  const handleUpdateField = (patch: {
    name?: string;
    email?: string | null;
    isActive?: boolean;
  }) => {
    const nextEmail: string | null =
      patch.email !== undefined ? patch.email : (person.email ?? null);
    updateMutation.mutate({
      id: personId,
      data: {
        name: patch.name ?? personName,
        email: nextEmail,
        isActive: patch.isActive ?? (person.isActive ?? true),
        businessCalendarId: person.businessCalendarId ?? '',
        roleId: person.roleId ?? null,
      },
    });
  };

  const handleRename = (name: string) => handleUpdateField({ name });

  const handleEmailSave = (raw: string) => {
    const trimmed = raw.trim();
    handleUpdateField({ email: trimmed.length === 0 ? null : trimmed });
  };

  const handleToggleActive = () => {
    const nextActive = !(person.isActive ?? true);
    updateMutation.mutate(
      {
        id: personId,
        data: {
          name: personName,
          email: person.email ?? null,
          isActive: nextActive,
          businessCalendarId: person.businessCalendarId ?? '',
          roleId: person.roleId ?? null,
        },
      },
      {
        onSuccess: () => {
          message.success(
            nextActive
              ? t('people.activateSuccess')
              : t('people.deactivateSuccess'),
          );
        },
      },
    );
  };

  const handleRoleChange = (nextRoleId: string | null) => {
    assignRoleMutation.mutate({
      id: personId,
      data: { roleId: nextRoleId },
    });
  };

  const handleCreateRoleThenAssign = async (raw: string) => {
    try {
      const created = await roleCreateMutation.mutateAsync({
        data: { name: raw },
      });
      void queryClient.invalidateQueries({
        queryKey: getRolesGetAllQueryKey(),
      });
      if (created?.id) handleRoleChange(created.id);
    } catch {
      // useApiError handles user feedback.
    }
  };

  const confirmDelete = () => {
    modal.confirm({
      title: t('people.deletePromptTitle', { name: personName }),
      content: (
        <>
          {t('people.deletePromptBody')} {t('common.irreversibleAction')}
        </>
      ),
      icon: <ExclamationCircleOutlined style={{ color: token.colorError }} />,
      okText: t('common.delete'),
      okButtonProps: { danger: true },
      cancelText: t('common.cancel'),
      onOk: () =>
        deleteMutation.mutateAsync({ id: personId }).catch(() => undefined),
    });
  };

  // Role -----------------------------------------------------------------
  const currentRole = useMemo(
    () => (person.roleId ? allRoles.find((r) => r.id === person.roleId) : null),
    [person.roleId, allRoles],
  );

  // Tags -----------------------------------------------------------------
  const tagPool: SuggestComboboxOption[] = useMemo(
    () =>
      allTags
        .filter((tag): tag is TagReadDto & { id: string; name: string } =>
          !!tag.id && !!tag.name,
        )
        .map((tag) => ({ id: tag.id, label: tag.name })),
    [allTags],
  );

  const attachedTags = useMemo(() => {
    const personTags = person.tags ?? [];
    return personTags
      .map((rt) => {
        const meta = allTags.find((tag) => tag.id === rt.tagId);
        return meta?.id && meta.name
          ? { id: meta.id, name: meta.name }
          : null;
      })
      .filter((x): x is { id: string; name: string } => x !== null)
      .sort((a, b) => a.name.localeCompare(b.name));
  }, [person.tags, allTags]);

  const excludedTagLabels = attachedTags.map((tag) => tag.name);

  const handleAttachTag = (opt: SuggestComboboxOption) => {
    addTagMutation.mutate({ id: personId, data: { tagId: opt.id } });
  };

  const handleCreateTag = async (raw: string) => {
    try {
      const created = await tagCreateMutation.mutateAsync({
        data: { name: raw },
      });
      void queryClient.invalidateQueries({
        queryKey: getTagsGetAllQueryKey(),
      });
      if (created?.id) {
        addTagMutation.mutate({ id: personId, data: { tagId: created.id } });
      }
    } catch {
      // useApiError handles user feedback via the mutation's onError.
    }
  };

  // Skills ---------------------------------------------------------------
  const skillPool: SuggestComboboxOption[] = useMemo(
    () =>
      allSkills
        .filter((sk): sk is SkillReadDto & { id: string; name: string } =>
          !!sk.id && !!sk.name,
        )
        .map((sk) => ({ id: sk.id, label: sk.name })),
    [allSkills],
  );

  const attachedSkills = useMemo(() => {
    const personSkills = person.skills ?? [];
    return personSkills
      .map((rs) => {
        const meta = allSkills.find((sk) => sk.id === rs.skillId);
        if (!meta?.id || !meta.name) return null;
        return {
          id: meta.id,
          name: meta.name,
          level: rs.level ?? SkillLevel.Basic,
          status: rs.approvalStatus ?? SkillApprovalStatus.Pending,
        };
      })
      .filter((x): x is NonNullable<typeof x> => x !== null)
      .sort((a, b) => a.name.localeCompare(b.name));
  }, [person.skills, allSkills]);

  const excludedSkillLabels = attachedSkills.map((sk) => sk.name);
  const pendingCount = attachedSkills.filter(
    (s) => s.status === SkillApprovalStatus.Pending,
  ).length;

  const handleAttachSkill = (opt: SuggestComboboxOption) => {
    addSkillMutation.mutate({
      id: personId,
      data: { skillId: opt.id, level: SkillLevel.Basic },
    });
  };

  const handleCreateSkill = async (raw: string) => {
    try {
      const created = await skillCreateMutation.mutateAsync({
        data: { name: raw },
      });
      void queryClient.invalidateQueries({
        queryKey: getSkillsGetAllQueryKey(),
      });
      if (created?.id) {
        addSkillMutation.mutate({
          id: personId,
          data: { skillId: created.id, level: SkillLevel.Basic },
        });
      }
    } catch {
      // useApiError handles user feedback.
    }
  };

  // Role select options. Includes a synthetic "create" option when the user
  // types a value that doesn't exist in the pool: we surface it via the
  // dropdown render so the experience matches Tags/Skills.
  const roleSelectOptions = useMemo(
    () =>
      allRoles
        .filter(
          (r): r is RoleReadDto & { id: string; name: string } => !!r.id && !!r.name,
        )
        .map((r) => ({ value: r.id, label: r.name })),
    [allRoles],
  );

  return (
    <Space direction="vertical" size={16} className={styles.stack}>
      {/* Header card */}
      <Card>
        <div className={styles.headerRow}>
          <InitialsAvatar
            name={personName || '?'}
            size={56}
            seed={personId}
            style={{ fontSize: 20 }}
          />
          <div className={styles.headerBody}>
            <Title level={4} className={styles.title}>
              <InlineEditableText
                value={personName}
                fontSize={20}
                fontWeight={600}
                placeholder={t('people.namePlaceholder')}
                onSave={handleRename}
                validate={(next) =>
                  next.length === 0
                    ? t('people.nameRequired')
                    : next.length > 200
                      ? t('people.nameMaxLength')
                      : null
                }
              />
            </Title>

            <div className={styles.email}>
              <InlineEditableText
                value={person.email ?? ''}
                fontSize={13}
                placeholder={t('people.emailPlaceholder')}
                onSave={handleEmailSave}
                validate={(next) => {
                  if (next.length === 0) return null;
                  if (next.length > 256) return t('people.emailMaxLength');
                  return EMAIL_REGEX.test(next) ? null : t('people.emailInvalid');
                }}
              />
            </div>

            <div className={styles.roleRow}>
              {!(person.isActive ?? true) && (
                <Tag color="default">{t('people.inactiveBadge')}</Tag>
              )}
              <Text type="secondary" className={styles.caption}>
                {t('people.role')}
              </Text>
              <RoleSelect
                value={currentRole?.id ?? null}
                options={roleSelectOptions}
                loading={assignRoleMutation.isPending}
                creating={roleCreateMutation.isPending}
                onChange={handleRoleChange}
                onCreate={handleCreateRoleThenAssign}
              />
            </div>
          </div>
          <Dropdown
            placement="bottomRight"
            menu={{
              items: [
                {
                  key: 'toggle-active',
                  label: (person.isActive ?? true)
                    ? t('people.setInactive')
                    : t('people.setActive'),
                  onClick: handleToggleActive,
                },
                { type: 'divider' },
                {
                  key: 'delete',
                  label: t('people.deleteAction'),
                  danger: true,
                  onClick: confirmDelete,
                },
              ],
            }}
          >
            <Button type="text" icon={<MoreOutlined />} />
          </Dropdown>
        </div>
      </Card>

      {/* Tags card */}
      <Card
        title={
          <div className={styles.cardTitleRow}>
            <Text strong className={styles.cardTitleText}>{t('people.tags.title')}</Text>
            <Text type="secondary" className={styles.cardTitleSub}>{t('people.tags.subtitle')}</Text>
          </div>
        }
      >
        <Text type="secondary" className={styles.cardDesc}>
          {t('people.tags.description')}
        </Text>
        <div className={styles.tagWrap}>
          {attachedTags.length === 0 && (
            <Text className={styles.muted}>{t('people.tags.empty')}</Text>
          )}
          {attachedTags.map((tag) => (
            <Tag
              key={tag.id}
              color="blue"
              closable
              onClose={(e) => {
                e.preventDefault();
                removeTagMutation.mutate({
                  id: personId,
                  tagId: tag.id,
                });
              }}
              className={styles.tagItem}
            >
              {tag.name}
            </Tag>
          ))}
        </div>
        <div className={styles.comboWrap}>
          <SuggestCombobox
            pool={tagPool}
            exclude={excludedTagLabels}
            placeholder={t('people.tags.addPlaceholder')}
            createLabel={t('people.tags.createLabel')}
            size="small"
            onPick={handleAttachTag}
            onCreate={handleCreateTag}
            loading={
              tagCreateMutation.isPending ||
              addTagMutation.isPending
            }
          />
        </div>
      </Card>

      {/* Skills card */}
      <Card
        title={
          <div className={styles.cardTitleRow}>
            <Text strong className={styles.cardTitleText}>{t('people.skills.title')}</Text>
            <Text type="secondary" className={styles.cardTitleSub}>{t('people.skills.subtitle')}</Text>
          </div>
        }
      >
        <Text type="secondary" className={styles.cardDesc}>
          {t('people.skills.description')}
        </Text>

        {attachedSkills.length === 0 ? (
          <Text className={styles.mutedBlock}>{t('people.skills.empty')}</Text>
        ) : (
          <div className={styles.skillList}>
            {attachedSkills.map((skill) => (
              <PersonSkillRow
                key={skill.id}
                name={skill.name}
                level={skill.level}
                status={skill.status}
                onChangeLevel={(next) =>
                  updateSkillLevelMutation.mutate({
                    id: personId,
                    skillId: skill.id,
                    data: { skillId: skill.id, level: next },
                  })
                }
                onRemove={() =>
                  removeSkillMutation.mutate({
                    id: personId,
                    skillId: skill.id,
                  })
                }
                busy={
                  (updateSkillLevelMutation.isPending &&
                    updateSkillLevelMutation.variables?.skillId === skill.id) ||
                  (removeSkillMutation.isPending &&
                    removeSkillMutation.variables?.skillId === skill.id)
                }
              />
            ))}
          </div>
        )}

        {pendingCount > 0 && (
          <Alert
            type="warning"
            showIcon
            icon={<WarningFilled style={{ color: token.colorWarning }} />}
            message={t('people.skills.pendingCount', { count: pendingCount })}
            className={styles.alert}
          />
        )}

        <div className={styles.comboWrap}>
          <SuggestCombobox
            pool={skillPool}
            exclude={excludedSkillLabels}
            placeholder={t('people.skills.addPlaceholder')}
            createLabel={t('people.skills.createLabel')}
            size="small"
            onPick={handleAttachSkill}
            onCreate={handleCreateSkill}
            loading={
              skillCreateMutation.isPending || addSkillMutation.isPending
            }
          />
        </div>
      </Card>
    </Space>
  );
}

// AntD Select tuned for the centralized Role catalogue. The dropdown footer
// is an inline "create new role" row — same gesture as SuggestCombobox, but
// fit into a single-pick Select. The pool stays unmodified; create+assign
// is the caller's responsibility.
type RoleSelectProps = {
  value: string | null;
  options: { value: string; label: string }[];
  loading: boolean;
  creating: boolean;
  onChange: (id: string | null) => void;
  onCreate: (rawName: string) => void;
};

function RoleSelect({
  value,
  options,
  loading,
  creating,
  onChange,
  onCreate,
}: RoleSelectProps) {
  const { t } = useTranslation();
  const { styles } = useStyles();
  const [draft, setDraft] = useState('');

  const exactExists = options.some(
    (o) => o.label.toLowerCase() === draft.trim().toLowerCase(),
  );

  const handleCreate = () => {
    const raw = draft.trim();
    if (!raw || exactExists) return;
    onCreate(raw);
    setDraft('');
  };

  return (
    <Select
      size="small"
      className={styles.roleSelect}
      allowClear
      showSearch
      optionFilterProp="label"
      placeholder={t('people.rolePlaceholder')}
      value={value ?? undefined}
      options={options}
      loading={loading}
      onSearch={setDraft}
      onChange={(v) => onChange(v ?? null)}
      onClear={() => onChange(null)}
      dropdownRender={(menu) => (
        <>
          {menu}
          <Divider className={styles.roleDivider} />
          <div className={styles.roleCreateRow}>
            <Input
              size="small"
              placeholder={t('people.roleCreateLabel')}
              value={draft}
              onChange={(e) => setDraft(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === 'Enter') {
                  e.preventDefault();
                  handleCreate();
                }
              }}
            />
            <Button
              type="text"
              size="small"
              icon={<PlusOutlined />}
              disabled={draft.trim().length === 0 || exactExists}
              loading={creating}
              onClick={handleCreate}
            >
              {t('common.create')}
            </Button>
          </div>
        </>
      )}
    />
  );
}
