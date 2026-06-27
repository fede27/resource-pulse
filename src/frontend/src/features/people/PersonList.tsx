import { Badge, Button, Empty, Input, Tooltip, Typography } from 'antd';
import { PlusOutlined, SearchOutlined, TeamOutlined } from '@ant-design/icons';
import { createStyles } from 'antd-style';
import { useTranslation } from 'react-i18next';
import { InitialsAvatar } from '@/components/domain/InitialsAvatar';
import type { ResourceReadDto } from '@/api/generated/schemas';

const { Text } = Typography;

const useStyles = createStyles(({ token, css }) => ({
  root: css`
    background: ${token.colorBgContainer};
    border: 1px solid ${token.colorBorderSecondary};
    border-radius: ${token.borderRadiusLG}px;
    overflow: hidden;
    display: flex;
    flex-direction: column;
  `,
  header: css`
    padding: ${token.paddingSM}px ${token.padding}px;
    display: flex;
    align-items: center;
    justify-content: space-between;
    border-bottom: 1px solid ${token.colorBorderSecondary};
    gap: ${token.marginXS}px;
  `,
  searchWrap: css`
    padding: ${token.paddingXS}px ${token.paddingSM}px;
    border-bottom: 1px solid ${token.colorBorderSecondary};
  `,
  searchIcon: css`
    color: ${token.colorTextTertiary};
    font-size: ${token.fontSizeSM}px;
  `,
  list: css`
    overflow: auto;
    flex: 1;
    max-height: 620px;
  `,
  row: css`
    position: relative;
    padding: ${token.paddingSM}px ${token.padding}px;
    cursor: pointer;
    border-bottom: 1px solid ${token.colorBorderSecondary};
    background: transparent;
    display: flex;
    align-items: center;
    gap: ${token.marginSM}px;
    transition: background ${token.motionDurationFast};
    &:hover {
      background: ${token.colorFillTertiary};
    }
  `,
  rowSelected: css`
    background: ${token.colorPrimaryBg};
    &:hover {
      background: ${token.colorPrimaryBg};
    }
  `,
  accent: css`
    position: absolute;
    left: 0;
    top: 0;
    bottom: 0;
    width: 3px;
    background: ${token.colorPrimary};
  `,
  rowBody: css`
    flex: 1;
    min-width: 0;
  `,
  name: css`
    font-size: ${token.fontSize}px;
    font-weight: 500;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
    color: ${token.colorText};
  `,
  nameInactive: css`
    color: ${token.colorTextTertiary};
  `,
  secondary: css`
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextTertiary};
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  `,
  pendingBadge: css`
    .ant-badge-count {
      background: ${token.colorWarningBg};
      color: ${token.colorWarningText};
      border: 1px solid ${token.colorWarningBorder};
      box-shadow: none;
      font-variant-numeric: tabular-nums;
    }
  `,
  emptyWrap: css`
    padding: ${token.paddingLG}px;
  `,
  emptyIcon: css`
    font-size: 44px;
    color: ${token.colorTextQuaternary};
  `,
}));

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
