import { useState } from 'react';
import { App, InputNumber, Select, theme } from 'antd';
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
      <div style={{ marginBottom: 20 }}>
        <div
          style={{
            position: 'relative',
            height: 56,
            borderRadius: token.borderRadius,
            overflow: 'hidden',
            display: 'flex',
            border: `1px solid ${token.colorBorderSecondary}`,
          }}
        >
          {valid ? (
            zones.map((z) => {
              const widthPct = ((z.to - z.from) / totalDays) * 100;
              return (
                <div
                  key={z.key}
                  style={{
                    width: `${widthPct}%`,
                    background: z.color + '1f',
                    borderRight: z.key !== 'liquid' ? `2px solid ${z.color}` : 'none',
                    display: 'flex',
                    flexDirection: 'column',
                    justifyContent: 'center',
                    padding: '0 10px',
                    minWidth: 0,
                  }}
                >
                  <span style={{ fontSize: 12, fontWeight: 600, color: z.color }}>{z.label}</span>
                  <span style={{ fontSize: 11, color: token.colorTextTertiary }}>
                    {z.dur ? durationLabel(z.dur, t) : t('settings.fence.beyondSlushy')}
                  </span>
                </div>
              );
            })
          ) : (
            <div
              style={{
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                width: '100%',
                fontSize: 13,
                color: token.colorError,
              }}
            >
              {t('settings.fence.invalidShort')}
            </div>
          )}
        </div>
        {valid && (
          <div
            style={{
              position: 'relative',
              height: 24,
              marginTop: 4,
              fontSize: 11,
              color: token.colorTextTertiary,
              fontVariantNumeric: 'tabular-nums',
            }}
          >
            <span style={{ position: 'absolute', left: 0 }}>↑ {t('settings.fence.today')}</span>
            <span style={{ position: 'absolute', left: leftPct(frozenDays), transform: 'translateX(-50%)', whiteSpace: 'nowrap' }}>
              ↑ {frozenEnd.format('D MMM')}
            </span>
            <span style={{ position: 'absolute', left: leftPct(slushyDays), transform: 'translateX(-50%)', whiteSpace: 'nowrap' }}>
              ↑ {slushyEnd.format('D MMM')}
            </span>
          </div>
        )}
      </div>

      {/* Duration editors */}
      <div style={{ display: 'flex', gap: 32, flexWrap: 'wrap' }}>
        {editors.map((p) => (
          <div key={p.key}>
            <div style={{ fontSize: 13, fontWeight: 500, marginBottom: 6, display: 'inline-flex', alignItems: 'center', gap: 6 }}>
              <span style={{ width: 8, height: 8, borderRadius: 2, background: p.color }} />
              {p.label}
            </div>
            <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
              <InputNumber
                value={fence[p.key].value}
                onChange={(v) => setPart(p.key, { value: v ?? 0 })}
                min={1}
                status={!valid ? 'error' : ''}
                style={{ width: 90 }}
              />
              <Select<DurationUnit>
                value={fence[p.key].unit}
                onChange={(u) => setPart(p.key, { unit: u })}
                options={unitOptions}
                style={{ width: 130 }}
              />
            </div>
            <div style={{ fontSize: 11, color: token.colorTextTertiary, marginTop: 6, maxWidth: 220, lineHeight: 1.4 }}>
              {p.hint}
            </div>
          </div>
        ))}
      </div>

      {!valid && (
        <div style={{ fontSize: 12, color: token.colorError, marginTop: 12 }}>
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
