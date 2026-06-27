import { useMemo, useRef, useState } from 'react';
import { App, Button, Input, InputNumber } from 'antd';
import { DeleteOutlined, LockOutlined, PlusOutlined } from '@ant-design/icons';
import { createStyles } from 'antd-style';
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

// Shared grid template for the header + editable rows.
const ROW_COLS = '24px 1fr 170px 32px';

const useStyles = createStyles(({ token, css }) => ({
  barWrap: css`
    margin-block-end: 20px;
  `,
  barTrack: css`
    position: relative;
    height: 44px;
    border-radius: ${token.borderRadius}px;
    overflow: hidden;
    display: flex;
    border: 1px solid ${token.colorBorderSecondary};
  `,
  bandSegment: css`
    display: flex;
    flex-direction: column;
    justify-content: center;
    padding: 0 ${token.paddingXS}px;
    min-width: 0;
  `,
  bandLabel: css`
    font-size: ${token.fontSizeSM}px;
    font-weight: 500;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  `,
  bandRange: css`
    font-size: 11px;
    color: ${token.colorTextTertiary};
    font-variant-numeric: tabular-nums;
  `,
  probe: css`
    position: absolute;
    top: 0;
    bottom: 0;
    width: 2px;
    background: ${token.colorText};
    transition: left ${token.motionDurationMid};
  `,
  probeDot: css`
    position: absolute;
    top: -1px;
    left: 50%;
    transform: translateX(-50%);
    width: 8px;
    height: 8px;
    border-radius: 50%;
    background: ${token.colorText};
    margin-block-start: -4px;
  `,
  rows: css`
    display: flex;
    flex-direction: column;
    gap: ${token.marginXS}px;
  `,
  gridHeader: css`
    display: grid;
    grid-template-columns: ${ROW_COLS};
    gap: ${token.marginSM}px;
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextTertiary};
    padding: 0 ${token.paddingXXS}px;
  `,
  gridRow: css`
    display: grid;
    grid-template-columns: ${ROW_COLS};
    gap: ${token.marginSM}px;
    align-items: center;
  `,
  swatch: css`
    width: 12px;
    height: 12px;
    border-radius: 3px;
    justify-self: center;
  `,
  boundCell: css`
    display: inline-flex;
    align-items: center;
    gap: ${token.marginXS}px;
  `,
  firstFixed: css`
    display: inline-flex;
    align-items: center;
    gap: ${token.marginXXS}px;
    height: 32px;
    padding: 0 11px;
    border-radius: ${token.borderRadius}px;
    background: ${token.colorFillQuaternary};
    border: 1px solid ${token.colorBorderSecondary};
    font-size: ${token.fontSize}px;
    color: ${token.colorTextTertiary};
    font-variant-numeric: tabular-nums;
    width: 96px;
    box-sizing: border-box;
  `,
  firstFixedLock: css`
    margin-inline-start: auto;
    font-size: 11px;
  `,
  beyond: css`
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextTertiary};
  `,
  boundInput: css`
    width: 120px;
  `,
  removeCell: css`
    justify-self: center;
  `,
  rowError: css`
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorError};
    padding-inline-start: 36px;
  `,
  addBtn: css`
    margin-block-start: ${token.marginXXS}px;
  `,
  probeBox: css`
    margin-block-start: 18px;
    padding: ${token.paddingSM}px;
    background: ${token.colorInfoBg};
    border: 1px solid ${token.colorInfoBorder};
    border-radius: ${token.borderRadius}px;
    display: flex;
    align-items: center;
    gap: ${token.marginSM}px;
    flex-wrap: wrap;
  `,
  probeText: css`
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorText};
  `,
  probeInput: css`
    width: 110px;
  `,
  probeChip: css`
    display: inline-flex;
    align-items: center;
    height: 24px;
    padding: 0 ${token.paddingSM}px;
    border-radius: 12px;
    font-size: ${token.fontSizeSM}px;
    font-weight: 500;
  `,
  probeInvalid: css`
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorError};
  `,
}));

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
  const { styles } = useStyles();
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
      <div className={styles.barWrap}>
        <div className={styles.barTrack}>
          {valid &&
            bands.map((b, i) => {
              const lower = b.lowerBound ?? 0;
              const upper = i < bands.length - 1 ? (bands[i + 1]?.lowerBound ?? maxScale) : maxScale;
              const widthPct = ((upper - lower) / maxScale) * 100;
              const col = bandColor(i, bands.length);
              return (
                <div
                  key={b.id}
                  className={styles.bandSegment}
                  // dynamic: segment width and band colour are derived from the
                  // configured bounds + band index, unknowable at author time.
                  style={{
                    width: `${widthPct}%`,
                    background: col + '26',
                    borderRight: i < bands.length - 1 ? `2px solid ${col}` : 'none',
                  }}
                >
                  {/* dynamic: label colour follows the band's generated colour. */}
                  <span className={styles.bandLabel} style={{ color: col }}>
                    {b.label}
                  </span>
                  <span className={styles.bandRange}>
                    {lower}%{i < bands.length - 1 ? `–${upper}%` : '+'}
                  </span>
                </div>
              );
            })}
          {valid && probeBand && (
            // dynamic: marker position is the probed percentage along the scale.
            <div className={styles.probe} style={{ left: `${Math.min(100, (probe / maxScale) * 100)}%` }}>
              <span className={styles.probeDot} />
            </div>
          )}
        </div>
      </div>

      {/* Editable rows */}
      <div className={styles.rows}>
        <div className={styles.gridHeader}>
          <span />
          <span>{t('settings.load.label')}</span>
          <span>{t('settings.load.lowerBound')}</span>
          <span />
        </div>

        {bands.map((b, i) => (
          <div key={b.id} className={styles.gridRow}>
            {/* dynamic: swatch colour is the band's generated colour. */}
            <span className={styles.swatch} style={{ background: bandColor(i, bands.length) }} />
            <Input
              value={b.label}
              onChange={(e) => update(b.id, { label: e.target.value })}
              status={errors[`label-${b.id}`] ? 'error' : ''}
            />
            <span className={styles.boundCell}>
              {i === 0 ? (
                <span className={styles.firstFixed} title={t('settings.load.firstFixedTooltip')}>
                  0%
                  <LockOutlined className={styles.firstFixedLock} />
                </span>
              ) : (
                <InputNumber
                  value={b.lowerBound}
                  onChange={(v) => update(b.id, { lowerBound: v })}
                  min={1}
                  addonAfter="%"
                  status={errors[`bound-${b.id}`] ? 'error' : ''}
                  className={styles.boundInput}
                />
              )}
              {i === bands.length - 1 && (
                <span className={styles.beyond}>{t('settings.load.andBeyond')}</span>
              )}
            </span>
            <span className={styles.removeCell}>
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
          return firstError ? <div className={styles.rowError}>{firstError}</div> : null;
        })()}

        <div>
          <Button
            size="small"
            type="dashed"
            icon={<PlusOutlined />}
            onClick={add}
            className={styles.addBtn}
          >
            {t('settings.load.addBand')}
          </Button>
        </div>
      </div>

      {/* Probe / explainability */}
      <div className={styles.probeBox}>
        <span className={styles.probeText}>{t('settings.load.probePrefix')}</span>
        <InputNumber
          value={probe}
          onChange={(v) => setProbe(v ?? 0)}
          min={0}
          step={5}
          addonAfter="%"
          className={styles.probeInput}
        />
        <span className={styles.probeText}>{t('settings.load.probeSuffix')}</span>
        {probeBand ? (
          <span
            className={styles.probeChip}
            // dynamic: chip colour matches the resolved band's generated colour.
            style={{
              background: bandColor(probeIdx, bands.length) + '22',
              border: `1px solid ${bandColor(probeIdx, bands.length)}`,
              color: bandColor(probeIdx, bands.length),
            }}
          >
            {probeBand.label}
          </span>
        ) : (
          <span className={styles.probeInvalid}>{t('settings.load.invalidConfig')}</span>
        )}
      </div>

      <ConstantNote>{t('settings.load.constantNote')}</ConstantNote>
    </ConfigCard>
  );
}
