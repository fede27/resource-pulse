import { useEffect, useRef, useState } from 'react';
import { Button, Input, Select } from 'antd';
import type { InputRef } from 'antd';
import { CloseOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import type { Category } from './rolesTeamsModel';
import { useStyles } from './PersonInlineCreate.styles';

const EMAIL_REGEX = /^[^@\s]+@[^@\s]+\.[^@\s]+$/;

export type PersonCreateValues = {
  name: string;
  email: string | null;
  roleId: string | null;
  teamId: string | null;
};

export type PersonInlineCreateProps = {
  roles: Category[];
  teams: Category[];
  presetRoleId?: string;
  presetTeamId?: string;
  saving: boolean;
  onCancel: () => void;
  onCreate: (values: PersonCreateValues) => void;
};

export function PersonInlineCreate({
  roles,
  teams,
  presetRoleId,
  presetTeamId,
  saving,
  onCancel,
  onCreate,
}: PersonInlineCreateProps) {
  const { t } = useTranslation();
  const { styles } = useStyles();
  const [name, setName] = useState('');
  const [email, setEmail] = useState('');
  const [roleId, setRoleId] = useState<string | undefined>(presetRoleId);
  const [teamId, setTeamId] = useState<string | undefined>(presetTeamId);
  const [errors, setErrors] = useState<{ name?: boolean; email?: string }>({});
  const nameRef = useRef<InputRef>(null);

  useEffect(() => {
    setTimeout(() => nameRef.current?.focus(), 0);
  }, []);

  const submit = () => {
    const next: { name?: boolean; email?: string } = {};
    if (!name.trim()) next.name = true;
    if (email.trim() && !EMAIL_REGEX.test(email.trim())) {
      next.email = t('rolesTeams.create.emailInvalid');
    }
    setErrors(next);
    if (next.name || next.email) return;
    onCreate({
      name: name.trim(),
      email: email.trim() ? email.trim() : null,
      roleId: roleId ?? null,
      teamId: teamId ?? null,
    });
  };

  return (
    <div
      className={styles.root}
      onKeyDown={(e) => {
        if (e.key === 'Escape') onCancel();
      }}
    >
      <div className={styles.head}>
        <div className={styles.title}>{t('rolesTeams.create.title')}</div>
        <span className={styles.close} onClick={onCancel}>
          <CloseOutlined />
        </span>
      </div>
      <div className={styles.full}>
        <Input
          ref={nameRef}
          value={name}
          status={errors.name ? 'error' : ''}
          onChange={(e) => setName(e.target.value)}
          onPressEnter={submit}
          placeholder={t('rolesTeams.create.namePlaceholder')}
        />
      </div>
      <div className={styles.full}>
        <Input
          value={email}
          status={errors.email ? 'error' : ''}
          onChange={(e) => setEmail(e.target.value)}
          onPressEnter={submit}
          placeholder={t('rolesTeams.create.emailPlaceholder')}
        />
      </div>
      {errors.email && <div className={styles.err}>{errors.email}</div>}
      <div className={styles.twoCol}>
        <Select
          value={roleId}
          onChange={setRoleId}
          allowClear
          placeholder={t('rolesTeams.create.roleLabel')}
          options={roles.map((r) => ({ value: r.id, label: r.name }))}
        />
        <Select
          value={teamId}
          onChange={setTeamId}
          allowClear
          placeholder={t('rolesTeams.create.teamLabel')}
          options={teams.map((tm) => ({ value: tm.id, label: tm.name }))}
        />
      </div>
      <div className={styles.actions}>
        <Button size="small" onClick={onCancel}>
          {t('common.cancel')}
        </Button>
        <Button size="small" type="primary" loading={saving} onClick={submit}>
          {t('rolesTeams.create.submit')}
        </Button>
      </div>
    </div>
  );
}
