import { Input, Segmented, Select, Switch } from 'antd';
import { SearchOutlined, SortAscendingOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { BoardTimeFilter, BoardToolbar, type BoardDomain } from '@/components/board';
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
    <BoardToolbar>
      <BoardToolbar.Row>
        <Segmented<Metric>
          size="small"
          value={props.metric}
          onChange={props.onMetricChange}
          options={[
            { value: 'pct', label: t('peopleBoard.toolbar.metricPct') },
            { value: 'hours', label: t('peopleBoard.toolbar.metricHours') },
          ]}
        />
        <BoardToolbar.Divider />
        <BoardTimeFilter
          grain={props.bucket}
          onGrainChange={props.onBucketChange}
          domain={props.domain}
          onDomainChange={props.onDomainChange}
          onToday={props.onToday}
          onFit={props.onFit}
        />
        <BoardToolbar.Spacer>
          <Input
            allowClear
            size="small"
            className={styles.search}
            prefix={<SearchOutlined />}
            placeholder={t('peopleBoard.toolbar.searchPlaceholder')}
            value={props.query}
            onChange={(e) => props.onQueryChange(e.target.value)}
          />
        </BoardToolbar.Spacer>
      </BoardToolbar.Row>

      <BoardToolbar.Row>
        {/* Row shaping (how the roster is grouped) belongs with sort/filter, not
            with the time window — the first row is the time filter's grammar. */}
        <BoardToolbar.Label>{t('peopleBoard.toolbar.groupBy')}</BoardToolbar.Label>
        <Segmented<GroupBy>
          size="small"
          value={props.groupBy}
          onChange={props.onGroupByChange}
          options={[
            { value: 'role', label: t('peopleBoard.toolbar.groupRole') },
            { value: 'team', label: t('peopleBoard.toolbar.groupTeam') },
          ]}
        />
        <BoardToolbar.Divider />
        <BoardToolbar.Label>{t('peopleBoard.toolbar.bands')}</BoardToolbar.Label>
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
        <BoardToolbar.Divider />
        <BoardToolbar.Label as="label" title={t('peopleBoard.toolbar.countTentativeHint')}>
          <Switch
            size="small"
            checked={props.countTentative}
            onChange={props.onCountTentativeChange}
          />{' '}
          {t('peopleBoard.toolbar.countTentative')}
        </BoardToolbar.Label>
        <BoardToolbar.Spacer>
          <BoardToolbar.Count>
            <strong>{props.resultCount}</strong>{' '}
            {t(props.resultCount === 1 ? 'peopleBoard.toolbar.resultOne' : 'peopleBoard.toolbar.resultMany')}
            {props.resultCount !== props.totalCount
              ? ` ${t('peopleBoard.toolbar.ofTotal', { total: props.totalCount })}`
              : ''}
          </BoardToolbar.Count>
          <SortAscendingOutlined className={styles.dimIcon} />
          <Select
            size="small"
            value={props.sort}
            onChange={props.onSortChange}
            options={SORTS.map((s) => ({ value: s, label: t(`peopleBoard.sort.${s}`) }))}
            popupMatchSelectWidth={false}
          />
        </BoardToolbar.Spacer>
      </BoardToolbar.Row>
    </BoardToolbar>
  );
}
