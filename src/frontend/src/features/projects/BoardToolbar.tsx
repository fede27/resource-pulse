import { useMemo, type ReactNode } from 'react';
import { Button, Checkbox, Popover, Segmented, Select, Tag } from 'antd';
import { FilterOutlined, SortAscendingOutlined } from '@ant-design/icons';
import type { TFunction } from 'i18next';
import { useTranslation } from 'react-i18next';
import { BoardDateControls } from '@/components/board';
import type { Grain } from '@/components/timeline';
import {
  activeFilterCount,
  defaultFilters,
  type BoardFilters,
  type Lifecycle,
  type Provenance,
  type SortKey,
  type Verdict,
} from './boardModel';
import { VERDICT_COLORS } from './boardColors';
import type { PersonPoolEntry } from './useProjectsBoard';
import type { BoardDomain } from './useProjectsBoard';
import { useStyles } from './BoardToolbar.styles';

const LIFECYCLES: Lifecycle[] = ['futuro', 'attivo', 'chiuso'];
const PROVENANCES: Provenance[] = ['committed', 'proposed'];
const VERDICTS: Verdict[] = ['sostenibile', 'arischio', 'scoperto'];
const SORTS: SortKey[] = ['sustain', 'name', 'start', 'owner'];
const LIFECYCLE_DOTS: Record<Lifecycle, string> = {
  futuro: '#8c8c8c',
  attivo: '#52c41a',
  chiuso: '#bfbfbf',
};

export type Metric = 'pct' | 'hours';

export type BoardToolbarProps = {
  metric: Metric;
  onMetricChange: (m: Metric) => void;
  bucket: Grain;
  onBucketChange: (b: Grain) => void;
  domain: BoardDomain;
  onDomainChange: (d: BoardDomain) => void;
  onToday: () => void;
  onFit: () => void;
  filters: BoardFilters;
  onFiltersChange: (f: BoardFilters) => void;
  personPool: PersonPoolEntry[];
  roles: string[];
  resultCount: number;
  totalCount: number;
};

function toggleInSet<T>(set: Set<T>, key: T): Set<T> {
  const next = new Set(set);
  if (next.has(key)) next.delete(key);
  else next.add(key);
  return next;
}

