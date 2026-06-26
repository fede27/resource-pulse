import { useState } from 'react';
import { Button, Input, Space } from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';

type TeamCreateInlineProps = {
  onCreate: (name: string) => void;
  saving: boolean;
};

export function TeamCreateInline({ onCreate, saving }: TeamCreateInlineProps) {
  const { t } = useTranslation();
  const [editing, setEditing] = useState(false);
  const [name, setName] = useState('');

  const submit = () => {
    const next = name.trim();
    if (!next) return;
    onCreate(next);
    setName('');
    setEditing(false);
  };

  if (!editing) {
    return (
      <Button
        type="dashed"
        size="small"
        icon={<PlusOutlined />}
        onClick={() => setEditing(true)}
        block
      >
        {t('teams.newTitle')}
      </Button>
    );
  }

  return (
    <Space.Compact style={{ width: '100%' }}>
      <Input
        autoFocus
        size="small"
        value={name}
        placeholder={t('teams.namePlaceholder')}
        onChange={(e) => setName(e.target.value)}
        onPressEnter={submit}
        onKeyDown={(e) => {
          if (e.key === 'Escape') {
            setEditing(false);
            setName('');
          }
        }}
      />
      <Button size="small" type="primary" loading={saving} onClick={submit}>
        {t('common.create')}
      </Button>
    </Space.Compact>
  );
}
