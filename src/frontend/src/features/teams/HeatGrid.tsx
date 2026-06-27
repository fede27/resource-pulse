import { useMemo, type ReactNode } from 'react';
import { Button, Tag, theme } from 'antd';
import { DeleteOutlined, RightOutlined } from '@ant-design/icons';
import { createStyles } from 'antd-style';
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

const TEAM_H = 50;
const PERSON_H = 38;
const ADD_H = 38;

// Decorative loading/hatch patterns — pure shimmer ornament, intentionally not
// part of the token design language.
const LOADING_BG =
  'repeating-linear-gradient(45deg, #f0f0f0, #f0f0f0 4px, #f7f7f7 4px, #f7f7f7 8px)';
const ADD_ROW_HATCH =
  'repeating-linear-gradient(45deg, #fafafa, #fafafa 6px, #f3f3f3 6px, #f3f3f3 12px)';
// Decorative inset bevel highlight (white at low alpha) — not a themeable colour.
const BEVEL = 'inset -1px -1px 0 rgba(255,255,255,.5)';

const fmtH = (h: number) => Math.round(h);
const pctText = (load: BucketLoad): string =>
  load.empty ? '' : Number.isFinite(load.pct) ? String(Math.round(load.pct)) : '∞';

const useStyles = createStyles(({ token, css }) => ({
  cell: css`
    display: flex;
    align-items: center;
    justify-content: center;
    font-variant-numeric: tabular-nums;
    letter-spacing: -0.02em;
  `,
  reducedDot: css`
    position: absolute;
    top: 3px;
    right: 3px;
    width: 3px;
    height: 3px;
    border-radius: 50%;
    background: ${token.colorTextTertiary};
  `,
  cornerTop: css`
    padding: 0 14px;
    font-size: 11px;
    color: ${token.colorTextTertiary};
    letter-spacing: 0.05em;
    text-transform: uppercase;
  `,
  cornerBottom: css`
    width: 100%;
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 0 14px;
  `,
  cornerBottomTitle: css`
    font-size: ${token.fontSizeSM}px;
    font-weight: 600;
    color: ${token.colorTextSecondary};
  `,
  cornerBottomGrain: css`
    font-size: 11px;
    color: ${token.colorTextTertiary};
  `,
  bucketHead: css`
    height: 100%;
    background: ${token.colorFillQuaternary};
    border-bottom: 1px solid ${token.colorBorder};
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: space-between;
    padding-block-start: 5px;
  `,
  bucketLabel: css`
    font-size: 9.5px;
    font-variant-numeric: tabular-nums;
    white-space: nowrap;
  `,
  todayTag: css`
    font-size: 8px;
    color: ${token.colorPrimary};
    font-weight: 600;
  `,
  heatBar: css`
    width: 100%;
    height: 8px;
    box-shadow: inset 0 1px 0 rgba(255, 255, 255, 0.4);
  `,
  expandBtn: css`
    border: 0;
    background: transparent;
    cursor: pointer;
    padding: 2px;
    color: ${token.colorTextTertiary};
    display: inline-flex;
    transition: transform 0.18s;
    flex-shrink: 0;
    transform: none;
  `,
  expandBtnOpen: css`
    transform: rotate(90deg);
  `,
  expandIcon: css`
    font-size: 11px;
  `,
  teamNameWrap: css`
    flex: 1;
    min-width: 0;
    margin-inline-start: ${token.marginXS}px;
  `,
  teamName: css`
    font-size: ${token.fontSize}px;
    font-weight: 600;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
    line-height: 1.2;
    display: flex;
    align-items: center;
    gap: 6px;
  `,
  inactiveTag: css`
    font-size: 10px;
    line-height: 16px;
    margin-inline-end: 0;
  `,
  teamMeta: css`
    font-size: 11.5px;
    color: ${token.colorTextTertiary};
    margin-block-start: 1px;
  `,
  teamActions: css`
    display: flex;
    align-items: center;
    gap: 2px;
    flex-shrink: 0;
  `,
  nowBadge: css`
    flex-shrink: 0;
    min-width: 42px;
    height: 22px;
    margin-inline-start: 6px;
    padding: 0 ${token.paddingXS}px;
    border-radius: ${token.borderRadius}px;
    font-size: ${token.fontSizeSM}px;
    font-weight: 600;
    display: inline-flex;
    align-items: center;
    justify-content: center;
    font-variant-numeric: tabular-nums;
  `,
  personNameWrap: css`
    flex: 1;
    min-width: 0;
    margin-inline-start: 9px;
  `,
  personName: css`
    font-size: ${token.fontSizeSM}px;
    font-weight: 500;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
    line-height: 1.15;
  `,
  personRole: css`
    font-size: 11px;
    color: ${token.colorTextTertiary};
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  `,
  deleteIcon: css`
    font-size: ${token.fontSizeSM}px;
  `,
  addLabel: css`
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextTertiary};
    margin-inline-start: ${token.marginXXS}px;
  `,
}));

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