export function BoardToolbar(props: BoardToolbarProps) {
  const { t } = useTranslation();
  const { styles } = useStyles();
  const { filters, onFiltersChange } = props;
  const set = (patch: Partial<BoardFilters>) => onFiltersChange({ ...filters, ...patch });

  const count = activeFilterCount(filters);
  const dirty = count > 0 || filters.sort !== 'sustain';

  const chips = useMemo(() => buildChips(filters, set, t, props.personPool), [filters, props.personPool, t]);

  const filterPanel = (
    <div className={styles.filterPanel}>
      <div className={styles.panelHeader}>
        <span>{t('projects.toolbar.filters')}</span>
        <Button
          type="link"
          size="small"
          disabled={!dirty}
          onClick={() => onFiltersChange(defaultFilters())}
        >
          {t('projects.toolbar.resetDefaults')}
        </Button>
      </div>
      <div className={styles.panelSection}>{t('projects.filters.lifecycle')}</div>
      {LIFECYCLES.map((l) => (
        <label key={l} className={styles.checkRow}>
          <Checkbox
            checked={filters.lifecycle.has(l)}
            onChange={() => set({ lifecycle: toggleInSet(filters.lifecycle, l) })}
          />
          {/* dynamic: lifecycle dot colour from semantic palette. */}
          <span className={styles.dot} style={{ background: LIFECYCLE_DOTS[l] }} />
          <span>{t(`projects.lifecycle.${l}`)}</span>
        </label>
      ))}
      <div className={styles.panelSection}>{t('projects.filters.provenance')}</div>
      {PROVENANCES.map((p) => (
        <label key={p} className={styles.checkRow}>
          <Checkbox
            checked={filters.provenance.has(p)}
            onChange={() => set({ provenance: toggleInSet(filters.provenance, p) })}
          />
          <span>{t(`projects.provenance.${p}`)}</span>
        </label>
      ))}
      <div className={styles.panelSection}>{t('projects.filters.sustain')}</div>
      {VERDICTS.map((v) => (
        <label key={v} className={styles.checkRow}>
          <Checkbox
            checked={filters.sustain.has(v)}
            onChange={() => set({ sustain: toggleInSet(filters.sustain, v) })}
          />
          {/* dynamic: verdict dot colour from semantic palette. */}
          <span className={styles.dot} style={{ background: VERDICT_COLORS[v].stripe }} />
          <span>{t(`projects.verdict.${v}`)}</span>
        </label>
      ))}
      <div className={styles.panelSection}>{t('projects.filters.mine')}</div>
      <label className={styles.checkRow}>
        <Checkbox checked={filters.mineOwner} onChange={() => set({ mineOwner: !filters.mineOwner })} />
        <span>{t('projects.filters.mineOwner')}</span>
      </label>
      <label className={styles.checkRow}>
        <Checkbox checked={filters.mineHoles} onChange={() => set({ mineHoles: !filters.mineHoles })} />
        <span>{t('projects.filters.mineHoles')}</span>
      </label>
      <div className={styles.panelSection}>{t('projects.filters.byPerson')}</div>
      <Select
        mode="multiple"
        allowClear
        className={styles.fullWidth}
        placeholder={t('projects.filters.searchPerson')}
        value={[...filters.people]}
        onChange={(ids) => set({ people: new Set(ids) })}
        options={props.personPool.map((p) => ({
          value: p.id,
          label: p.roleName ? `${p.name} · ${p.roleName}` : p.name,
        }))}
        optionFilterProp="label"
        maxTagCount="responsive"
      />
      <div className={styles.panelSection}>{t('projects.filters.byRole')}</div>
      <div className={styles.roleGrid}>
        {props.roles.map((r) => (
          <label key={r} className={styles.checkRow}>
            <Checkbox
              checked={filters.roles.has(r)}
              onChange={() => set({ roles: toggleInSet(filters.roles, r) })}
            />
            <span>{r}</span>
          </label>
        ))}
      </div>
      <label className={styles.checkRow}>
        <Checkbox checked={filters.hideEmpty} onChange={() => set({ hideEmpty: !filters.hideEmpty })} />
        <span>{t('projects.filters.hideEmpty')}</span>
      </label>
    </div>
  );

  return (
    <div className={styles.toolbar}>
      <div className={styles.controls}>
        <Segmented<Metric>
          size="small"
          value={props.metric}
          onChange={props.onMetricChange}
          options={[
            { value: 'pct', label: t('projects.toolbar.metricPct') },
            { value: 'hours', label: t('projects.toolbar.metricHours') },
          ]}
        />
        <span className={styles.divider} />
        <span className={styles.dimLabel}>{t('projects.toolbar.bucket')}</span>
        <Segmented<Grain>
          size="small"
          value={props.bucket}
          onChange={props.onBucketChange}
          options={[
            { value: 'day', label: t('projects.toolbar.bucketDay') },
            { value: 'week', label: t('projects.toolbar.bucketWeek') },
            { value: 'month', label: t('projects.toolbar.bucketMonth') },
          ]}
        />
        <span className={styles.divider} />
        <BoardDateControls
          domain={props.domain}
          onDomainChange={props.onDomainChange}
          onToday={props.onToday}
          onFit={props.onFit}
        />
        <div className={styles.spacer}>
          <SortAscendingOutlined className={styles.dimLabel} />
          <Select
            size="small"
            value={filters.sort}
            onChange={(v) => set({ sort: v })}
            options={SORTS.map((s) => ({ value: s, label: t(`projects.sort.${s}`) }))}
            popupMatchSelectWidth={false}
          />
          <Popover trigger="click" placement="bottomRight" content={filterPanel}>
            <Button size="small" icon={<FilterOutlined />} type={count > 0 ? 'primary' : 'default'} ghost={count > 0}>
              {t('projects.toolbar.filters')}
              {count > 0 ? ` (${count})` : ''}
            </Button>
          </Popover>
        </div>
      </div>

      <div className={styles.chipsRow}>
        <span className={styles.resultCount}>
          <strong>{props.resultCount}</strong>{' '}
          {t(
            props.resultCount === 1
              ? 'projects.toolbar.resultProjectOne'
              : 'projects.toolbar.resultProjectsMany',
          )}
          {props.resultCount !== props.totalCount
            ? ` ${t('projects.toolbar.ofTotal', { total: props.totalCount })}`
            : ''}
        </span>
        <span className={styles.divider} />
        {chips.length ? chips : <span className={styles.noFilters}>{t('projects.toolbar.noFilters')}</span>}
        {dirty && (
          <Button type="link" size="small" onClick={() => onFiltersChange(defaultFilters())}>
            {t('projects.toolbar.clear')}
          </Button>
        )}
      </div>
    </div>
  );
}

