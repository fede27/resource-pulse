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
import {
  WorkWindowPopoverContent,
  type WorkWindowFormValues,
} from './WorkWindowPopover';

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

const HOUR_START = 6;
const HOUR_END = 22;
const HOURS = HOUR_END - HOUR_START;
const PX_PER_HOUR = 40;
const AXIS_WIDTH = 48;

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
  const { token } = theme.useToken();
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
    <div
      style={{
        background: token.colorBgContainer,
        border: `1px solid ${token.colorBorderSecondary}`,
        borderRadius: token.borderRadiusLG,
        padding: 16,
        overflow: 'auto',
      }}
    >
      <div
        style={{
          display: 'grid',
          gridTemplateColumns: `${AXIS_WIDTH}px repeat(7, minmax(0, 1fr))`,
          marginBottom: 4,
        }}
      >
        <div />
        {days.long.map((label, idx) => (
          <div
            key={label}
            style={{
              textAlign: 'center',
              padding: '8px 4px',
              fontSize: 13,
              fontWeight: 500,
              color: idx >= 5 ? token.colorTextTertiary : token.colorText,
            }}
          >
            {label}
          </div>
        ))}
      </div>

      <div
        style={{
          position: 'relative',
          display: 'grid',
          gridTemplateColumns: `${AXIS_WIDTH}px repeat(7, minmax(0, 1fr))`,
          height: HOURS * PX_PER_HOUR,
          border: `1px solid ${token.colorBorderSecondary}`,
          borderRadius: token.borderRadiusSM,
          overflow: 'hidden',
          background: token.colorBgContainer,
        }}
      >
        <div
          style={{
            position: 'relative',
            borderRight: `1px solid ${token.colorBorderSecondary}`,
            background: token.colorFillQuaternary,
          }}
        >
          {Array.from({ length: HOURS + 1 }).map((_, i) => {
            const hour = HOUR_START + i;
            if (hour > HOUR_END) return null;
            return (
              <div
                key={hour}
                style={{
                  position: 'absolute',
                  top: i * PX_PER_HOUR - 8,
                  right: 6,
                  fontSize: 11,
                  color: token.colorTextTertiary,
                  fontVariantNumeric: 'tabular-nums',
                }}
              >
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
              style={{
                position: 'relative',
                borderRight:
                  dayIdx < 6 ? `1px solid ${token.colorBorderSecondary}` : 'none',
                background: isWeekend ? 'rgba(0,0,0,.015)' : 'transparent',
                cursor: 'crosshair',
              }}
            >
              {Array.from({ length: HOURS }).map((_, i) => (
                <div
                  key={i}
                  style={{
                    position: 'absolute',
                    left: 0,
                    right: 0,
                    top: (i + 1) * PX_PER_HOUR,
                    borderTop: `1px solid ${token.colorSplit}`,
                    pointerEvents: 'none',
                  }}
                />
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
                  <div style={{ position: 'absolute', top: 0, left: 0, width: 1, height: 1 }} />
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

      <div
        style={{
          marginTop: 12,
          display: 'flex',
          gap: 16,
          fontSize: 12,
          color: token.colorTextTertiary,
          flexWrap: 'wrap',
        }}
      >
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
        <span style={{ marginLeft: 'auto' }}>
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
          style={{
            position: 'absolute',
            top,
            left: 4,
            right: 4,
            height,
            background: palette.bg,
            border: `1px solid ${palette.border}`,
            borderLeft: `3px solid ${palette.accent}`,
            borderRadius: token.borderRadiusSM,
            padding: '4px 6px',
            cursor: 'pointer',
            color: palette.fg,
            overflow: 'hidden',
          }}
        >
          <div
            style={{
              fontSize: 12,
              fontWeight: 500,
              fontVariantNumeric: 'tabular-nums',
              whiteSpace: 'nowrap',
              overflow: 'hidden',
              textOverflow: 'ellipsis',
            }}
          >
            {formatHourMinute(w.startTime)}–{formatHourMinute(w.endTime)}
          </div>
          {height >= 36 && kind === 'future' && w.validFrom && (
            <div style={{ fontSize: 10, marginTop: 2, opacity: 0.9 }}>{fromLabel(w.validFrom)}</div>
          )}
          {height >= 36 && kind === 'past' && w.validTo && (
            <div style={{ fontSize: 10, marginTop: 2, opacity: 0.9 }}>
              {untilLabel(w.validTo)}
            </div>
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
  const palette = paletteFor(kind, token);
  return (
    <span style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}>
      <span
        style={{
          display: 'inline-block',
          width: 12,
          height: 12,
          background: palette.bg,
          border: `1px solid ${palette.border}`,
          borderLeft: `3px solid ${palette.accent}`,
          borderRadius: 2,
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
