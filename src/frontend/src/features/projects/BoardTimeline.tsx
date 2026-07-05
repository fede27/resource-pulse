import type { ReactNode, RefObject } from 'react';
import { LockOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import type { BoardGeo } from './timelineGeo';
import {
  FENCE_EDGE_SOFT,
  FENCE_EDGE_STRONG,
  FENCE_FROZEN_BG,
  FENCE_FROZEN_TEXT,
  FENCE_LIQUID_BG,
  FENCE_LIQUID_TEXT,
  FENCE_SLUSHY_BG,
  FENCE_SLUSHY_TEXT,
  FENCE_TINT_FROZEN,
  FENCE_TINT_SLUSHY,
} from './boardColors';
import { LEFT_W, PAST_HATCH, PAST_HATCH_STRONG, useStyles } from './BoardTimeline.styles';

export type BoardTimelineProps = {
  geo: BoardGeo;
  scrollRef: RefObject<HTMLDivElement | null>;
  isEmpty: boolean;
  emptyContent: ReactNode;
  children: ReactNode;
};

// The scrollable timeline shell: 3-row header (major bands / time-fence /
// unit ticks) + a backdrop (past hatch, fence tints, gridlines, today line)
// behind the project rows. Left labels stay pinned via sticky positioning.
export function BoardTimeline({ geo, scrollRef, isEmpty, emptyContent, children }: BoardTimelineProps) {
  const { t } = useTranslation();
  const { styles, cx } = useStyles();

  const fenceSegments: Array<{
    key: 'frozen' | 'slushy' | 'liquid';
    left: number;
    right: number;
    bg: string;
    fg: string;
    icon: boolean;
    edge: boolean;
  }> = [
    {
      key: 'frozen',
      left: geo.todayX,
      right: geo.frozenX,
      bg: FENCE_FROZEN_BG,
      fg: FENCE_FROZEN_TEXT,
      icon: true,
      edge: true,
    },
    {
      key: 'slushy',
      left: geo.frozenX,
      right: geo.slushyX,
      bg: FENCE_SLUSHY_BG,
      fg: FENCE_SLUSHY_TEXT,
      icon: false,
      edge: true,
    },
    {
      key: 'liquid',
      left: geo.slushyX,
      right: geo.contentW,
      bg: FENCE_LIQUID_BG,
      fg: FENCE_LIQUID_TEXT,
      icon: false,
      edge: false,
    },
  ];

  // The tick containing today (week/day emphasis).
  const isTodayTick = (x: number, w: number) => geo.todayIn && geo.todayX >= x && geo.todayX < x + w;

  return (
    <div className={styles.frame}>
      <div ref={scrollRef} className={styles.scroll}>
        {/* dynamic: total width = sticky label column + computed axis width. */}
        <div style={{ width: LEFT_W + geo.contentW, minWidth: LEFT_W + geo.contentW }}>
          <div className={styles.header}>
            <div className={styles.headerLeft}>
              <div className={styles.headerLeftTitle}>{t('projects.timeline.header')}</div>
              <div className={styles.headerLeftFence} />
              <div className={styles.headerLeftUnit}>{t(`projects.timeline.unit.${geo.bucket}`)}</div>
            </div>

            {/* dynamic: axis width computed from the domain. */}
            <div className={styles.axis} style={{ width: geo.contentW }}>
              <div className={styles.majorRow}>
                {geo.majorBands.map((m, i) => (
                  // dynamic: band position/width computed from the domain.
                  <div
                    key={i}
                    className={styles.majorBand}
                    style={{ left: m.x, width: m.w, borderInlineStart: i === 0 ? 'none' : undefined }}
                  >
                    <span>{m.label}</span>
                  </div>
                ))}
              </div>

              <div className={styles.fenceRow}>
                {geo.todayX > 1 && (
                  // dynamic: past-zone width up to today.
                  <div
                    className={styles.fenceSeg}
                    style={{ left: 0, width: geo.todayX, background: PAST_HATCH_STRONG }}
                  />
                )}
                {fenceSegments.map((s) => {
                  const w = s.right - s.left;
                  if (w <= 1) return null;
                  return (
                    // dynamic: fence segment geometry + zone colours.
                    <div
                      key={s.key}
                      className={styles.fenceSeg}
                      style={{
                        left: s.left,
                        width: w,
                        background: s.bg,
                        borderRight: s.edge ? `1px solid ${FENCE_EDGE_STRONG}` : 'none',
                      }}
                    >
                      {w > 46 && (
                        <span style={{ color: s.fg }}>
                          {s.icon && w > 90 && <LockOutlined />}
                          {t(`projects.legend.${s.key}`)}
                        </span>
                      )}
                    </div>
                  );
                })}
              </div>

              <div className={styles.ticksRow}>
                {geo.unitTicks.map((tk, i) => (
                  // dynamic: tick geometry computed from the domain.
                  <div
                    key={i}
                    className={cx(
                      styles.tick,
                      tk.isWeekend && styles.tickFaded,
                      isTodayTick(tk.x, tk.w) && geo.bucket !== 'month' && styles.tickToday,
                    )}
                    style={{ left: tk.x, width: tk.w, borderInlineStart: i === 0 ? 'none' : undefined }}
                  >
                    <span>{tk.label}</span>
                  </div>
                ))}
                {geo.todayIn && (
                  <>
                    {/* dynamic: today marker at computed x. */}
                    <div className={styles.todayLine} style={{ left: geo.todayX }} />
                    <div className={styles.todayPill} style={{ left: geo.todayX }}>
                      <span>{t('projects.timeline.today')}</span>
                    </div>
                  </>
                )}
              </div>
            </div>
          </div>

          {isEmpty ? (
            <div className={styles.emptyWrap}>{emptyContent}</div>
          ) : (
            <div className={styles.body}>
              {/* dynamic: backdrop sits behind the axis area only. */}
              <div className={styles.backdrop} style={{ left: LEFT_W, width: geo.contentW }}>
                {geo.todayX > 0 && (
                  // dynamic: past hatch width up to today.
                  <div
                    style={{ position: 'absolute', top: 0, bottom: 0, left: 0, width: geo.todayX, background: PAST_HATCH }}
                  />
                )}
                {/* dynamic: fence tints between computed boundaries. */}
                <div
                  style={{
                    position: 'absolute',
                    top: 0,
                    bottom: 0,
                    left: geo.todayX,
                    width: Math.max(0, geo.frozenX - geo.todayX),
                    background: FENCE_TINT_FROZEN,
                  }}
                />
                <div
                  style={{
                    position: 'absolute',
                    top: 0,
                    bottom: 0,
                    left: geo.frozenX,
                    width: Math.max(0, geo.slushyX - geo.frozenX),
                    background: FENCE_TINT_SLUSHY,
                  }}
                />
                {geo.unitTicks.map((tk, i) => {
                  if (tk.x <= 0.5) return null;
                  const bg = tk.isMonthStart
                    ? 'rgba(0,0,0,.08)'
                    : tk.isMonday
                      ? 'rgba(0,0,0,.05)'
                      : 'rgba(0,0,0,.03)';
                  // dynamic: gridline position + weight from tick metadata.
                  return <div key={i} className={styles.gridline} style={{ left: tk.x, background: bg }} />;
                })}
                {geo.frozenX > 0 && geo.frozenX < geo.contentW && (
                  // dynamic: fence boundary line at computed x.
                  <div className={styles.gridline} style={{ left: geo.frozenX, background: FENCE_EDGE_STRONG }} />
                )}
                {geo.slushyX > 0 && geo.slushyX < geo.contentW && (
                  // dynamic: fence boundary line at computed x.
                  <div className={styles.gridline} style={{ left: geo.slushyX, background: FENCE_EDGE_SOFT }} />
                )}
                {geo.todayIn && (
                  // dynamic: today line at computed x.
                  <div className={styles.todayLine} style={{ left: geo.todayX }} />
                )}
              </div>
              <div className={styles.rows}>{children}</div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
