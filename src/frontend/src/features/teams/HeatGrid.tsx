import { useMemo, type ReactNode } from 'react';
import { Button, Tag } from 'antd';
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

const TEAM_H = 50;
const PERSON_H = 38;
const ADD_H = 38;

const LOADING_BG =
  'repeating-linear-gradient(45deg, #f0f0f0, #f0f0f0 4px, #f7f7f7 4px, #f7f7f7 8px)';

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
  const c = loadColor(load.pct, bands);
  const shadows = ['inset -1px -1px 0 rgba(255,255,255,.5)'];
  if (isToday) shadows.push('inset 2px 0 0 #1677ff');
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
      style={{
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        background: loading ? undefined : c.bg,
        backgroundImage: loading ? LOADING_BG : undefined,
        color: c.fg,
        fontSize: big ? 12 : 10,
        fontWeight: big ? 600 : 500,
        fontVariantNumeric: 'tabular-nums',
        boxShadow: shadows.join(','),
        letterSpacing: '-.02em',
      }}
    >
      {!loading && showText && txt ? (big ? `${txt}%` : txt) : ''}
      {!loading && load.reduced && !load.empty && (
        <span
          style={{
            position: 'absolute',
            top: 3,
            right: 3,
            width: 3,
            height: 3,
            borderRadius: '50%',
            background: 'rgba(0,0,0,.35)',
          }}
        />
      )}
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
  const { buckets, bands, teams, membersByTeam, overall } = grid;
  const showCellText = grid.grain === 'month' || showValues;

  // overall load by logical bucket index, for the header heat bar.
  const overallByIdx = useMemo(() => {
    const m = new Map<number, BucketLoad>();
    buckets.forEach((b, i) => m.set(b.idx, overall[i] ?? EMPTY_LOAD));
    return m;
  }, [buckets, overall]);

  const cornerTop = (
    <span
      style={{
        padding: '0 14px',
        fontSize: 11,
        color: 'rgba(0,0,0,.4)',
        letterSpacing: '.05em',
        textTransform: 'uppercase',
      }}
    >
      {t('teams.grid.overallLoad')}
    </span>
  );
  const cornerBottom = (
    <div
      style={{
        width: '100%',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        padding: '0 14px',
      }}
    >
      <span style={{ fontSize: 12, fontWeight: 600, color: 'rgba(0,0,0,.7)' }}>
        {t('teams.grid.rowsHeader')}
      </span>
      <span style={{ fontSize: 11, color: 'rgba(0,0,0,.4)' }}>{t(`teams.grain.${grid.grain}`)}</span>
    </div>
  );

  const renderBucketHeader = (b: Bucket) => {
    const o = overallByIdx.get(b.idx) ?? EMPTY_LOAD;
    const c = loadColor(o.pct, bands);
    const loading = grid.isColumnLoading(b);
    return (
      <div
        title={`${bucketTooltip(b)} · ${t('teams.grid.overallLoad')} ${o.empty ? '—' : `${Math.round(o.pct)}%`}`}
        style={{
          height: '100%',
          background: '#fafafa',
          borderBottom: '1px solid #e8e8e8',
          boxShadow: b.isToday ? 'inset 2px 0 0 #1677ff' : 'inset -1px 0 0 #f0f0f0',
          display: 'flex',
          flexDirection: 'column',
          alignItems: 'center',
          justifyContent: 'space-between',
          paddingTop: 5,
        }}
      >
        <span
          style={{
            fontSize: 9.5,
            color: b.isToday ? '#1677ff' : 'rgba(0,0,0,.5)',
            fontWeight: b.isToday ? 600 : 400,
            fontVariantNumeric: 'tabular-nums',
            whiteSpace: 'nowrap',
          }}
        >
          {b.label}
        </span>
        {b.isToday && <span style={{ fontSize: 8, color: '#1677ff', fontWeight: 600 }}>{t('teams.grid.today')}</span>}
        <div
          style={{
            width: '100%',
            height: 8,
            background: loading ? '#ececec' : o.empty ? '#f0f0f0' : c.solid,
            backgroundImage: loading ? LOADING_BG : undefined,
            boxShadow: 'inset 0 1px 0 rgba(255,255,255,.4)',
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
              borderBottom="1px solid #f0f0f0"
              name={
                <>
                  <button
                    onClick={() => onToggleExpand(teamId)}
                    aria-label={isOpen ? t('teams.grid.collapse') : t('teams.grid.expand')}
                    style={{
                      border: 0,
                      background: 'transparent',
                      cursor: 'pointer',
                      padding: 2,
                      color: 'rgba(0,0,0,.45)',
                      display: 'inline-flex',
                      transform: isOpen ? 'rotate(90deg)' : 'none',
                      transition: 'transform .18s',
                      flexShrink: 0,
                    }}
                  >
                    <RightOutlined style={{ fontSize: 11 }} />
                  </button>
                  <div style={{ flex: 1, minWidth: 0, marginLeft: 8 }}>
                    <div
                      style={{
                        fontSize: 14,
                        fontWeight: 600,
                        whiteSpace: 'nowrap',
                        overflow: 'hidden',
                        textOverflow: 'ellipsis',
                        lineHeight: 1.2,
                        display: 'flex',
                        alignItems: 'center',
                        gap: 6,
                      }}
                    >
                      {team.name ?? '—'}
                      {team.isActive === false && (
                        <Tag bordered={false} style={{ fontSize: 10, lineHeight: '16px', marginInlineEnd: 0 }}>
                          {t('common.inactive')}
                        </Tag>
                      )}
                    </div>
                    <div style={{ fontSize: 11.5, color: 'rgba(0,0,0,.45)', marginTop: 1 }}>
                      {t('teams.grid.memberCount', { count: members.length })}
                    </div>
                  </div>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 2, flexShrink: 0 }}>
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
                      style={{
                        flexShrink: 0,
                        minWidth: 42,
                        height: 22,
                        marginLeft: 6,
                        padding: '0 8px',
                        borderRadius: 6,
                        background: nowC.bg,
                        color: nowC.fg,
                        fontSize: 12,
                        fontWeight: 600,
                        display: 'inline-flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        fontVariantNumeric: 'tabular-nums',
                      }}
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
                    borderBottom="1px solid #f5f5f5"
                    namePadding="0 10px 0 28px"
                    name={
                      <>
                        <InitialsAvatar name={p.name ?? '?'} seed={pid} size={24} style={{ fontSize: 10 }} />
                        <div style={{ flex: 1, minWidth: 0, marginLeft: 9 }}>
                          <div
                            style={{
                              fontSize: 13,
                              fontWeight: 500,
                              whiteSpace: 'nowrap',
                              overflow: 'hidden',
                              textOverflow: 'ellipsis',
                              lineHeight: 1.15,
                            }}
                          >
                            {p.name ?? '—'}
                          </div>
                          <div
                            style={{
                              fontSize: 11,
                              color: 'rgba(0,0,0,.4)',
                              whiteSpace: 'nowrap',
                              overflow: 'hidden',
                              textOverflow: 'ellipsis',
                            }}
                          >
                            {role || t('teams.members.noRole')}
                          </div>
                        </div>
                        <Button
                          type="text"
                          size="small"
                          icon={<DeleteOutlined style={{ fontSize: 12 }} />}
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
                background="#fcfcfc"
                borderBottom="1px solid #f0f0f0"
                namePadding="0 10px 0 28px"
                name={
                  <>
                    <MemberPopover team={team} grid={grid} onAssign={onAssign} assigningId={assigningId} />
                    <span style={{ fontSize: 12, color: 'rgba(0,0,0,.5)', marginLeft: 4 }}>
                      {members.length === 0 ? t('teams.grid.emptyTeam') : t('teams.grid.manageMembers')}
                    </span>
                  </>
                }
              >
                <TimelineRowFiller
                  height={ADD_H}
                  background="repeating-linear-gradient(45deg, #fafafa, #fafafa 6px, #f3f3f3 6px, #f3f3f3 12px)"
                />
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
