import { useMemo, useState, type ReactNode } from 'react';
import { theme, Tooltip, Popover } from 'antd';
import type { GlobalToken } from 'antd';
import { useTranslation } from 'react-i18next';
import dayjs from 'dayjs';
import type { WorkWindowDto } from '@/api/generated/schemas';
import { useDays } from '@/i18n/useDays';
import {
  columnIndexToDayOfWeek,
  formatHourMinute,
  isWindowActiveToday,
  isWindowFuture,
  isWindowHistorical,
  timeToMinutes,
} from './workWindow.utils';
import { WorkWindowPopoverContent } from './WorkWindowPopover';
import type { WorkWindowFormValues } from './workWindowForm';
import { HOUR_END, HOUR_START, HOURS, PX_PER_HOUR, useStyles } from './WeekGrid.styles';

export type WeekGridView = 'today' | 'all' | 'historical' | 'future';

export type WeekGridProps = {
  windows: WorkWindowDto[];
  view: WeekGridView;
  saving: boolean;
  deleting: boolean;
  onCreate: (values: WorkWindowFormValues) => Promise<void> | void;
  onUpdate: (id: string, values: WorkWindowFormValues) => Promise<void> | void;
  onDelete: (id: string) => Promise<void> | void;
};

type OpenState =
  | { kind: 'closed' }
  | { kind: 'create'; dayIdx: number; seed: Partial<WorkWindowDto> }
  | { kind: 'edit'; windowId: string };

function windowKind(w: WorkWindowDto, view: WeekGridView): 'active' | 'past' | 'future' {
  if (view === 'all') {
    if (isWindowHistorical(w)) return 'past';
    if (isWindowFuture(w)) return 'future';
    return 'active';
  }
  return view === 'historical' ? 'past' : view === 'future' ? 'future' : 'active';
}

export function WeekGrid({
  windows,
  view,
  saving,
  deleting,
  onCreate,
  onUpdate,
  onDelete,
}: WeekGridProps) {
  const { t } = useTranslation();
  const { styles, cx } = useStyles();
  const days = useDays();
  const [open, setOpen] = useState<OpenState>({ kind: 'closed' });

  const filtered = useMemo(() => {
    if (view === 'today') return windows.filter(isWindowActiveToday);
    if (view === 'historical') return windows.filter(isWindowHistorical);
    if (view === 'future') return windows.filter(isWindowFuture);
    return windows;
  }, [windows, view]);

  const close = () => setOpen({ kind: 'closed' });

  const handleColumnClick = (dayIdx: number, evt: React.MouseEvent<HTMLDivElement>) => {
    const target = evt.currentTarget;
    const rect = target.getBoundingClientRect();
    const y = evt.clientY - rect.top;
    const startMin = Math.max(0, Math.round(y / (PX_PER_HOUR / 60) / 30) * 30);
    const startHour = HOUR_START + Math.floor(startMin / 60);
    const startMm = startMin % 60;
    if (startHour >= HOUR_END) return;
    const endHour = Math.min(HOUR_END, startHour + 1);
    const seed: Partial<WorkWindowDto> = {
      dayOfWeek: columnIndexToDayOfWeek(dayIdx),
      startTime: `${pad(startHour)}:${pad(startMm)}:00`,
      endTime: `${pad(endHour)}:${pad(startMm)}:00`,
    };
    setOpen({ kind: 'create', dayIdx, seed });
  };

  const submitCreate = async (values: WorkWindowFormValues) => {
    await onCreate(values);
    close();
  };
  const submitUpdate = async (id: string, values: WorkWindowFormValues) => {
    await onUpdate(id, values);
    close();
  };
  const submitDelete = async (id: string) => {
    await onDelete(id);
    close();
  };

  return (
    <div className={styles.root}>
      <div className={styles.headerRow}>
        <div />
        {days.long.map((label, idx) => (
          <div
            key={label}
            className={cx(styles.dayHead, idx >= 5 && styles.dayHeadWeekend)}
          >
            {label}
          </div>
        ))}
      </div>

      <div className={styles.body}>
        <div className={styles.axis}>
          {Array.from({ length: HOURS + 1 }).map((_, i) => {
            const hour = HOUR_START + i;
            if (hour > HOUR_END) return null;
            return (
              // dynamic: vertical offset is the hour's position on the time axis.
              <div key={hour} className={styles.hourLabel} style={{ top: i * PX_PER_HOUR - 8 }}>
                {pad(hour)}:00
              </div>
            );
          })}
        </div>

        {Array.from({ length: 7 }).map((_, dayIdx) => {
          const dow = columnIndexToDayOfWeek(dayIdx);
          const windowsOfDay = filtered.filter((w) => w.dayOfWeek === dow);
          const isWeekend = dayIdx >= 5;
          const showCreatePop = open.kind === 'create' && open.dayIdx === dayIdx;

          return (
            <div
              key={dayIdx}
              onClick={(e) => handleColumnClick(dayIdx, e)}
              className={cx(
                styles.dayCol,
                dayIdx === 6 && styles.dayColLast,
                isWeekend && styles.dayColWeekend,
              )}
            >
              {Array.from({ length: HOURS }).map((_, i) => (
                // dynamic: gridline offset is the hour boundary position.
                <div key={i} className={styles.gridLine} style={{ top: (i + 1) * PX_PER_HOUR }} />
              ))}

              {showCreatePop && (
                <Popover
                  open
                  trigger="click"
                  placement="rightTop"
                  destroyOnHidden
                  onOpenChange={(v) => {
                    if (!v) close();
                  }}
                  content={
                    <WorkWindowPopoverContent
                      initial={open.kind === 'create' ? open.seed : null}
                      saving={saving}
                      deleting={false}
                      onSubmit={submitCreate}
                      onCancel={close}
                    />
                  }
                >
                  <div className={styles.popAnchor} />
                </Popover>
              )}

              {windowsOfDay.map((w) => (
                <WindowBlock
                  key={w.id ?? `${w.startTime}-${w.endTime}-${w.validFrom}`}
                  w={w}
                  view={view}
                  fromLabel={(date) =>
                    t('timeConfig.calendars.grid.from', { date: formatValidityDate(date) })
                  }
                  untilLabel={(date) =>
                    t('timeConfig.calendars.grid.until', { date: formatValidityDate(date) })
                  }
                  isOpen={open.kind === 'edit' && open.windowId === (w.id ?? '')}
                  onOpen={() => {
                    if (w.id) setOpen({ kind: 'edit', windowId: w.id });
                  }}
                  onClose={close}
                  popoverContent={
                    <WorkWindowPopoverContent
                      initial={w}
                      saving={saving}
                      deleting={deleting}
                      onSubmit={(values) => {
                        if (w.id) void submitUpdate(w.id, values);
                      }}
                      onCancel={close}
                      onDelete={() => {
                        if (w.id) void submitDelete(w.id);
                      }}
                    />
                  }
                />
              ))}
            </div>
          );
        })}
      </div>

      <div className={styles.legend}>
        <LegendSwatch label={t('timeConfig.calendars.grid.legendActive')} kind="active" />
        {view === 'all' && (
          <>
            <LegendSwatch
              label={t('timeConfig.calendars.grid.legendFuture')}
              kind="future"
            />
            <LegendSwatch label={t('timeConfig.calendars.grid.legendPast')} kind="past" />
          </>
        )}
        <span className={styles.legendHint}>
          {t('timeConfig.calendars.grid.emptyAreaHint')}
        </span>
      </div>
    </div>
  );
}

