import { useState } from 'react';
import { Button, InputNumber, Modal, Segmented, Select } from 'antd';
import { CloseOutlined, PlusOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import { useTranslation } from 'react-i18next';
import { isoWeek } from '@/components/board';
import { bandLabelFor, loadColor, NO_CAPACITY_CELL, type LoadBand } from '@/lib/loadBands';
import { nodeWindowOf, peopleByRole, type BoardProject, type DemandRow } from './boardModel';
import {
  capacityHours,
  convertAmount,
  coverBuckets,
  demandBalance,
  freeHours,
  grainFor,
  percentForHours,
  presentDays,
  rowHours,
  rowPercent,
  type CoverBucket,
  type CoverDay,
  type CoverRow,
  type CoverUnit,
} from './coverModel';
import type { PersonPoolEntry } from './useProjectsBoard';
import { useCoverPreview } from './useCoverPreview';
import { COVER_MODAL_WIDTH, useStyles } from './CoverDemandModal.styles';

// One coverage to create. Exactly one of the two is set: hours go through
// `createByHours` (the server derives the quota), a quota through `create`.
export type CoverSubmitRow = { resourceId: string; hours: number | null; percent: number | null };

export type CoverDemandModalProps = {
  state: { demand: DemandRow; project: BoardProject } | null;
  submitting: boolean;
  personPool: PersonPoolEntry[];
  bands: LoadBand[];
  onSubmit: (rows: CoverSubmitRow[]) => void;
  onCancel: () => void;
};

const num = (n: number) => n.toLocaleString('it-IT', { maximumFractionDigits: 1 });
const firstName = (name: string) => name.split(' ')[0] ?? name;

// Mounted per demand, so every open starts from that demand's residual.
export function CoverDemandModal(props: CoverDemandModalProps) {
  return props.state ? <CoverDialog key={props.state.demand.demandId} {...props} state={props.state} /> : null;
}

// Covering a demand (study «Copertura della domanda»): the demand is asked in
// hours, so the coverage is WRITTEN in hours and the quota is derived for the
// person chosen. Above, the demand with what stays uncovered; under each
// person, what their load becomes. More people on the same demand are more
// rows of the same dialog — the demand stays one, and so does its residual.
function CoverDialog({
  state,
  submitting,
  personPool,
  bands,
  onSubmit,
  onCancel,
}: CoverDemandModalProps & { state: { demand: DemandRow; project: BoardProject } }) {
  const { t } = useTranslation();
  const { styles, cx } = useStyles();
  const { demand, project } = state;

  // The coverage is born as wide as the demand's node and follows it
  // (ADR-0035 §2): that node's window is the period every figure refers to.
  const home = nodeWindowOf(project, demand.nodeId);
  const preview = useCoverPreview(home?.from ?? null, home?.to ?? null);

  const uncovered = demand.requiredH !== null ? Math.max(0, demand.requiredH - demand.coveredH) : null;
  const [unit, setUnit] = useState<CoverUnit>('hours');
  const [serial, setSerial] = useState(1);
  // The field is born with the residual: a fact about the demand, not advice.
  const [rows, setRows] = useState<CoverRow[]>([
    { key: 'row-1', resourceId: null, amount: uncovered !== null && uncovered > 0 ? uncovered : null },
  ]);

  const daysFor = (row: CoverRow): CoverDay[] | null => (row.resourceId ? preview.daysOf(row.resourceId) : null);
  const hoursByRow = rows.map((r) => {
    const days = daysFor(r);
    return days ? rowHours(r, unit, days) : 0;
  });
  const adding = hoursByRow.reduce((sum, h) => sum + h, 0);
  const balance = demandBalance(demand.requiredH, demand.coveredH, adding);

  const patchRow = (key: string, patch: Partial<CoverRow>) =>
    setRows((prev) => prev.map((r) => (r.key === key ? { ...r, ...patch } : r)));

  const changeUnit = (next: CoverUnit) => {
    setRows((prev) => prev.map((r) => ({ ...r, amount: convertAmount(r.amount, next, daysFor(r)) })));
    setUnit(next);
  };

  const addRow = () => {
    setRows((prev) => [
      ...prev,
      // A second person starts from what the first leaves open.
      { key: `row-${serial + 1}`, resourceId: null, amount: unit === 'hours' && balance.left > 0 ? balance.left : null },
    ]);
    setSerial((n) => n + 1);
  };

  const ready =
    !!home &&
    !preview.isLoading &&
    rows.every((r) => {
      const days = daysFor(r);
      return !!days && r.amount !== null && r.amount > 0 && capacityHours(days) > 0;
    });

  const submit = () =>
    onSubmit(
      rows.map((r) => ({
        resourceId: r.resourceId!,
        hours: unit === 'hours' ? r.amount : null,
        percent: unit === 'percent' ? r.amount : null,
      })),
    );

  const submitLabel =
    rows.length === 1
      ? t('projects.cover.submitOne', { hours: num(adding) })
      : t('projects.cover.submitMany', { count: rows.length, hours: num(adding) });

  const total = Math.max(demand.requiredH ?? 0, demand.coveredH + adding);

  return (
    <Modal
      open
      width={COVER_MODAL_WIDTH}
      title={t('projects.cover.title', { role: demand.roleName })}
      onCancel={onCancel}
      footer={
        <div className={styles.footer}>
          <span className={styles.footerNote}>{t('projects.cover.tentativeNote')}</span>
          <span className={styles.footerEnd}>
            <Button onClick={onCancel}>{t('projects.cover.cancel')}</Button>
            <Button type="primary" disabled={!ready} loading={submitting} onClick={submit}>
              {submitLabel}
            </Button>
          </span>
        </div>
      }
    >
      <div className={styles.demand}>
        <div className={styles.demandHead}>
          <span className={styles.demandTitle}>
            {home && !home.root
              ? t('projects.cover.demandOn', { role: demand.roleName, node: home.name })
              : t('projects.cover.demandOn', { role: demand.roleName, node: project.name })}
          </span>
          <span className={styles.demandMeta}>
            {home ? `${dayjs(home.from).format('D MMM')} – ${dayjs(home.to).format('D MMM YYYY')} · ` : ''}
            {demand.requiredH !== null
              ? t('projects.cover.required', { hours: num(demand.requiredH) })
              : t('projects.cover.bestEffort')}
          </span>
        </div>
        <div className={styles.track}>
          {/* dynamic: segment widths from the demand's live balance. */}
          {total > 0 && balance.covered > 0 && (
            <span className={styles.trackCovered} style={{ width: `${(balance.covered / total) * 100}%` }} />
          )}
          {total > 0 && adding > 0 && (
            <span className={styles.trackAdding} style={{ width: `${(adding / total) * 100}%` }} />
          )}
        </div>
        <div className={styles.balance}>
          {balance.covered > 0 && <span>{t('projects.cover.alreadyCovered', { hours: num(balance.covered) })}</span>}
          <span className={styles.balanceAdding}>{t('projects.cover.thisCoverage', { hours: num(balance.adding) })}</span>
          {demand.requiredH !== null &&
            (balance.over > 0 ? (
              <span>
                {t('projects.cover.over')} <b>{num(balance.over)} h</b>
              </span>
            ) : (
              <span>
                {t('projects.cover.left')} <b>{num(balance.left)} h</b>
              </span>
            ))}
        </div>
      </div>

      <div className={styles.unitRow}>
        <span>{t('projects.cover.who')}</span>
        <span className={styles.unitSpacer}>
          <Segmented<CoverUnit>
            size="small"
            value={unit}
            onChange={changeUnit}
            options={[
              { value: 'hours', label: t('projects.cover.unitHours') },
              { value: 'percent', label: t('projects.cover.unitPercent') },
            ]}
          />
        </span>
      </div>

      {rows.map((row, index) => {
        const days = daysFor(row);
        const person = personPool.find((p) => p.id === row.resourceId);
        const taken = new Set(rows.filter((r) => r.key !== row.key && r.resourceId).map((r) => r.resourceId));
        const percent = days ? rowPercent(row, unit, days) : null;
        const capacity = days ? capacityHours(days) : 0;
        const free = days ? freeHours(days) : 0;
        // What the demand still asks once the OTHER rows are counted.
        const leftForRow =
          demand.requiredH === null
            ? null
            : Math.max(0, demand.requiredH - demand.coveredH - (adding - (hoursByRow[index] ?? 0)));
        const setHours = (hours: number) =>
          patchRow(row.key, { amount: unit === 'hours' ? hours : convertAmount(hours, 'percent', days) });

        return (
          <div key={row.key} className={styles.row}>
            <div className={styles.rowHead}>
              <Select
                className={styles.person}
                showSearch
                optionFilterProp="label"
                aria-label={t('projects.cover.personLabel')}
                placeholder={t('projects.cover.personPlaceholder')}
                value={row.resourceId}
                onChange={(resourceId: string) => patchRow(row.key, { resourceId })}
                options={peopleByRole(
                  personPool.filter((p) => !taken.has(p.id)),
                  demand.roleName,
                  t('projects.lane.actions.noRoleGroup'),
                )}
                // The open picker is where people are compared: free hours in
                // the window and the quota these hours would take of each.
                optionRender={(option) => {
                  const candidate = preview.daysOf(String(option.value));
                  const quota =
                    unit === 'hours' && row.amount ? percentForHours(row.amount, candidate) : null;
                  return (
                    <span className={styles.option}>
                      <span className={styles.optionName}>{option.label}</span>
                      {!preview.isLoading && (
                        <span className={styles.optionFacts}>
                          {capacityHours(candidate) > 0
                            ? `${t('projects.cover.optionFree', { hours: num(freeHours(candidate)) })}${
                                quota !== null ? ` · ${num(quota)}%` : ''
                              }`
                            : t('projects.cover.optionNoCapacity')}
                        </span>
                      )}
                    </span>
                  );
                }}
              />
              <InputNumber
                className={styles.amount}
                aria-label={unit === 'hours' ? t('projects.cover.unitHours') : t('projects.cover.unitPercent')}
                min={0}
                step={unit === 'hours' ? 8 : 5}
                value={row.amount}
                onChange={(amount) => patchRow(row.key, { amount })}
                suffix={unit === 'hours' ? 'h' : '%'}
              />
              <span className={styles.derived}>
                {days && row.amount
                  ? unit === 'hours'
                    ? percent !== null
                      ? `= ${num(percent)}%`
                      : '—'
                    : `= ${num(hoursByRow[index] ?? 0)} h`
                  : ''}
              </span>
              {rows.length > 1 && (
                <Button
                  type="text"
                  size="small"
                  icon={<CloseOutlined />}
                  aria-label={t('projects.cover.removeRow')}
                  onClick={() => setRows((prev) => prev.filter((r) => r.key !== row.key))}
                />
              )}
            </div>

            {days && person && !preview.isLoading && (
              <>
                <div className={styles.links}>
                  {leftForRow !== null && leftForRow > 0 && (
                    <a onClick={() => setHours(leftForRow)}>
                      {t('projects.cover.linkUncovered', { hours: num(leftForRow) })}
                    </a>
                  )}
                  {free > 0 && (
                    <a onClick={() => setHours(free)}>{t('projects.cover.linkFree', { hours: num(free) })}</a>
                  )}
                </div>
                <div className={cx(styles.fact, capacity === 0 && styles.factWarn)}>
                  {capacity > 0
                    ? t('projects.cover.fact', {
                        name: firstName(person.name),
                        days: presentDays(days),
                        capacity: num(capacity),
                        free: num(free),
                      })
                    : t('projects.cover.factNoCapacity', { name: firstName(person.name) })}
                </div>
                {percent !== null && (
                  <div className={styles.buckets}>
                    {coverBuckets(days, percent, grainFor(days)).map((b) => (
                      <Bucket key={b.key} bucket={b} grain={grainFor(days)} bands={bands} />
                    ))}
                  </div>
                )}
              </>
            )}
          </div>
        );
      })}

      <Button type="link" size="small" icon={<PlusOutlined />} onClick={addRow}>
        {demand.requiredH !== null && balance.left > 0
          ? t('projects.cover.addPersonLeft', { hours: num(balance.left) })
          : t('projects.cover.addPerson')}
      </Button>
    </Modal>
  );
}

// The person's load in one week (or month) of the window, before → after.
function Bucket({ bucket, grain, bands }: { bucket: CoverBucket; grain: 'week' | 'month'; bands: LoadBand[] }) {
  const { t } = useTranslation();
  const { styles } = useStyles();
  const from = dayjs(bucket.from);
  const to = dayjs(bucket.to);
  const label =
    grain === 'month'
      ? from.format('MMM YYYY')
      : t('projects.cover.week', {
          week: isoWeek(from),
          range: from.month() === to.month() ? `${from.format('D')}–${to.format('D MMM')}` : `${from.format('D MMM')}–${to.format('D MMM')}`,
        });

  return (
    <div className={styles.bucket}>
      <div className={styles.bucketLabel}>{label}</div>
      <div className={styles.bucketChips}>
        <LoadChip pct={bucket.before} bands={bands} />
        <span>→</span>
        <LoadChip pct={bucket.after} bands={bands} />
      </div>
      <div className={styles.bucketHours}>
        {bucket.before === null ? t('projects.cover.zeroCapacity') : `+${num(bucket.addedHours)} h`}
      </div>
    </div>
  );
}

// Number and band colour only; the band's name is in the tooltip.
function LoadChip({ pct, bands }: { pct: number | null; bands: LoadBand[] }) {
  const { t } = useTranslation();
  const { styles } = useStyles();
  const c = pct === null ? NO_CAPACITY_CELL : loadColor(pct, bands);
  return (
    // dynamic: band colours resolved from the configured bands.
    <span
      className={styles.chip}
      style={{ background: c.bg, color: c.fg, borderColor: c.solid }}
      title={pct === null ? t('projects.cover.undefinedLoad') : bandLabelFor(pct, bands)}
    >
      {pct === null ? '—' : `${pct}%`}
    </span>
  );
}
