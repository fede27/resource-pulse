import { Badge, Button, Empty, Input, Tooltip, Typography } from 'antd';
import { PlusOutlined, SearchOutlined, TeamOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { InitialsAvatar } from '@/components/domain/InitialsAvatar';
import type { ResourceReadDto } from '@/api/generated/schemas';
import { useStyles } from './PersonList.styles';

const { Text } = Typography;

const EMPTY_IMAGE_STYLE = { height: 56, display: 'flex', justifyContent: 'center' } as const;

export type PersonListProps = {
  people: ResourceReadDto[];
  selectedId: string | null;
  onSelect: (id: string) => void;
  search: string;
  onSearchChange: (value: string) => void;
  onStartCreate: () => void;
  isCreating: boolean;
  /** Optional render slot rendered above the list (typically the inline create form). */
  inlineSlot?: React.ReactNode;
  /** Pending levels per person, indexed by resource id, to show the amber badge. */
  pendingByPerson: Record<string, number>;
  /** Role names per person, indexed by resource id, to show in the secondary line. */
  roleNameByPerson: Record<string, string>;
};

export function PersonList({
  people,
  selectedId,
  onSelect,
  search,
  onSearchChange,
  onStartCreate,
  isCreating,
  inlineSlot,
  pendingByPerson,
  roleNameByPerson,
}: PersonListProps) {
  const { t } = useTranslation();
  const { styles, cx } = useStyles();

  return (
    <div className={styles.root}>
      <div className={styles.header}>
        <Text strong>{t('people.listCount', { count: people.length })}</Text>
        {!isCreating && (
          <Button
            size="small"
            type="primary"
            icon={<PlusOutlined />}
            onClick={onStartCreate}
          >
            {t('people.addButton')}
          </Button>
        )}
      </div>

      <div className={styles.searchWrap}>
        <Input
          size="small"
          value={search}
          onChange={(e) => onSearchChange(e.target.value)}
          placeholder={t('people.searchPlaceholder')}
          prefix={<SearchOutlined className={styles.searchIcon} />}
          allowClear
        />
      </div>

      {inlineSlot}

      <div className={styles.list}>
        {people.map((p) => {
          const id = p.id ?? '';
          const selected = id === selectedId;
          const pending = pendingByPerson[id] ?? 0;
          const roleName = roleNameByPerson[id] ?? '';
          // Secondary line: role first, fallback to email, fallback to inactive marker.
          const secondary = !p.isActive
            ? t('people.inactiveBadge')
            : (roleName || p.email || '');
          return (
            <div
              key={id}
              onClick={() => onSelect(id)}
              className={cx(styles.row, selected && styles.rowSelected)}
            >
              {selected && <span className={styles.accent} />}
              <InitialsAvatar name={p.name ?? '?'} size={36} seed={id} />
              <div className={styles.rowBody}>
                <div className={cx(styles.name, !p.isActive && styles.nameInactive)}>
                  {p.name || '—'}
                </div>
                {secondary && <div className={styles.secondary}>{secondary}</div>}
              </div>
              {pending > 0 && (
                <Tooltip title={t('people.pendingBadgeTooltip', { count: pending })}>
                  <Badge count={pending} className={styles.pendingBadge} overflowCount={99} />
                </Tooltip>
              )}
            </div>
          );
        })}
        {people.length === 0 && (
          <div className={styles.emptyWrap}>
            <Empty
              image={<TeamOutlined className={styles.emptyIcon} />}
              imageStyle={EMPTY_IMAGE_STYLE}
              description={
                search.trim()
                  ? t('people.emptyListSearch')
                  : t('people.emptyListNoPeople')
              }
            />
          </div>
        )}
      </div>
    </div>
  );
}
