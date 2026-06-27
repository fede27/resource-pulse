import { useState } from 'react';
import { App, InputNumber, Select, theme } from 'antd';
import { createStyles } from 'antd-style';
import { useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import {
  getTimeFenceGetQueryKey,
  useTimeFenceUpdate,
} from '@/api/generated/time-fence/time-fence';
import {
  DurationUnit,
  type DurationDto,
  type TimeFenceConfigurationDto,
} from '@/api/generated/schemas';
import { useApiError } from '@/lib/errors';
import { ConfigCard } from './ConfigCard';
import { ConstantNote } from './ConstantNote';
import {
  addDuration,
  durationLabel,
  durationToDays,
  durationUnitKey,
  today,
  type Duration,
} from './helpers';

const nowTime = () =>
  new Date().toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' });

type Fence = { frozenHorizon: Duration; slushyHorizon: Duration };

const useStyles = createStyles(({ token, css }) => ({
  timelineWrap: css`
    margin-block-end: 20px;
  `,
  track: css`
    position: relative;
    height: 56px;
    border-radius: ${token.borderRadius}px;
    overflow: hidden;
    display: flex;
    border: 1px solid ${token.colorBorderSecondary};
  `,
  zone: css`
    display: flex;
    flex-direction: column;
    justify-content: center;
    padding: 0 ${token.paddingSM}px;
    min-width: 0;
  `,
  zoneLabel: css`
    font-size: ${token.fontSizeSM}px;
    font-weight: 600;
  `,
  zoneSub: css`
    font-size: 11px;
    color: ${token.colorTextTertiary};
  `,
  invalidTrack: css`
    display: flex;
    align-items: center;
    justify-content: center;
    width: 100%;
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorError};
  `,
  axis: css`
    position: relative;
    height: 24px;
    margin-block-start: ${token.marginXXS}px;
    font-size: 11px;
    color: ${token.colorTextTertiary};
    font-variant-numeric: tabular-nums;
  `,
  axisStart: css`
    position: absolute;
    left: 0;
  `,
  axisMark: css`
    position: absolute;
    transform: translateX(-50%);
    white-space: nowrap;
  `,
  editors: css`
    display: flex;
    gap: ${token.marginXL}px;
    flex-wrap: wrap;
  `,
  editorLabel: css`
    font-size: ${token.fontSizeSM}px;
    font-weight: 500;
    margin-block-end: ${token.marginXXS}px;
    display: inline-flex;
    align-items: center;
    gap: ${token.marginXXS}px;
  `,
  editorDot: css`
    width: 8px;
    height: 8px;
    border-radius: 2px;
  `,
  editorInputs: css`
    display: flex;
    gap: ${token.marginXS}px;
    align-items: center;
  `,
  valueInput: css`
    width: 90px;
  `,
  unitSelect: css`
    width: 130px;
  `,
  editorHint: css`
    font-size: 11px;
    color: ${token.colorTextTertiary};
    margin-block-start: ${token.marginXXS}px;
    max-width: 220px;
    line-height: 1.4;
  `,
  invalidDetail: css`
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorError};
    margin-block-start: ${token.marginSM}px;
  `,
}));

// The generated DurationDto has optional value/unit — normalize to a strict shape.
const toDur = (d?: DurationDto): Duration => ({
  value: d?.value ?? 1,
  unit: d?.unit ?? DurationUnit.Weeks,
});
const toFence = (dto: TimeFenceConfigurationDto): Fence => ({
  frozenHorizon: toDur(dto.frozenHorizon),
  slushyHorizon: toDur(dto.slushyHorizon),
});

const signature = (f: Fence): string =>
  JSON.stringify([f.frozenHorizon.value, f.frozenHorizon.unit, f.slushyHorizon.value, f.slushyHorizon.unit]);

export function TimeFenceCard({ committed }: { committed: TimeFenceConfigurationDto }) {
  const { t } = useTranslation();
  const { styles } = useStyles();
  // Zone/editor accent colours are token values assigned per zone, so we still
  // read the token map here and apply them inline (single-sourced, data-keyed).
  const { token } = theme.useToken();
  const { message } = App.useApp();
  const showApiError = useApiError();
  const queryClient = useQueryClient();

  const base = toFence(committed);
  const [fence, setFence] = useState<Fence>(() => toFence(committed));
  const [savedAt, setSavedAt] = useState<string | null>(null);

  const dirty = signature(fence) !== signature(base);
  const frozenDays = durationToDays(fence.frozenHorizon);
  const slushyDays = durationToDays(fence.slushyHorizon);
  const valid =
    fence.frozenHorizon.value > 0 && fence.slushyHorizon.value > 0 && frozenDays < slushyDays;

  const setPart = (which: 'frozenHorizon' | 'slushyHorizon', patch: Partial<Duration>) =>
    setFence((f) => ({ ...f, [which]: { ...f[which], ...patch } }));

  const mutation = useTimeFenceUpdate({
    mutation: {
      onSuccess: (data) => {
        message.success(t('settings.fence.saveSuccess'));
        setSavedAt(nowTime());
        queryClient.setQueryData(getTimeFenceGetQueryKey(), data);
        queryClient
          .invalidateQueries({ queryKey: getTimeFenceGetQueryKey() })
          .catch(() => undefined);
      },
      onError: (e) => showApiError(e),
    },
  });

  const save = () => {
    if (!valid) return;
    mutation.mutate({ data: { frozenHorizon: fence.frozenHorizon, slushyHorizon: fence.slushyHorizon } });
  };

  const unitOptions = [DurationUnit.Days, DurationUnit.Weeks, DurationUnit.Months].map((u) => ({
    value: u,
    label: t(`settings.fence.units.${durationUnitKey(u)}`),
  }));

  const ref = today();
  const frozenEnd = addDuration(ref, fence.frozenHorizon);
  const slushyEnd = addDuration(ref, fence.slushyHorizon);
  const totalDays = Math.max(slushyDays * 1.5, slushyDays + 30);
  const leftPct = (days: number) => `${Math.min(100, (days / totalDays) * 100)}%`;

  const zones = [
    {
      key: 'frozen',
      label: t('settings.fence.zoneFrozen'),
      color: token.colorPrimary,
      from: 0,
      to: frozenDays,
      dur: fence.frozenHorizon,
    },
    {
      key: 'slushy',
      label: t('settings.fence.zoneSlushy'),
      color: token.colorWarning,
      from: frozenDays,
      to: slushyDays,
      dur: fence.slushyHorizon,
    },
    {
      key: 'liquid',
      label: t('settings.fence.zoneLiquid'),
      color: token.colorTextTertiary,
      from: slushyDays,
      to: totalDays,
      dur: null as Duration | null,
    },
  ];

  const editors: { key: 'frozenHorizon' | 'slushyHorizon'; label: string; color: string; hint: string }[] = [
    { key: 'frozenHorizon', label: t('settings.fence.frozen'), color: token.colorPrimary, hint: t('settings.fence.frozenHint') },
    { key: 'slushyHorizon', label: t('settings.fence.slushy'), color: token.colorWarning, hint: t('settings.fence.slushyHint') },
  ];

  return (
    <ConfigCard
      title={t('settings.fence.title')}
      subtitle={t('settings.fence.subtitle')}
      dirty={dirty}
      valid={valid}
      saving={mutation.isPending}
      savedAt={savedAt}
      onSave={save}
      onReset={() => setFence(base)}
    >
      {/* Timeline */}
      <div className={styles.timelineWrap}>
        <div className={styles.track}>
          {valid ? (
            zones.map((z) => {
              const widthPct = ((z.to - z.from) / totalDays) * 100;
              return (
                <div
                  key={z.key}
                  className={styles.zone}
                  // dynamic: zone width comes from the configured horizon; the
                  // accent colour is the zone's token colour applied per zone.
                  style={{
                    width: `${widthPct}%`,
                    background: z.color + '1f',
                    borderRight: z.key !== 'liquid' ? `2px solid ${z.color}` : 'none',
                  }}
                >
                  <span className={styles.zoneLabel} style={{ color: z.color }}>
                    {z.label}
                  </span>
                  <span className={styles.zoneSub}>
                    {z.dur ? durationLabel(z.dur, t) : t('settings.fence.beyondSlushy')}
                  </span>
                </div>
              );
            })
          ) : (
            <div className={styles.invalidTrack}>{t('settings.fence.invalidShort')}</div>
          )}
        </div>
        {valid && (
          <div className={styles.axis}>
            <span className={styles.axisStart}>↑ {t('settings.fence.today')}</span>
            {/* dynamic: marker offsets are the horizon positions along the axis. */}
            <span className={styles.axisMark} style={{ left: leftPct(frozenDays) }}>
              ↑ {frozenEnd.format('D MMM')}
            </span>
            <span className={styles.axisMark} style={{ left: leftPct(slushyDays) }}>
              ↑ {slushyEnd.format('D MMM')}
            </span>
          </div>
        )}
      </div>

      {/* Duration editors */}
      <div className={styles.editors}>
        {editors.map((p) => (
          <div key={p.key}>
            <div className={styles.editorLabel}>
              {/* dynamic: dot colour is the editor's token accent. */}
              <span className={styles.editorDot} style={{ background: p.color }} />
              {p.label}
            </div>
            <div className={styles.editorInputs}>
              <InputNumber
                value={fence[p.key].value}
                onChange={(v) => setPart(p.key, { value: v ?? 0 })}
                min={1}
                status={!valid ? 'error' : ''}
                className={styles.valueInput}
              />
              <Select<DurationUnit>
                value={fence[p.key].unit}
                onChange={(u) => setPart(p.key, { unit: u })}
                options={unitOptions}
                className={styles.unitSelect}
              />
            </div>
            <div className={styles.editorHint}>{p.hint}</div>
          </div>
        ))}
      </div>

      {!valid && (
        <div className={styles.invalidDetail}>
          {t('settings.fence.invalidDetail', {
            frozen: durationLabel(fence.frozenHorizon, t),
            frozenDays,
            slushy: durationLabel(fence.slushyHorizon, t),
            slushyDays,
          })}
        </div>
      )}

      <ConstantNote>{t('settings.fence.constantNote')}</ConstantNote>
    </ConfigCard>
  );
}
