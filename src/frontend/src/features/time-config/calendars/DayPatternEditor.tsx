import { useState } from 'react';
import { Card, Dropdown, Popover, Typography } from 'antd';
import { CopyOutlined } from '@ant-design/icons';
import type { MenuProps } from 'antd';
import { useTranslation } from 'react-i18next';
import type { DayOfWeek, WorkWindowDto } from '@/api/generated/schemas';
import { useDays } from '@/i18n/useDays';
import {
  columnIndexToDayOfWeek,
  filterWindowsByView,
  formatHourMinute,
  minutesToTime,
  timeToMinutes,
  windowKindForView,
  type WindowView,
} from './workWindow.utils';
import { WorkWindowPopoverContent } from './WorkWindowPopover';
import type { WorkWindowFormValues } from './workWindowForm';
import { useStyles } from './DayPatternEditor.styles';

const { Text } = Typography;

export type DayPatternEditorProps = {
  windows: WorkWindowDto[];
  /** Validity scope, shared with the week grid so pills follow the same filter. */
  view: WindowView;
  saving: boolean;
  deleting: boolean;
  onCreate: (values: WorkWindowFormValues) => Promise<void> | void;
  onUpdate: (id: string, values: WorkWindowFormValues) => Promise<void> | void;
  onDelete: (id: string) => Promise<void> | void;
  onCopyDay: (
    source: DayOfWeek,
    targets: DayOfWeek[],
    view: WindowView,
  ) => Promise<void> | void;
};

type OpenState =
  | { kind: 'closed' }
  | { kind: 'create'; dayIdx: number; seed: Partial<WorkWindowDto> }
  | { kind: 'edit'; windowId: string };

// Slots start no later than 20:00 and end no later than 22:00 — matches the
// week grid's visible window (HOUR_END).
const DAY_MAX_START_MIN = 20 * 60;
const DAY_MAX_END_MIN = 22 * 60;

