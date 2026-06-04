import { Badge, Button, Empty, Input, theme, Tooltip, Typography } from 'antd';
import { PlusOutlined, SearchOutlined, TeamOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { InitialsAvatar } from '@/components/domain/InitialsAvatar';
import type { ResourceReadDto } from '@/api/generated/schemas';

const { Text } = Typography;

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
  const { token } = theme.useToken();

  return (
    <div
      style={{
        background: token.colorBgContainer,
        border: `1px solid ${token.colorBorderSecondary}`,
        borderRadius: token.borderRadiusLG,
        overflow: 'hidden',
        display: 'flex',
        flexDirection: 'column',
      }}
    >
      <div
        style={{
          padding: '12px 16px',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          borderBottom: `1px solid ${token.colorBorderSecondary}`,
          gap: 8,
        }}
      >
        <Text strong>
          {t('people.listCount', { count: people.length })}
        </Text>
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

      <div
        style={{
          padding: '10px 12px',
          borderBottom: `1px solid ${token.colorBorderSecondary}`,
        }}
      >
        <Input
          size="small"
          value={search}
          onChange={(e) => onSearchChange(e.target.value)}
          placeholder={t('people.searchPlaceholder')}
          prefix={
            <SearchOutlined style={{ color: token.colorTextTertiary, fontSize: 13 }} />
          }
          allowClear
        />
      </div>

      {inlineSlot}

      <div style={{ overflow: 'auto', flex: 1, maxHeight: 620 }}>
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
              style={{
                position: 'relative',
                padding: '12px 16px',
                cursor: 'pointer',
                borderBottom: `1px solid ${token.colorBorderSecondary}`,
                background: selected ? token.colorPrimaryBg : 'transparent',
                display: 'flex',
                alignItems: 'center',
                gap: 12,
                transition: `background ${token.motionDurationFast}`,
              }}
              onMouseEnter={(e) => {
                if (!selected)
                  e.currentTarget.style.background = token.colorFillTertiary;
              }}
              onMouseLeave={(e) => {
                if (!selected) e.currentTarget.style.background = 'transparent';
              }}
            >
              {selected && (
                <span
                  style={{
                    position: 'absolute',
                    left: 0,
                    top: 0,
                    bottom: 0,
                    width: 3,
                    background: token.colorPrimary,
                  }}
                />
              )}
              <InitialsAvatar name={p.name ?? '?'} size={36} seed={id} />
              <div style={{ flex: 1, minWidth: 0 }}>
                <div
                  style={{
                    fontSize: 14,
                    fontWeight: 500,
                    whiteSpace: 'nowrap',
                    overflow: 'hidden',
                    textOverflow: 'ellipsis',
                    color: p.isActive ? token.colorText : token.colorTextTertiary,
                  }}
                >
                  {p.name || '—'}
                </div>
                {secondary && (
                  <div
                    style={{
                      fontSize: 12,
                      color: token.colorTextTertiary,
                      whiteSpace: 'nowrap',
                      overflow: 'hidden',
                      textOverflow: 'ellipsis',
                    }}
                  >
                    {secondary}
                  </div>
                )}
              </div>
              {pending > 0 && (
                <Tooltip
                  title={t('people.pendingBadgeTooltip', { count: pending })}
                >
                  <Badge
                    count={pending}
                    style={{
                      backgroundColor: token.colorWarningBg,
                      color: token.colorWarningText,
                      border: `1px solid ${token.colorWarningBorder}`,
                      boxShadow: 'none',
                      fontVariantNumeric: 'tabular-nums',
                    }}
                    overflowCount={99}
                  />
                </Tooltip>
              )}
            </div>
          );
        })}
        {people.length === 0 && (
          <div style={{ padding: 24 }}>
            <Empty
              image={<TeamOutlined style={{ fontSize: 44, color: token.colorTextQuaternary }} />}
              imageStyle={{ height: 56, display: 'flex', justifyContent: 'center' }}
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
