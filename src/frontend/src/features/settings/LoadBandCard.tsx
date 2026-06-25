import { useMemo, useRef, useState } from 'react';
import { App, Button, Input, InputNumber, theme } from 'antd';
import { DeleteOutlined, LockOutlined, PlusOutlined } from '@ant-design/icons';
import { useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import {
  getLoadBandsGetQueryKey,
  useLoadBandsUpdate,
} from '@/api/generated/load-bands/load-bands';
import type { LoadBandConfigurationDto } from '@/api/generated/schemas';
import { useApiError } from '@/lib/errors';
import { ConfigCard } from './ConfigCard';
import { ConstantNote } from './ConstantNote';
import { bandColor } from './helpers';

type DraftBand = { id: string; label: string; lowerBound: number | null };

const toDraft = (dto: LoadBandConfigurationDto): DraftBand[] =>
  (dto.bands ?? []).map((b, i) => ({
    id: `b${i}`,
    label: b.label ?? '',
    lowerBound: b.lowerBound ?? 0,
  }));

const signature = (bands: DraftBand[]): string =>
  JSON.stringify(bands.map((b) => [b.label, b.lowerBound]));

const nowTime = () =>
  new Date().toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' });

export function LoadBandCard({ committed }: { committed: LoadBandConfigurationDto }) {
  const { t } = useTranslation();
  const { token } = theme.useToken();
  const { message } = App.useApp();
  const showApiError = useApiError();
  const queryClient = useQueryClient();

  const [bands, setBands] = useState<DraftBand[]>(() => toDraft(committed));
  const [savedAt, setSavedAt] = useState<string | null>(null);
  const [probe, setProbe] = useState<number>(95);
  const counter = useRef(bands.length);

  const committedSig = signature(toDraft(committed));
  const dirty = signature(bands) !== committedSig;

  const errors = useMemo(() => {
    const errs: Record<string, string> = {};
    if (bands.length === 0) errs.general = t('settings.load.errorAtLeastOne');
    if (bands[0] && bands[0].lowerBound !== 0) errs.first = t('settings.load.errorFirstZero');
    bands.forEach((b, i) => {
      if (b.label.trim() === '') errs[`label-${b.id}`] = t('settings.load.errorLabelRequired');
      if (b.lowerBound === null) {
        errs[`bound-${b.id}`] = t('settings.load.errorValueRequired');
        return;
      }
      const prev = bands[i - 1]?.lowerBound;
      if (i > 0 && prev !== null && prev !== undefined && b.lowerBound <= prev)
        errs[`bound-${b.id}`] = t('settings.load.errorIncreasing');
    });
    return errs;
  }, [bands, t]);
  const valid = Object.keys(errors).length === 0;

  const mutation = useLoadBandsUpdate({
    mutation: {
      onSuccess: (data) => {
        message.success(t('settings.load.saveSuccess'));
        setSavedAt(nowTime());
        queryClient.setQueryData(getLoadBandsGetQueryKey(), data);
        queryClient
          .invalidateQueries({ queryKey: getLoadBandsGetQueryKey() })
          .catch(() => undefined);
      },
      onError: (e) => showApiError(e),
    },
  });

  const update = (id: string, patch: Partial<DraftBand>) =>
    setBands((bs) => bs.map((b) => (b.id === id ? { ...b, ...patch } : b)));
  const remove = (id: string) => setBands((bs) => bs.filter((b) => b.id !== id));
  const add = () => {
    counter.current += 1;
    setBands((bs) => {
      const last = bs[bs.length - 1];
      const nextBound = bs.length === 0 ? 0 : (last?.lowerBound ?? 0) + 10;
      return [...bs, { id: `n${counter.current}`, label: '', lowerBound: nextBound }];
    });
  };

  const save = () => {
    if (!valid) return;
    mutation.mutate({
      data: { bands: bands.map((b) => ({ label: b.label.trim(), lowerBound: b.lowerBound ?? 0 })) },
    });
  };

  // Probe resolution: half-open [lower, nextLower) — last band whose lower ≤ pct.
  const resolveBand = (pct: number): DraftBand | null => {
    let found: DraftBand | null = null;
    for (const b of bands) if (b.lowerBound !== null && b.lowerBound <= pct) found = b;
    return found ?? bands[0] ?? null;
  };
  const probeBand = valid ? resolveBand(probe) : null;
  const probeIdx = probeBand ? bands.findIndex((b) => b.id === probeBand.id) : -1;

  const maxScale = Math.max(...bands.map((b) => b.lowerBound ?? 0), 100) + 25;

  return (
    <ConfigCard
      title={t('settings.load.title')}
      subtitle={t('settings.load.subtitle')}
      dirty={dirty}
      valid={valid}
      saving={mutation.isPending}
      savedAt={savedAt}
      onSave={save}
      onReset={() => setBands(toDraft(committed))}
    >
      {/* Visual band bar + probe marker */}
      <div style={{ marginBottom: 20 }}>
        <div
          style={{
            position: 'relative',
            height: 44,
            borderRadius: token.borderRadius,
            overflow: 'hidden',
            display: 'flex',
            border: `1px solid ${token.colorBorderSecondary}`,
          }}
        >
          {valid &&
            bands.map((b, i) => {
              const lower = b.lowerBound ?? 0;
              const upper = i < bands.length - 1 ? (bands[i + 1]?.lowerBound ?? maxScale) : maxScale;
              const widthPct = ((upper - lower) / maxScale) * 100;
              const col = bandColor(i, bands.length);
              return (
                <div
                  key={b.id}
                  style={{
                    width: `${widthPct}%`,
                    background: col + '26',
                    borderRight: i < bands.length - 1 ? `2px solid ${col}` : 'none',
                    display: 'flex',
                    flexDirection: 'column',
                    justifyContent: 'center',
                    padding: '0 8px',
                    minWidth: 0,
                  }}
                >
                  <span
                    style={{
                      fontSize: 12,
                      fontWeight: 500,
                      color: col,
                      whiteSpace: 'nowrap',
                      overflow: 'hidden',
                      textOverflow: 'ellipsis',
                    }}
                  >
                    {b.label}
                  </span>
                  <span
                    style={{
                      fontSize: 11,
                      color: token.colorTextTertiary,
                      fontVariantNumeric: 'tabular-nums',
                    }}
                  >
                    {lower}%{i < bands.length - 1 ? `–${upper}%` : '+'}
                  </span>
                </div>
              );
            })}
          {valid && probeBand && (
            <div
              style={{
                position: 'absolute',
                top: 0,
                bottom: 0,
                left: `${Math.min(100, (probe / maxScale) * 100)}%`,
                width: 2,
                background: token.colorText,
                transition: 'left .15s',
              }}
            >
              <span
                style={{
                  position: 'absolute',
                  top: -1,
                  left: '50%',
                  transform: 'translateX(-50%)',
                  width: 8,
                  height: 8,
                  borderRadius: '50%',
                  background: token.colorText,
                  marginTop: -4,
                }}
              />
            </div>
          )}
        </div>
      </div>

      {/* Editable rows */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
        <div
          style={{
            display: 'grid',
            gridTemplateColumns: '24px 1fr 170px 32px',
            gap: 12,
            fontSize: 12,
            color: token.colorTextTertiary,
            padding: '0 4px',
          }}
        >
          <span />
          <span>{t('settings.load.label')}</span>
          <span>{t('settings.load.lowerBound')}</span>
          <span />
        </div>

        {bands.map((b, i) => (
          <div
            key={b.id}
            style={{
              display: 'grid',
              gridTemplateColumns: '24px 1fr 170px 32px',
              gap: 12,
              alignItems: 'center',
            }}
          >
            <span
              style={{
                width: 12,
                height: 12,
                borderRadius: 3,
                background: bandColor(i, bands.length),
                justifySelf: 'center',
              }}
            />
            <Input
              value={b.label}
              onChange={(e) => update(b.id, { label: e.target.value })}
              status={errors[`label-${b.id}`] ? 'error' : ''}
            />
            <span style={{ display: 'inline-flex', alignItems: 'center', gap: 8 }}>
              {i === 0 ? (
                <span
                  style={{
                    display: 'inline-flex',
                    alignItems: 'center',
                    gap: 6,
                    height: 32,
                    padding: '0 11px',
                    borderRadius: token.borderRadius,
                    background: token.colorFillQuaternary,
                    border: `1px solid ${token.colorBorderSecondary}`,
                    fontSize: 14,
                    color: token.colorTextTertiary,
                    fontVariantNumeric: 'tabular-nums',
                    width: 96,
                    boxSizing: 'border-box',
                  }}
                  title={t('settings.load.firstFixedTooltip')}
                >
                  0%
                  <LockOutlined style={{ marginLeft: 'auto', fontSize: 11 }} />
                </span>
              ) : (
                <InputNumber
                  value={b.lowerBound}
                  onChange={(v) => update(b.id, { lowerBound: v })}
                  min={1}
                  addonAfter="%"
                  status={errors[`bound-${b.id}`] ? 'error' : ''}
                  style={{ width: 120 }}
                />
              )}
              {i === bands.length - 1 && (
                <span style={{ fontSize: 12, color: token.colorTextTertiary }}>
                  {t('settings.load.andBeyond')}
                </span>
              )}
            </span>
            <span style={{ justifySelf: 'center' }}>
              {bands.length > 1 && i > 0 && (
                <Button
                  type="text"
                  size="small"
                  icon={<DeleteOutlined />}
                  onClick={() => remove(b.id)}
                  aria-label={t('settings.load.removeBand')}
                />
              )}
            </span>
          </div>
        ))}

        {(() => {
          const firstError = errors.first ?? errors.general;
          return firstError ? (
            <div style={{ fontSize: 12, color: token.colorError, paddingLeft: 36 }}>
              {firstError}
            </div>
          ) : null;
        })()}

        <div>
          <Button
            size="small"
            type="dashed"
            icon={<PlusOutlined />}
            onClick={add}
            style={{ marginTop: 4 }}
          >
            {t('settings.load.addBand')}
          </Button>
        </div>
      </div>

      {/* Probe / explainability */}
      <div
        style={{
          marginTop: 18,
          padding: 12,
          background: token.colorInfoBg,
          border: `1px solid ${token.colorInfoBorder}`,
          borderRadius: token.borderRadius,
          display: 'flex',
          alignItems: 'center',
          gap: 12,
          flexWrap: 'wrap',
        }}
      >
        <span style={{ fontSize: 13, color: token.colorText }}>{t('settings.load.probePrefix')}</span>
        <InputNumber
          value={probe}
          onChange={(v) => setProbe(v ?? 0)}
          min={0}
          step={5}
          addonAfter="%"
          style={{ width: 110 }}
        />
        <span style={{ fontSize: 13, color: token.colorText }}>{t('settings.load.probeSuffix')}</span>
        {probeBand ? (
          <span
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              height: 24,
              padding: '0 10px',
              borderRadius: 12,
              background: bandColor(probeIdx, bands.length) + '22',
              border: `1px solid ${bandColor(probeIdx, bands.length)}`,
              color: bandColor(probeIdx, bands.length),
              fontSize: 13,
              fontWeight: 500,
            }}
          >
            {probeBand.label}
          </span>
        ) : (
          <span style={{ fontSize: 13, color: token.colorError }}>
            {t('settings.load.invalidConfig')}
          </span>
        )}
      </div>

      <ConstantNote>{t('settings.load.constantNote')}</ConstantNote>
    </ConfigCard>
  );
}