export function DayPatternEditor({
  windows,
  view,
  saving,
  deleting,
  onCreate,
  onUpdate,
  onDelete,
  onCopyDay,
}: DayPatternEditorProps) {
  const { t } = useTranslation();
  const { styles, cx } = useStyles();
  const days = useDays();
  const [open, setOpen] = useState<OpenState>({ kind: 'closed' });

  const close = () => setOpen({ kind: 'closed' });
  const shown = filterWindowsByView(windows, view);
  // Add and copy work in every view and stay scoped to it: a copy preserves each
  // window's validity and a new slot inherits the scope's validity, so a
  // future-view edit lands in the future (e.g. planning a part-time change).
  // Pills open inspect-first outside "today" (validity matters there); "today"
  // keeps the quick-edit.
  const chipStartMode = view === 'today' ? 'edit' : 'inspect';
  const subtitle =
    view === 'future'
      ? t('timeConfig.calendars.dayEditor.subtitleFuture')
      : view === 'historical'
        ? t('timeConfig.calendars.dayEditor.subtitleHistorical')
        : view === 'all'
          ? t('timeConfig.calendars.dayEditor.subtitleAll')
          : t('timeConfig.calendars.dayEditor.subtitleToday');

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
    <Card
      size="small"
      styles={{ body: { padding: 0 } }}
      title={
        <div>
          <Text strong>{t('timeConfig.calendars.dayEditor.title')}</Text>{' '}
          <span className={styles.headerSub}>· {subtitle}</span>
        </div>
      }
    >
      {Array.from({ length: 7 }).map((_, idx) => {
        const dow = columnIndexToDayOfWeek(idx);
        const dayLabel = days.long[idx] ?? '';
        const windowsOfDay = shown
          .filter((w) => w.dayOfWeek === dow)
          .sort((a, b) => timeToMinutes(a.startTime) - timeToMinutes(b.startTime));
        const hasWindows = windowsOfDay.length > 0;
        const isWeekend = idx >= 5;
        const dayMinutes = windowsOfDay.reduce(
          (acc, w) => acc + Math.max(0, timeToMinutes(w.endTime) - timeToMinutes(w.startTime)),
          0,
        );
        const dayHours = dayMinutes / 60;

        const last = windowsOfDay[windowsOfDay.length - 1];
        const seedStartMin = last
          ? Math.min(timeToMinutes(last.endTime) + 60, DAY_MAX_START_MIN)
          : 9 * 60;
        const seed: Partial<WorkWindowDto> = {
          dayOfWeek: dow,
          startTime: minutesToTime(seedStartMin),
          endTime: minutesToTime(Math.min(seedStartMin + 240, DAY_MAX_END_MIN)),
        };
        // Outside "today" a new slot inherits the scope's validity (the day's own
        // windows first, else any shown window) so it lands in the same period
        // as the change being planned.
        if (view !== 'today') {
          const validitySource = last ?? shown[0];
          if (validitySource?.validFrom) seed.validFrom = validitySource.validFrom;
          if (validitySource?.validTo) seed.validTo = validitySource.validTo;
        }

        const copyMenu: MenuProps['items'] = [
          {
            key: 'weekdays',
            label: t('timeConfig.calendars.dayEditor.applyToWeekdays'),
            disabled: !hasWindows,
            onClick: () =>
              void onCopyDay(
                dow,
                [1, 2, 3, 4, 5]
                  .map((i) => columnIndexToDayOfWeek(i - 1))
                  .filter((d) => d !== dow),
                view,
              ),
          },
          { type: 'divider' },
          ...Array.from({ length: 7 })
            .map((__, targetIdx) => targetIdx)
            .filter((targetIdx) => targetIdx !== idx)
            .map((targetIdx) => ({
              key: `day-${targetIdx}`,
              label: t('timeConfig.calendars.dayEditor.applyToDay', {
                day: days.long[targetIdx] ?? '',
              }),
              disabled: !hasWindows,
              onClick: () => void onCopyDay(dow, [columnIndexToDayOfWeek(targetIdx)], view),
            })),
        ];

        return (
          <div
            key={idx}
            className={cx(styles.row, isWeekend && !hasWindows && styles.rowEmptyWeekend)}
          >
            <span className={cx(styles.dayLabel, !hasWindows && styles.dayLabelEmpty)}>
              {dayLabel}
            </span>

            <div className={styles.chips}>
              {windowsOfDay.map((w) => {
                const kind = windowKindForView(w, view);
                const kindClass =
                  kind === 'future'
                    ? styles.chipFuture
                    : kind === 'past'
                      ? styles.chipPast
                      : styles.chipActive;
                return (
                  <Popover
                    key={w.id ?? `${w.startTime}-${w.endTime}`}
                    open={open.kind === 'edit' && open.windowId === (w.id ?? '')}
                    trigger="click"
                    placement="bottomLeft"
                    destroyOnHidden
                    onOpenChange={(v) => {
                      if (!v) close();
                    }}
                    content={
                      <WorkWindowPopoverContent
                        initial={w}
                        saving={saving}
                        deleting={deleting}
                        startMode={chipStartMode}
                        onSubmit={(values) => {
                          if (w.id) void submitUpdate(w.id, values);
                        }}
                        onCancel={close}
                        onDelete={() => {
                          if (w.id) void submitDelete(w.id);
                        }}
                      />
                    }
                  >
                    <button
                      type="button"
                      className={cx(styles.chip, kindClass)}
                      onClick={() => {
                        if (w.id) setOpen({ kind: 'edit', windowId: w.id });
                      }}
                    >
                      {formatHourMinute(w.startTime)}–{formatHourMinute(w.endTime)}
                    </button>
                  </Popover>
                );
              })}

              {!hasWindows && (
                <span className={styles.nonWorking}>
                  {view === 'today' || view === 'all'
                    ? t('timeConfig.calendars.dayEditor.nonWorking')
                    : '—'}
                </span>
              )}

              <Popover
                open={open.kind === 'create' && open.dayIdx === idx}
                trigger="click"
                placement="bottomLeft"
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
                <button
                  type="button"
                  className={styles.addChip}
                  onClick={() => setOpen({ kind: 'create', dayIdx: idx, seed })}
                >
                  + {t('timeConfig.calendars.dayEditor.addSlot')}
                </button>
              </Popover>
            </div>

            <span className={cx(styles.hours, !hasWindows && styles.hoursEmpty)}>
              {hasWindows ? `${formatHours(dayHours)}h` : '—'}
            </span>

            <Dropdown
              placement="bottomRight"
              trigger={['click']}
              disabled={!hasWindows}
              menu={{ items: copyMenu }}
            >
              <span
                className={cx(styles.copyTrigger, !hasWindows && styles.copyTriggerDisabled)}
                title={t('timeConfig.calendars.dayEditor.copyTooltip')}
              >
                <CopyOutlined />
              </span>
            </Dropdown>
          </div>
        );
      })}
    </Card>
  );
}

function formatHours(h: number): string {
  return h % 1 === 0 ? String(h) : h.toFixed(1);
}
