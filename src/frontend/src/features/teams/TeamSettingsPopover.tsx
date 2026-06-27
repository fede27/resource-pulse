import { useState } from 'react';
import { App, Button, Input, Popover, Switch } from 'antd';
import { DeleteOutlined, MoreOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import type { TeamReadDto } from '@/api/generated/schemas';
import { useStyles } from './TeamSettingsPopover.styles';

type TeamSettingsPopoverProps = {
  team: TeamReadDto;
  onRename: (name: string) => void;
  onToggleActive: (isActive: boolean) => void;
  onDelete: () => void;
  saving: boolean;
};

export function TeamSettingsPopover({
  team,
  onRename,
  onToggleActive,
  onDelete,
  saving,
}: TeamSettingsPopoverProps) {
  const { t } = useTranslation();
  const { styles } = useStyles();
  const { modal } = App.useApp();
  const [open, setOpen] = useState(false);
  const [name, setName] = useState(team.name ?? '');

  const commitRename = () => {
    const next = name.trim();
    if (next && next !== team.name) onRename(next);
  };

  const confirmDelete = () => {
    setOpen(false);
    modal.confirm({
      title: t('teams.settings.deleteConfirmTitle', { name: team.name ?? '' }),
      content: t('teams.settings.deleteConfirmBody'),
      okText: t('common.delete'),
      cancelText: t('common.cancel'),
      okButtonProps: { danger: true },
      onOk: () => onDelete(),
    });
  };

  const content = (
    <div className={styles.content}>
      <div>
        <div className={styles.label}>{t('common.name')}</div>
        <Input.Search
          size="small"
          value={name}
          enterButton={t('common.save')}
          loading={saving}
          onChange={(e) => setName(e.target.value)}
          onSearch={commitRename}
          placeholder={t('teams.namePlaceholder')}
        />
      </div>
      <div className={styles.toggleRow}>
        <span className={styles.toggleLabel}>{t('common.active')}</span>
        <Switch
          checked={team.isActive ?? true}
          loading={saving}
          onChange={(checked) => onToggleActive(checked)}
        />
      </div>
      <Button danger size="small" icon={<DeleteOutlined />} onClick={confirmDelete} block>
        {t('teams.settings.deleteTeam')}
      </Button>
    </div>
  );

  return (
    <Popover
      open={open}
      onOpenChange={(o) => {
        setOpen(o);
        if (o) setName(team.name ?? '');
      }}
      trigger="click"
      placement="bottomRight"
      content={content}
      title={t('teams.settings.title')}
    >
      <Button
        type="text"
        size="small"
        icon={<MoreOutlined />}
        aria-label={t('teams.settings.title')}
      />
    </Popover>
  );
}
