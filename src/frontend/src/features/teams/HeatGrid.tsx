import { useMemo, type ReactNode } from 'react';
import { Button, Tag, theme } from 'antd';
import { DeleteOutlined, RightOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { InitialsAvatar } from '@/components/domain/InitialsAvatar';
import {
  bucketTooltip,
  TimelineCell,
  TimelineGrid,
  TimelineRow,
  TimelineRowFiller,
  type Bucket,
} from '@/components/timeline';
import type { TeamReadDto } from '@/api/generated/schemas';
import { EMPTY_LOAD, loadColor, type BucketLoad, type LoadBand } from './loadModel';
import type { TeamGrid } from './useTeamGrid';
import { MemberPopover } from './MemberPopover';
import { TeamSettingsPopover } from './TeamSettingsPopover';
import {
  ADD_H,
  ADD_ROW_HATCH,
  BEVEL,
  LOADING_BG,
  PERSON_H,
  TEAM_H,
  useStyles,
} from './HeatGrid.styles';

const fmtH = (h: number) => Math.round(h);
const pctText = (load: BucketLoad): string =>
  load.empty ? '' : Number.isFinite(load.pct) ? String(Math.round(load.pct)) : '∞';

type HeatGridProps = {
  grid: TeamGrid;
  expanded: Set<string>;
  onToggleExpand: (id: string) => void;
  showValues: boolean;
  onAssign: (resourceId: string, teamId: string | null) => void;
  assigningId: string | null;
  onRenameTeam: (team: TeamReadDto, name: string) => void;
  onToggleTeamActive: (team: TeamReadDto, isActive: boolean) => void;
  onDeleteTeam: (team: TeamReadDto) => void;
  savingTeamId: string | null;
  createRow: ReactNode;
};

function LoadCell({
  k,
  h,
  load,
  big,
  showText,
  isToday,
  bands,
  tooltip,
  name,
  loading,
}: {
  k: number;
  h: number;
  load: BucketLoad;
  big?: boolean;
  showText: boolean;
  isToday: boolean;
  bands: LoadBand[];
  tooltip: string;
  name: string;
  loading: boolean;
}) {
  const { styles } = useStyles();
  const { token } = theme.useToken();
  const c = loadColor(load.pct, bands);
  const shadows = [BEVEL];
  if (isToday) shadows.push(`inset 2px 0 0 ${token.colorPrimary}`);
  const txt = pctText(load);
  const detail = loading
    ? '…'
    : load.empty
      ? '—'
      : `${Number.isFinite(load.pct) ? `${Math.round(load.pct)}%` : '∞'} (${fmtH(load.allocH)}/${fmtH(load.capH)} h)`;
  return (
    <TimelineCell
      k={k}
      height={h}
      title={`${tooltip} · ${name}: ${detail}`}
      className={styles.cell}
      // dynamic: cell fill/text come from the resolved load colour; the box
      // shadow carries the today marker (token primary) + decorative bevel.
      style={{
        background: loading ? undefined : c.bg,
        backgroundImage: loading ? LOADING_BG : undefined,
        color: c.fg,
        fontSize: big ? 12 : 10,
        fontWeight: big ? 600 : 500,
        boxShadow: shadows.join(','),
      }}
    >
      {!loading && showText && txt ? (big ? `${txt}%` : txt) : ''}
      {!loading && load.reduced && !load.empty && <span className={styles.reducedDot} />}
    </TimelineCell>
  );
}

export function HeatGrid({
  grid,
  expanded,
  onToggleExpand,
  showValues,
  onAssign,
  assigningId,
  onRenameTeam,
  onToggleTeamActive,
  onDeleteTeam,
  savingTeamId,
  createRow,
}: HeatGridProps) {
  const { t } = useTranslation();
  const { styles, cx } = useStyles();
  const { token } = theme.useToken();
  const { buckets, bands, teams, membersByTeam, overall } = grid;
  const showCellText = grid.grain === 'month' || showValues;

  const rowBorder = `1px solid ${token.colorBorderSecondary}`;

  // overall load by logical bucket index, for the header heat bar.
  const overallByIdx = useMemo(() => {
    const m = new Map<number, BucketLoad>();
    buckets.forEach((b, i) => m.set(b.idx, overall[i] ?? EMPTY_LOAD));
    return m;
  }, [buckets, overall]);

  const cornerTop = <span className={styles.cornerTop}>{t('teams.grid.overallLoad')}</span>;
  const cornerBottom = (
    <div className={styles.cornerBottom}>
      <span className={styles.cornerBottomTitle}>{t('teams.grid.rowsHeader')}</span>
      <span className={styles.cornerBottomGrain}>{t(`teams.grain.${grid.grain}`)}</span>
    </div>
  );

  const renderBucketHeader = (b: Bucket) => {
    const o = overallByIdx.get(b.idx) ?? EMPTY_LOAD;
    const c = loadColor(o.pct, bands);
    const loading = grid.isColumnLoading(b);
    return (
      <div
        title={`${bucketTooltip(b)} · ${t('teams.grid.overallLoad')} ${o.empty ? '—' : `${Math.round(o.pct)}%`}`}
        className={styles.bucketHead}
        // dynamic: today gets a primary inset marker, others a hairline divider.
        style={{
          boxShadow: b.isToday
            ? `inset 2px 0 0 ${token.colorPrimary}`
            : `inset -1px 0 0 ${token.colorBorderSecondary}`,
        }}
      >
        <span
          className={styles.bucketLabel}
          // dynamic: today's label is emphasised in the primary colour.
          style={{
            color: b.isToday ? token.colorPrimary : token.colorTextTertiary,
            fontWeight: b.isToday ? 600 : 400,
          }}
        >
          {b.label}
        </span>
        {b.isToday && <span className={styles.todayTag}>{t('teams.grid.today')}</span>}
        <div
          className={styles.heatBar}
          // dynamic: heat-bar fill is the resolved overall-load colour.
          style={{
            background: loading ? token.colorFillSecondary : o.empty ? token.colorBorderSecondary : c.solid,
            backgroundImage: loading ? LOADING_BG : undefined,
          }}
        />
      </div>
    );
  };

  return (
    <TimelineGrid
      viewport={grid.viewport}
      buckets={buckets}
      groups={grid.groups}
      cellW={grid.cellW}
      nameW={grid.nameW}
      cornerTop={cornerTop}
      cornerBottom={cornerBottom}
      renderGroup={(g) => <span>{g.label}</span>}
      renderBucketHeader={renderBucketHeader}
    >
      {teams.map((team) => {
        const teamId = team.id ?? '';
        const isOpen = expanded.has(teamId);
        const tl = grid.teamLoads[teamId] ?? [];
        const members = membersByTeam[teamId] ?? [];
        const now = grid.nowByTeam[teamId] ?? EMPTY_LOAD;
        const nowC = loadColor(now.pct, bands);
        return (
          <div key={teamId}>
            {/* team row */}
            <TimelineRow
              height={TEAM_H}
              borderBottom={rowBorder}
              name={
                <>
                  <button
                    onClick={() => onToggleExpand(teamId)}
                    aria-label={isOpen ? t('teams.grid.collapse') : t('teams.grid.expand')}
                    className={cx(styles.expandBtn, isOpen && styles.expandBtnOpen)}
                  >
                    <RightOutlined className={styles.expandIcon} />
                  </button>
                  <div className={styles.teamNameWrap}>
                    <div className={styles.teamName}>
                      {team.name ?? '—'}
                      {team.isActive === false && (
                        <Tag bordered={false} className={styles.inactiveTag}>
                          {t('common.inactive')}
                        </Tag>
                      )}
                    </div>
                    <div className={styles.teamMeta}>
                      {t('teams.grid.memberCount', { count: members.length })}
                    </div>
                  </div>
                  <div className={styles.teamActions}>
                    <MemberPopover team={team} grid={grid} onAssign={onAssign} assigningId={assigningId} />
                    <TeamSettingsPopover
                      team={team}
                      onRename={(name) => onRenameTeam(team, name)}
                      onToggleActive={(active) => onToggleTeamActive(team, active)}
                      onDelete={() => onDeleteTeam(team)}
                      saving={savingTeamId === teamId}
                    />
                  </div>
                  {!now.empty && (
                    <span
                      title={t('teams.grid.currentLoad', {
                        value: Number.isFinite(now.pct) ? Math.round(now.pct) : '∞',
                      })}
                      className={styles.nowBadge}
                      // dynamic: badge colours are the team's current-load colour.
                      style={{ background: nowC.bg, color: nowC.fg }}
                    >
                      {Number.isFinite(now.pct) ? `${Math.round(now.pct)}%` : '∞'}
                    </span>
                  )}
                </>
              }
            >
              {buckets.map((b, i) => (
                <LoadCell
                  key={b.idx}
                  k={b.idx}
                  h={TEAM_H}
                  load={tl[i] ?? EMPTY_LOAD}
                  big
                  showText={showCellText}
                  isToday={b.isToday}
                  bands={bands}
                  tooltip={bucketTooltip(b)}
                  name={team.name ?? '—'}
                  loading={grid.isColumnLoading(b)}
                />
              ))}
            </TimelineRow>

            {/* person rows */}
            {isOpen &&
              members.map((pid) => {
                const p = grid.resourcesById[pid];
                if (!p) return null;
                const pl = grid.personLoads[pid] ?? [];
                const role = p.roleId ? grid.roleNameById[p.roleId] ?? '' : '';
                return (
                  <TimelineRow
                    key={pid}
                    height={PERSON_H}
                    borderBottom={rowBorder}
                    namePadding="0 10px 0 28px"
                    name={
                      <>
                        <InitialsAvatar name={p.name ?? '?'} seed={pid} size={24} style={{ fontSize: 10 }} />
                        <div className={styles.personNameWrap}>
                          <div className={styles.personName}>{p.name ?? '—'}</div>
                          <div className={styles.personRole}>
                            {role || t('teams.members.noRole')}
                          </div>
                        </div>
                        <Button
                          type="text"
                          size="small"
                          icon={<DeleteOutlined className={styles.deleteIcon} />}
                          loading={assigningId === pid}
                          aria-label={t('teams.members.remove')}
                          onClick={() => onAssign(pid, null)}
                        />
                      </>
                    }
                  >
                    {buckets.map((b, i) => (
                      <LoadCell
                        key={b.idx}
                        k={b.idx}
                        h={PERSON_H}
                        load={pl[i] ?? EMPTY_LOAD}
                        showText={showCellText}
                        isToday={b.isToday}
                        bands={bands}
                        tooltip={bucketTooltip(b)}
                        name={p.name ?? '—'}
                        loading={grid.isColumnLoading(b)}
                      />
                    ))}
                  </TimelineRow>
                );
              })}

            {/* add-member row */}
            {isOpen && (
              <TimelineRow
                height={ADD_H}
                background={token.colorFillQuaternary}
                borderBottom={rowBorder}
                namePadding="0 10px 0 28px"
                name={
                  <>
                    <MemberPopover team={team} grid={grid} onAssign={onAssign} assigningId={assigningId} />
                    <span className={styles.addLabel}>
                      {members.length === 0 ? t('teams.grid.emptyTeam') : t('teams.grid.manageMembers')}
                    </span>
                  </>
                }
              >
                <TimelineRowFiller height={ADD_H} background={ADD_ROW_HATCH} />
              </TimelineRow>
            )}
          </div>
        );
      })}

      {/* create team row */}
      <TimelineRow height={46} name={createRow} namePadding="6px 10px" />
    </TimelineGrid>
  );
}
