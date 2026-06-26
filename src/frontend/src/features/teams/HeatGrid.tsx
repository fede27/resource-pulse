import { Fragment, useRef, type ReactNode } from 'react';
import { Button, Tag } from 'antd';
import { DeleteOutlined, RightOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { InitialsAvatar } from '@/components/domain/InitialsAvatar';
import type { TeamReadDto } from '@/api/generated/schemas';
import { bucketTooltip, loadColor, type Bucket, type BucketLoad, type LoadBand } from './loadModel';
import type { TeamGrid } from './useTeamGrid';
import { MemberPopover } from './MemberPopover';
import { TeamSettingsPopover } from './TeamSettingsPopover';

const NAME_W = 300;
const CELL_W: Record<Bucket['grain'], number> = { day: 30, week: 32, month: 64 };

const EMPTY_LOAD: BucketLoad = { capH: 0, allocH: 0, pct: 0, empty: true, reduced: false };

const fmtPct = (load: BucketLoad): string =>
  load.empty ? '' : Number.isFinite(load.pct) ? String(Math.round(load.pct)) : '∞';
const fmtH = (h: number) => Math.round(h);

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

function HeatCell({
  load,
  w,
  h,
  big,
  showText,
  isToday,
  bands,
  tooltip,
  name,
}: {
  load: BucketLoad;
  w: number;
  h: number;
  big?: boolean;
  showText: boolean;
  isToday: boolean;
  bands: LoadBand[];
  tooltip: string;
  name: string;
}) {
  const c = loadColor(load.pct, bands);
  const shadows = ['inset -1px -1px 0 rgba(255,255,255,.5)'];
  if (isToday) shadows.push('inset 2px 0 0 #1677ff');
  const txt = fmtPct(load);
  const detail = load.empty
    ? '—'
    : `${Number.isFinite(load.pct) ? `${Math.round(load.pct)}%` : '∞'} (${fmtH(load.allocH)}/${fmtH(load.capH)} h)`;
  return (
    <div
      title={`${tooltip} · ${name}: ${detail}`}
      style={{
        width: w,
        flex: `0 0 ${w}px`,
        height: h,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        background: c.bg,
        color: c.fg,
        fontSize: big ? 12 : 10,
        fontWeight: big ? 600 : 500,
        fontVariantNumeric: 'tabular-nums',
        boxShadow: shadows.join(','),
        position: 'relative',
        letterSpacing: '-.02em',
      }}
    >
      {showText && txt ? (big ? `${txt}%` : txt) : ''}
      {load.reduced && !load.empty && (
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
    </div>
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
  const { buckets, groups, bands, teams, membersByTeam, todayIdx, overall, teamLoads, personLoads } = grid;
  const cellW = CELL_W[grid.grain];
  const showCellText = grid.grain === 'month' || showValues;
  const teamH = 50;
  const personH = 38;
  const totalW = NAME_W + buckets.length * cellW;

  const scrollRef = useRef<HTMLDivElement>(null);
  const overlayRef = useRef<HTMLDivElement>(null);
  const rafRef = useRef(0);
  const onMove = (e: React.MouseEvent) => {
    const sc = scrollRef.current;
    const ov = overlayRef.current;
    if (!sc || !ov) return;
    const rect = sc.getBoundingClientRect();
    const contentX = e.clientX - rect.left + sc.scrollLeft;
    if (rafRef.current) return;
    rafRef.current = requestAnimationFrame(() => {
      rafRef.current = 0;
      if (contentX < NAME_W) {
        ov.style.display = 'none';
        return;
      }
      const idx = Math.floor((contentX - NAME_W) / cellW);
      if (idx < 0 || idx >= buckets.length) {
        ov.style.display = 'none';
        return;
      }
      ov.style.display = 'block';
      ov.style.left = `${NAME_W + idx * cellW}px`;
      ov.style.width = `${cellW}px`;
    });
  };
  const onLeave = () => {
    if (overlayRef.current) overlayRef.current.style.display = 'none';
  };

  const headBg = '#fafafa';
  const headYearH = 26;
  const headWeekH = 46;

  return (
    <div style={{ border: '1px solid #f0f0f0', borderRadius: 10, overflow: 'hidden', background: '#fff' }}>
      <div
        ref={scrollRef}
        onMouseMove={onMove}
        onMouseLeave={onLeave}
        style={{ overflow: 'auto', maxHeight: 'min(640px, calc(100vh - 320px))' }}
      >
        <div style={{ position: 'relative', width: totalW, minWidth: totalW }}>
          <div
            ref={overlayRef}
            style={{
              position: 'absolute',
              top: 0,
              height: '100%',
              display: 'none',
              background: 'rgba(22,119,255,.06)',
              pointerEvents: 'none',
              zIndex: 6,
            }}
          />

          {/* ── Sticky header ── */}
          <div style={{ position: 'sticky', top: 0, zIndex: 30 }}>
            {/* primary group row */}
            <div style={{ display: 'flex' }}>
              <div
                style={{
                  position: 'sticky',
                  left: 0,
                  zIndex: 42,
                  width: NAME_W,
                  flex: `0 0 ${NAME_W}px`,
                  height: headYearH,
                  background: headBg,
                  borderRight: '1px solid #e8e8e8',
                  borderBottom: '1px solid #f0f0f0',
                  display: 'flex',
                  alignItems: 'center',
                  padding: '0 14px',
                  fontSize: 11,
                  color: 'rgba(0,0,0,.4)',
                  letterSpacing: '.05em',
                  textTransform: 'uppercase',
                }}
              >
                {t('teams.grid.overallLoad')}
              </div>
              {groups.map((g) => (
                <div
                  key={g.key}
                  style={{
                    width: g.count * cellW,
                    flex: `0 0 ${g.count * cellW}px`,
                    height: headYearH,
                    background: headBg,
                    borderBottom: '1px solid #f0f0f0',
                    borderLeft: '1px solid #e8e8e8',
                    display: 'flex',
                    alignItems: 'center',
                    padding: '0 10px',
                    fontSize: 12,
                    fontWeight: 600,
                    color: 'rgba(0,0,0,.75)',
                  }}
                >
                  <span style={{ position: 'sticky', left: NAME_W + 8 }}>{g.label}</span>
                </div>
              ))}
            </div>
            {/* secondary buckets + overall heat bar */}
            <div style={{ display: 'flex' }}>
              <div
                style={{
                  position: 'sticky',
                  left: 0,
                  zIndex: 42,
                  width: NAME_W,
                  flex: `0 0 ${NAME_W}px`,
                  height: headWeekH,
                  background: headBg,
                  borderRight: '1px solid #e8e8e8',
                  borderBottom: '1px solid #e8e8e8',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'space-between',
                  padding: '0 14px',
                }}
              >
                <span style={{ fontSize: 12, fontWeight: 600, color: 'rgba(0,0,0,.7)' }}>
                  {t('teams.grid.rowsHeader')}
                </span>
                <span style={{ fontSize: 11, color: 'rgba(0,0,0,.4)' }}>
                  {t(`teams.grain.${grid.grain}`)}
                </span>
              </div>
              {buckets.map((b) => {
                const o = overall[b.idx] ?? EMPTY_LOAD;
                const c = loadColor(o.pct, bands);
                const isToday = b.idx === todayIdx;
                return (
                  <div
                    key={b.idx}
                    title={`${bucketTooltip(b)} · ${t('teams.grid.overallLoad')} ${o.empty ? '—' : `${Math.round(o.pct)}%`}`}
                    style={{
                      width: cellW,
                      flex: `0 0 ${cellW}px`,
                      height: headWeekH,
                      background: headBg,
                      borderBottom: '1px solid #e8e8e8',
                      boxShadow: isToday ? 'inset 2px 0 0 #1677ff' : 'inset -1px 0 0 #f0f0f0',
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
                        color: isToday ? '#1677ff' : 'rgba(0,0,0,.5)',
                        fontWeight: isToday ? 600 : 400,
                        fontVariantNumeric: 'tabular-nums',
                        whiteSpace: 'nowrap',
                      }}
                    >
                      {b.label}
                    </span>
                    {isToday && <span style={{ fontSize: 8, color: '#1677ff', fontWeight: 600 }}>{t('teams.grid.today')}</span>}
                    <div
                      style={{
                        width: '100%',
                        height: 8,
                        background: o.empty ? '#f0f0f0' : c.solid,
                        boxShadow: 'inset 0 1px 0 rgba(255,255,255,.4)',
                      }}
                    />
                  </div>
                );
              })}
            </div>
          </div>

          {/* ── Body ── */}
          {teams.map((team) => {
            const teamId = team.id ?? '';
            const isOpen = expanded.has(teamId);
            const tl = teamLoads[teamId] ?? [];
            const members = membersByTeam[teamId] ?? [];
            const nowLoad = todayIdx >= 0 ? tl[todayIdx] ?? EMPTY_LOAD : EMPTY_LOAD;
            const nowC = loadColor(nowLoad.pct, bands);
            return (
              <Fragment key={teamId}>
                {/* team row */}
                <div style={{ display: 'flex', background: '#fff', borderBottom: '1px solid #f0f0f0' }}>
                  <div
                    style={{
                      position: 'sticky',
                      left: 0,
                      zIndex: 20,
                      width: NAME_W,
                      flex: `0 0 ${NAME_W}px`,
                      height: teamH,
                      background: '#fff',
                      borderRight: '1px solid #e8e8e8',
                      display: 'flex',
                      alignItems: 'center',
                      gap: 8,
                      padding: '0 10px',
                    }}
                  >
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
                    <div style={{ flex: 1, minWidth: 0 }}>
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
                    {!nowLoad.empty && (
                      <span
                        title={t('teams.grid.currentLoad', {
                          value: Number.isFinite(nowLoad.pct) ? Math.round(nowLoad.pct) : '∞',
                        })}
                        style={{
                          flexShrink: 0,
                          minWidth: 42,
                          height: 22,
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
                        {Number.isFinite(nowLoad.pct) ? `${Math.round(nowLoad.pct)}%` : '∞'}
                      </span>
                    )}
                  </div>
                  {buckets.map((b) => (
                    <HeatCell
                      key={b.idx}
                      load={tl[b.idx] ?? EMPTY_LOAD}
                      w={cellW}
                      h={teamH}
                      big
                      showText={showCellText}
                      isToday={b.idx === todayIdx}
                      bands={bands}
                      tooltip={bucketTooltip(b)}
                      name={team.name ?? '—'}
                    />
                  ))}
                </div>

                {/* person rows */}
                {isOpen &&
                  members.map((pid) => {
                    const p = grid.resourcesById[pid];
                    if (!p) return null;
                    const pl = personLoads[pid] ?? [];
                    const role = p.roleId ? grid.roleNameById[p.roleId] ?? '' : '';
                    return (
                      <div
                        key={pid}
                        style={{ display: 'flex', background: '#fff', borderBottom: '1px solid #f5f5f5' }}
                      >
                        <div
                          style={{
                            position: 'sticky',
                            left: 0,
                            zIndex: 20,
                            width: NAME_W,
                            flex: `0 0 ${NAME_W}px`,
                            height: personH,
                            background: '#fff',
                            borderRight: '1px solid #e8e8e8',
                            display: 'flex',
                            alignItems: 'center',
                            gap: 9,
                            padding: '0 10px 0 28px',
                          }}
                        >
                          <InitialsAvatar name={p.name ?? '?'} seed={pid} size={24} style={{ fontSize: 10 }} />
                          <div style={{ flex: 1, minWidth: 0 }}>
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
                        </div>
                        {buckets.map((b) => (
                          <HeatCell
                            key={b.idx}
                            load={pl[b.idx] ?? EMPTY_LOAD}
                            w={cellW}
                            h={personH}
                            showText={showCellText}
                            isToday={b.idx === todayIdx}
                            bands={bands}
                            tooltip={bucketTooltip(b)}
                            name={p.name ?? '—'}
                          />
                        ))}
                      </div>
                    );
                  })}

                {/* add-member row */}
                {isOpen && (
                  <div style={{ display: 'flex', background: '#fcfcfc', borderBottom: '1px solid #f0f0f0' }}>
                    <div
                      style={{
                        position: 'sticky',
                        left: 0,
                        zIndex: 20,
                        width: NAME_W,
                        flex: `0 0 ${NAME_W}px`,
                        height: personH,
                        background: '#fcfcfc',
                        borderRight: '1px solid #e8e8e8',
                        display: 'flex',
                        alignItems: 'center',
                        gap: 8,
                        padding: '0 10px 0 28px',
                      }}
                    >
                      <MemberPopover team={team} grid={grid} onAssign={onAssign} assigningId={assigningId} />
                      <span style={{ fontSize: 12, color: 'rgba(0,0,0,.5)' }}>
                        {members.length === 0
                          ? t('teams.grid.emptyTeam')
                          : t('teams.grid.manageMembers')}
                      </span>
                    </div>
                    <div
                      style={{
                        width: buckets.length * cellW,
                        flex: `0 0 ${buckets.length * cellW}px`,
                        height: personH,
                        backgroundImage:
                          'repeating-linear-gradient(45deg, #fafafa, #fafafa 6px, #f3f3f3 6px, #f3f3f3 12px)',
                      }}
                    />
                  </div>
                )}
              </Fragment>
            );
          })}

          {/* create team row */}
          <div style={{ display: 'flex', background: '#fff' }}>
            <div
              style={{
                position: 'sticky',
                left: 0,
                zIndex: 20,
                width: NAME_W,
                flex: `0 0 ${NAME_W}px`,
                minHeight: 46,
                background: '#fff',
                borderRight: '1px solid #e8e8e8',
                display: 'flex',
                alignItems: 'center',
                padding: '6px 10px',
              }}
            >
              {createRow}
            </div>
            <div style={{ width: buckets.length * cellW, flex: `0 0 ${buckets.length * cellW}px`, background: '#fff' }} />
          </div>
        </div>
      </div>
    </div>
  );
}