function buildChips(
  filters: BoardFilters,
  set: (patch: Partial<BoardFilters>) => void,
  t: TFunction,
  personPool: PersonPoolEntry[],
) {
  const chips: ReactNode[] = [];
  if (filters.lifecycle.size !== 3) {
    const labels = [...filters.lifecycle].map((k) => t(`projects.lifecycleShort.${k}`)).join(', ');
    chips.push(
      <Tag
        key="lc"
        closable
        color="blue"
        onClose={() => set({ lifecycle: new Set<Lifecycle>(['futuro', 'attivo', 'chiuso']) })}
      >
        {t('projects.filters.chips.lifecycle', { labels: labels || t('projects.filters.chips.none') })}
      </Tag>,
    );
  }
  if (filters.provenance.size !== 2) {
    const labels = [...filters.provenance].map((k) => t(`projects.provenance.${k}`)).join(', ');
    chips.push(
      <Tag
        key="pr"
        closable
        color="blue"
        onClose={() => set({ provenance: new Set<Provenance>(['committed', 'proposed']) })}
      >
        {t('projects.filters.chips.provenance', { labels: labels || t('projects.filters.chips.none') })}
      </Tag>,
    );
  }
  if (filters.sustain.size !== 3) {
    const labels = [...filters.sustain].map((k) => t(`projects.verdict.${k}`)).join(', ');
    chips.push(
      <Tag
        key="su"
        closable
        color="blue"
        onClose={() => set({ sustain: new Set<Verdict>(['sostenibile', 'arischio', 'scoperto']) })}
      >
        {t('projects.filters.chips.sustain', { labels: labels || t('projects.filters.chips.none') })}
      </Tag>,
    );
  }
  if (filters.mineOwner) {
    chips.push(
      <Tag key="mo" closable color="blue" onClose={() => set({ mineOwner: false })}>
        {t('projects.filters.chips.mineOwner')}
      </Tag>,
    );
  }
  if (filters.mineHoles) {
    chips.push(
      <Tag key="mh" closable color="blue" onClose={() => set({ mineHoles: false })}>
        {t('projects.filters.chips.mineHoles')}
      </Tag>,
    );
  }
  for (const id of filters.people) {
    const name = personPool.find((p) => p.id === id)?.name ?? '—';
    chips.push(
      <Tag
        key={`p-${id}`}
        closable
        color="blue"
        onClose={() => {
          const next = new Set(filters.people);
          next.delete(id);
          set({ people: next });
        }}
      >
        {name}
      </Tag>,
    );
  }
  for (const r of filters.roles) {
    chips.push(
      <Tag
        key={`r-${r}`}
        closable
        color="blue"
        onClose={() => {
          const next = new Set(filters.roles);
          next.delete(r);
          set({ roles: next });
        }}
      >
        {t('projects.filters.chips.role', { role: r })}
      </Tag>,
    );
  }
  if (filters.hideEmpty) {
    chips.push(
      <Tag key="he" closable color="blue" onClose={() => set({ hideEmpty: false })}>
        {t('projects.filters.chips.hideEmpty')}
      </Tag>,
    );
  }
  return chips;
}
