import { useMemo, useState } from 'react';
import { Button, Empty, Input, Popover, Tag, Tooltip } from 'antd';
import {
  CloseOutlined,
  PlusOutlined,
  SearchOutlined,
  TeamOutlined,
} from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { InitialsAvatar } from '@/components/domain/InitialsAvatar';
import type { TeamReadDto } from '@/api/generated/schemas';
import type { TeamGrid } from './useTeamGrid';
import { useStyles } from './MemberPopover.styles';

type MemberPopoverProps = {
  team: TeamReadDto;
  grid: TeamGrid;
  onAssign: (resourceId: string, teamId: string | null) => void;
  assigningId: string | null;
};

export function MemberPopover({ team, grid, onAssign, assigningId }: MemberPopoverProps) {
  const { t } = useTranslation();
  const { styles, cx } = useStyles();
  const [open, setOpen] = useState(false);
  const [q, setQ] = useState('');

  const teamId = team.id ?? '';
  const members = useMemo(() => grid.membersByTeam[teamId] ?? [], [grid.membersByTeam, teamId]);
  const memberSet = useMemo(() => new Set(members), [members]);
  const teamNameById = useMemo(() => {
    const out: Record<string, string> = {};
    grid.teams.forEach((tm) => {
      if (tm.id) out[tm.id] = tm.name ?? '';
    });
    return out;
  }, [grid.teams]);

  const ordered = useMemo(() => {
    const query = q.trim().toLowerCase();
    const rows = grid.allResources
      .filter((r) => !!r.id)
      .filter((r) => {
        if (!query) return true;
        const role = r.roleId ? grid.roleNameById[r.roleId] ?? '' : '';
        return (
          (r.name ?? '').toLowerCase().includes(query) ||
          role.toLowerCase().includes(query)
        );
      });
    // members of this team first, then everyone else, each alphabetical
    return rows.sort((a, b) => {
      const am = memberSet.has(a.id!) ? 0 : 1;
      const bm = memberSet.has(b.id!) ? 0 : 1;
      return am - bm || (a.name ?? '').localeCompare(b.name ?? '');
    });
  }, [grid.allResources, grid.roleNameById, q, memberSet]);

  // Built only while the popover is open — avoids rendering the full member
  // list (one per team row + add-member row) on every grid render.
  const content = !open ? null : (
    <div className={styles.content}>
      <Input
        autoFocus
        allowClear
        size="small"
        value={q}
        onChange={(e) => setQ(e.target.value)}
        placeholder={t('teams.members.searchPlaceholder')}
        prefix={<SearchOutlined className={styles.searchIcon} />}
        className={styles.search}
      />
      <div className={styles.countRow}>
        <span>
          {t('teams.members.count', {
            n: members.length,
            pool: grid.allResources.length,
          })}
        </span>
      </div>
      <div className={styles.list}>
        {ordered.map((r) => {
          const id = r.id!;
          const isMember = memberSet.has(id);
          const otherTeam = !isMember && r.teamId ? teamNameById[r.teamId] : undefined;
          const role = r.roleId ? grid.roleNameById[r.roleId] ?? '' : '';
          const busy = assigningId === id;
          return (
            <div key={id} className={cx(styles.row, isMember && styles.rowMember)}>
              <InitialsAvatar name={r.name ?? '?'} seed={id} size={30} style={{ fontSize: 11 }} />
              <div className={styles.rowBody}>
                <div className={styles.name}>{r.name ?? '—'}</div>
                <div className={styles.role}>
                  {otherTeam ? (
                    <>
                      {role ? `${role} · ` : ''}
                      <Tag bordered={false} className={styles.teamTag}>
                        {otherTeam}
                      </Tag>
                    </>
                  ) : (
                    role || t('teams.members.noRole')
                  )}
                </div>
              </div>
              {isMember ? (
                <Button
                  size="small"
                  danger
                  loading={busy}
                  icon={<CloseOutlined />}
                  onClick={() => onAssign(id, null)}
                >
                  {t('teams.members.remove')}
                </Button>
              ) : (
                <Tooltip title={otherTeam ? t('teams.members.moveHint', { team: otherTeam }) : undefined}>
                  <Button
                    size="small"
                    type="primary"
                    ghost={!!otherTeam}
                    loading={busy}
                    icon={<PlusOutlined />}
                    onClick={() => onAssign(id, teamId)}
                  >
                    {otherTeam ? t('teams.members.move') : t('teams.members.add')}
                  </Button>
                </Tooltip>
              )}
            </div>
          );
        })}
        {!ordered.length && (
          <Empty
            image={Empty.PRESENTED_IMAGE_SIMPLE}
            description={t('teams.members.noneFound')}
            className={styles.empty}
          />
        )}
      </div>
    </div>
  );

  return (
    <Popover
      open={open}
      onOpenChange={setOpen}
      trigger="click"
      placement="bottomLeft"
      content={content}
      title={t('teams.members.title', { name: team.name ?? '' })}
    >
      <Button
        type="text"
        size="small"
        icon={<TeamOutlined />}
        aria-label={t('teams.members.manage')}
      />
    </Popover>
  );
}
