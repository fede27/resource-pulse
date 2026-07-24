import { useMemo } from 'react';
import { App, Button, Select, Tag, Typography, theme } from 'antd';
import { DeleteOutlined, ExclamationCircleOutlined } from '@ant-design/icons';
import { useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import {
  getResourcesGetAllQueryKey,
  getResourcesGetByIdQueryKey,
  useResourcesAssignRole,
  useResourcesAssignTeam,
  useResourcesAddTag,
  useResourcesDelete,
  useResourcesRemoveTag,
  useResourcesUpdate,
} from '@/api/generated/resources/resources';
import {
  getTagsGetAllQueryKey,
  useTagsCreate,
} from '@/api/generated/tags/tags';
import type { ResourceReadDto, TagReadDto } from '@/api/generated/schemas';
import { InitialsAvatar } from '@/components/domain/InitialsAvatar';
import { InlineEditableText } from '@/components/domain/InlineEditableText';
import {
  SuggestCombobox,
  type SuggestComboboxOption,
} from '@/components/domain/SuggestCombobox';
import { useApiError } from '@/lib/errors';
import type { Category } from './rolesTeamsModel';
import { useStyles } from './PersonInspector.styles';

const { Text } = Typography;
const EMAIL_REGEX = /^[^@\s]+@[^@\s]+\.[^@\s]+$/;

export type PersonInspectorProps = {
  person: ResourceReadDto;
  roles: Category[];
  teams: Category[];
  tags: TagReadDto[];
  onDeleted: () => void;
};

export function PersonInspector({
  person,
  roles,
  teams,
  tags,
  onDeleted,
}: PersonInspectorProps) {
  const { t } = useTranslation();
  const { token } = theme.useToken();
  const { styles, cx } = useStyles();
  const { message, modal } = App.useApp();
  const queryClient = useQueryClient();
  const showApiError = useApiError();

  const personId = person.id ?? '';
  const personName = person.name ?? '';

  const invalidate = () => {
    void queryClient.invalidateQueries({ queryKey: getResourcesGetAllQueryKey() });
    void queryClient.invalidateQueries({
      queryKey: getResourcesGetByIdQueryKey(personId),
    });
  };

  const updateMutation = useResourcesUpdate({
    mutation: {
      onSuccess: () => invalidate(),
      onError: (e) => showApiError(e),
    },
  });
  const assignRoleMutation = useResourcesAssignRole({
    mutation: { onSuccess: () => invalidate(), onError: (e) => showApiError(e) },
  });
  const assignTeamMutation = useResourcesAssignTeam({
    mutation: { onSuccess: () => invalidate(), onError: (e) => showApiError(e) },
  });
  const addTagMutation = useResourcesAddTag({
    mutation: {
      onSuccess: () => {
        message.success(t('rolesTeams.toast.tagAdded'));
        invalidate();
        void queryClient.invalidateQueries({ queryKey: getTagsGetAllQueryKey() });
      },
      onError: (e) => showApiError(e),
    },
  });
  const removeTagMutation = useResourcesRemoveTag({
    mutation: {
      onSuccess: () => {
        message.success(t('rolesTeams.toast.tagRemoved'));
        invalidate();
      },
      onError: (e) => showApiError(e),
    },
  });
  const tagCreateMutation = useTagsCreate({
    mutation: { onError: (e) => showApiError(e) },
  });
  const deleteMutation = useResourcesDelete({
    mutation: {
      onSuccess: () => {
        message.success(t('rolesTeams.toast.personDeleted'));
        void queryClient.invalidateQueries({ queryKey: getResourcesGetAllQueryKey() });
        onDeleted();
      },
      onError: (e) => showApiError(e),
    },
  });

  const updateField = (patch: { name?: string; email?: string | null }) => {
    updateMutation.mutate({
      id: personId,
      data: {
        name: patch.name ?? personName,
        email: patch.email !== undefined ? patch.email : (person.email ?? null),
        isActive: person.isActive ?? true,
        businessCalendarId: person.businessCalendarId ?? '',
        roleId: person.roleId ?? null,
      },
    });
  };

  const role = useMemo(
    () => roles.find((r) => r.id === person.roleId) ?? null,
    [roles, person.roleId],
  );

  const tagPool: SuggestComboboxOption[] = useMemo(
    () =>
      tags
        .filter((g): g is TagReadDto & { id: string; name: string } => !!g.id && !!g.name)
        .map((g) => ({ id: g.id, label: g.name })),
    [tags],
  );

  const attachedTags = useMemo(() => {
    return (person.tags ?? [])
      .map((rt) => {
        const meta = tags.find((g) => g.id === rt.tagId);
        return meta?.id && meta.name ? { id: meta.id, name: meta.name } : null;
      })
      .filter((x): x is { id: string; name: string } => x !== null)
      .sort((a, b) => a.name.localeCompare(b.name));
  }, [person.tags, tags]);

  const handleCreateTag = async (raw: string) => {
    try {
      const created = await tagCreateMutation.mutateAsync({ data: { name: raw } });
      void queryClient.invalidateQueries({ queryKey: getTagsGetAllQueryKey() });
      if (created?.id) addTagMutation.mutate({ id: personId, data: { tagId: created.id } });
    } catch {
      // useApiError funnels feedback via the mutation onError.
    }
  };

  const confirmDelete = () => {
    modal.confirm({
      title: t('rolesTeams.person.deleteTitle', { name: personName }),
      content: (
        <>
          {t('rolesTeams.person.deleteBody')} {t('common.irreversibleAction')}
        </>
      ),
      icon: <ExclamationCircleOutlined style={{ color: token.colorError }} />,
      okText: t('common.delete'),
      okButtonProps: { danger: true },
      cancelText: t('common.cancel'),
      onOk: () => deleteMutation.mutateAsync({ id: personId }).catch(() => undefined),
    });
  };

  return (
    <div>
      <div className={styles.identity}>
        <InitialsAvatar name={personName || '?'} size={52} seed={personId} />
        <div className={styles.idBody}>
          <InlineEditableText
            value={personName}
            fontSize={19}
            fontWeight={600}
            placeholder={t('rolesTeams.person.namePlaceholder')}
            onSave={(v) => updateField({ name: v })}
            validate={(v) => (v.length === 0 ? t('rolesTeams.person.nameRequired') : null)}
          />
          <div className={styles.email}>
            <InlineEditableText
              value={person.email ?? ''}
              fontSize={13}
              placeholder={t('rolesTeams.person.emailPlaceholder')}
              onSave={(v) => updateField({ email: v.trim() ? v.trim() : null })}
              validate={(v) =>
                v.length === 0 || EMAIL_REGEX.test(v)
                  ? null
                  : t('rolesTeams.person.emailInvalid')
              }
            />
          </div>
        </div>
      </div>

      <div className={cx(styles.section, styles.sectionFirst)}>
        {t('rolesTeams.person.roleLabel')}
      </div>
      <Select
        className={styles.fullSelect}
        value={person.roleId ?? undefined}
        allowClear
        showSearch
        optionFilterProp="label"
        loading={assignRoleMutation.isPending}
        onChange={(v) =>
          assignRoleMutation.mutate({ id: personId, data: { roleId: v ?? null } })
        }
        options={roles.map((r) => ({ value: r.id, label: r.name }))}
      />

      <div className={styles.section}>{t('rolesTeams.person.teamLabel')}</div>
      <Select
        className={styles.fullSelect}
        value={person.teamId ?? undefined}
        allowClear
        showSearch
        optionFilterProp="label"
        placeholder={t('rolesTeams.person.noTeam')}
        loading={assignTeamMutation.isPending}
        onChange={(v) =>
          assignTeamMutation.mutate({ id: personId, data: { teamId: v ?? null } })
        }
        options={teams.map((tm) => ({ value: tm.id, label: tm.name }))}
      />

      <div className={styles.section}>{t('rolesTeams.person.tagsLabel')}</div>
      <div className={styles.tagWrap}>
        {attachedTags.length === 0 && (
          <span className={styles.tagEmpty}>{t('rolesTeams.person.tagsEmpty')}</span>
        )}
        {attachedTags.map((tag) => (
          <Tag
            key={tag.id}
            color="blue"
            closable
            onClose={(e) => {
              e.preventDefault();
              removeTagMutation.mutate({ id: personId, tagId: tag.id });
            }}
          >
            {tag.name}
          </Tag>
        ))}
      </div>
      <SuggestCombobox
        pool={tagPool}
        exclude={attachedTags.map((tg) => tg.name)}
        placeholder={t('rolesTeams.person.addTag')}
        createLabel={t('rolesTeams.person.createTag')}
        size="small"
        onPick={(opt) => addTagMutation.mutate({ id: personId, data: { tagId: opt.id } })}
        onCreate={handleCreateTag}
        loading={tagCreateMutation.isPending || addTagMutation.isPending}
      />

      <div className={styles.rule}>
        {role ? (
          <>
            {t('rolesTeams.person.rule', { role: role.name })}
          </>
        ) : (
          t('rolesTeams.person.ruleNoRole')
        )}
      </div>

      <div className={styles.deleteWrap}>
        <Button type="text" danger icon={<DeleteOutlined />} onClick={confirmDelete}>
          <Text type="danger">{t('rolesTeams.person.deleteAction')}</Text>
        </Button>
      </div>
    </div>
  );
}