type WindowBlockProps = {
  w: WorkWindowDto;
  view: WeekGridView;
  isOpen: boolean;
  popoverContent: ReactNode;
  fromLabel: (date: string) => string;
  untilLabel: (date: string) => string;
  onOpen: () => void;
  onClose: () => void;
};

function WindowBlock({
  w,
  view,
  isOpen,
  popoverContent,
  fromLabel,
  untilLabel,
  onOpen,
  onClose,
}: WindowBlockProps) {
  const { token } = theme.useToken();
  const { styles } = useStyles();
  const kind = windowKind(w, view);
  const palette = paletteFor(kind, token);
  const startMin = timeToMinutes(w.startTime) - HOUR_START * 60;
  const endMin = timeToMinutes(w.endTime) - HOUR_START * 60;
  const top = (startMin / 60) * PX_PER_HOUR;
  const height = Math.max(20, ((endMin - startMin) / 60) * PX_PER_HOUR);

  const tooltipTitle = `${formatHourMinute(w.startTime)}–${formatHourMinute(w.endTime)}${
    w.validTo ? ` · ${untilLabel(w.validTo)}` : ''
  }`;

  return (
    <Popover
      open={isOpen}
      trigger="click"
      placement="rightTop"
      destroyOnHidden
      content={popoverContent}
      onOpenChange={(v) => {
        if (!v) onClose();
      }}
    >
      <Tooltip title={tooltipTitle} mouseEnterDelay={0.4}>
        <div
          onClick={(e) => {
            e.stopPropagation();
            onOpen();
          }}
          className={styles.block}
          // dynamic: vertical position/height come from the window's times; the
          // colours are the validity-state palette (token-derived).
          style={{
            top,
            height,
            background: palette.bg,
            border: `1px solid ${palette.border}`,
            borderLeft: `3px solid ${palette.accent}`,
            color: palette.fg,
          }}
        >
          <div className={styles.blockTime}>
            {formatHourMinute(w.startTime)}–{formatHourMinute(w.endTime)}
          </div>
          {height >= 36 && kind === 'future' && w.validFrom && (
            <div className={styles.blockMeta}>{fromLabel(w.validFrom)}</div>
          )}
          {height >= 36 && kind === 'past' && w.validTo && (
            <div className={styles.blockMeta}>{untilLabel(w.validTo)}</div>
          )}
        </div>
      </Tooltip>
    </Popover>
  );
}

function formatValidityDate(iso: string): string {
  return dayjs(iso).format('D MMM YYYY');
}

function LegendSwatch({ label, kind }: { label: string; kind: 'active' | 'past' | 'future' }) {
  const { token } = theme.useToken();
  const { styles } = useStyles();
  const palette = paletteFor(kind, token);
  return (
    <span className={styles.legendSwatchWrap}>
      <span
        className={styles.legendSwatch}
        // dynamic: swatch colours mirror the validity-state palette.
        style={{
          background: palette.bg,
          border: `1px solid ${palette.border}`,
          borderLeft: `3px solid ${palette.accent}`,
        }}
      />
      {label}
    </span>
  );
}

type Palette = { bg: string; border: string; accent: string; fg: string };

function paletteFor(kind: 'active' | 'past' | 'future', token: GlobalToken): Palette {
  if (kind === 'past') {
    return {
      bg: token.colorFillQuaternary,
      border: token.colorBorderSecondary,
      accent: token.colorTextTertiary,
      fg: token.colorTextSecondary,
    };
  }
  if (kind === 'future') {
    return {
      bg: token.colorWarningBg,
      border: token.colorWarningBorder,
      accent: token.colorWarning,
      fg: token.colorWarningText,
    };
  }
  return {
    bg: token.colorPrimaryBg,
    border: token.colorPrimaryBorder,
    accent: token.colorPrimary,
    fg: token.colorPrimaryText,
  };
}

function pad(n: number): string {
  return String(n).padStart(2, '0');
}
