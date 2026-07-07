import { Input, Segmented, Select, Switch } from 'antd';
import { SearchOutlined, SortAscendingOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { BoardDateControls, type BoardDomain } from '@/components/board';
import type { Grain } from '@/components/timeline';
import { bandStop, type LoadBand } from '@/lib/loadBands';
import type { GroupBy, PeopleSort } from './peopleBoardModel';
import { useStyles } from './PeopleBoardToolbar.styles';

export type Metric = 'pct' | 'hours';

export type PeopleBoardToolbarProps = {
  metric: Metric;
  onMetricChange: (m: Metric) => void;
  bucket: Grain;
  onBucketChange: (b: Grain) => void;
  groupBy: GroupBy;
  onGroupByChange: (g: GroupBy) => void;
  query: string;
  onQueryChange: (q: string) => void;
  bands: LoadBand[];
  bandSelection: ReadonlySet<number>;
  onToggleBand: (index: number) => void;
  countTentative: boolean;
  onCountTentativeChange: (v: boolean) => void;
  sort: PeopleSort;
  onSortChange: (s: PeopleSort) => void;
  domain: BoardDomain;
  onDomainChange: (d: BoardDomain) => void;
  onToday: () => void;
  onFit: () => void;
  resultCount: number;
  totalCount: number;
};

const SORTS: PeopleSort[] = ['severity', 'idle', 'name', 'role'];

export function PeopleBoardToolbar(props: PeopleBoardToolbarProps) {
  const { t } = useTranslation();
  const { styles, cx } = useStyles();

  return (
    <div className={styles.toolbar}>
      <div className={styles.controls}>
        <Segmented<Metric>
          size="small"
          value={props.metric}
          onChange={props.onMetricChange}
          options={[
            { value: 'pct', label: t('peopleBoard.toolbar.metricPct') },
            { value: 'hours', label: t('peopleBoard.toolbar.metricHours') },
          ]}
        />
        <span className={styles.divider} />
        <span className={styles.dimLabel}>{t('peopleBoard.toolbar.bucket')}</span>
        <Segmented<Grain>
          size="small"
          value={props.bucket}
          onChange={props.onBucketChange}
          options={[
            { value: 'day', label: t('peopleBoard.toolbar.bucketDay') },
            { value: 'week', label: t('peopleBoard.toolbar.bucketWeek') },
            { value: 'month', label: t('peopleBoard.toolbar.bucketMonth') },
          ]}
        />
        <span className={styles.divider} />
        <span className={styles.dimLabel}>{t('peopleBoard.toolbar.groupBy')}</span>
        <Segmented<GroupBy>
          size="small"
          value={props.groupBy}
          onChange={props.onGroupByChange}
          options={[
            { value: 'role', label: t('peopleBoard.toolbar.groupRole') },
            { value: 'team', label: t('peopleBoard.toolbar.groupTeam') },
          ]}
        />
        <span className={styles.divider} />
        <BoardDateControls
          domain={props.domain}
          onDomainChange={props.onDomainChange}
          onToday={props.onToday}
          onFit={props.onFit}
        />
        <span className={styles.spacer}>
          <Input
            allowClear
            size="small"
            className={styles.search}
            prefix={<SearchOutlined />}
            placeholder={t('peopleBoard.toolbar.searchPlaceholder')}
            value={props.query}
            onChange={(e) => props.onQueryChange(e.target.value)}
          />
        </span>
      </div>

      <div className={styles.secondRow}>
        <span className={styles.dimLabel}>{t('peopleBoard.toolbar.bands')}</span>
        {props.bands.map((b, i) => {
          const on = props.bandSelection.has(i);
          const stop = bandStop(i, props.bands.length);
          return (
            <button
              key={i}
              type="button"
              className={cx(styles.bandButton, on && styles.bandButtonOn)}
              // dynamic: band colours resolved from the configured bands.
              style={on ? { background: stop.bg, borderColor: stop.solid, color: stop.fg } : undefined}
              onClick={() => props.onToggleBand(i)}
            >
              {/* dynamic: band swatch colour. */}
              <span className={styles.bandSwatch} style={{ background: stop.solid }} />
              {b.label}
            </button>
          );
        })}
        <span className={styles.divider} />
        <label className={styles.dimLabel} title={t('peopleBoard.toolbar.countTentativeHint')}>
          <Switch
            size="small"
            checked={props.countTentative}
            onChange={props.onCountTentativeChange}
          />{' '}
          {t('peopleBoard.toolbar.countTentative')}
        </label>
        <span className={styles.spacer}>
          <span className={styles.resultCount}>
            <strong>{props.resultCount}</strong>{' '}
            {t(props.resultCount === 1 ? 'peopleBoard.toolbar.resultOne' : 'peopleBoard.toolbar.resultMany')}
            {props.resultCount !== props.totalCount
              ? ` ${t('peopleBoard.toolbar.ofTotal', { total: props.totalCount })}`
              : ''}
          </span>
          <SortAscendingOutlined className={styles.dimLabel} />
          <Select
            size="small"
            value={props.sort}
            onChange={props.onSortChange}
            options={SORTS.map((s) => ({ value: s, label: t(`peopleBoard.sort.${s}`) }))}
            popupMatchSelectWidth={false}
          />
        </span>
      </div>
    </div>
  );
}
