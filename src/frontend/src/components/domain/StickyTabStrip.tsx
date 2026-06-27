import type { Key, ReactNode } from 'react';
import { useStyles } from './StickyTabStrip.styles';

export type StickyTabItem<K extends Key = string> = {
  key: K;
  label: ReactNode;
  /** Small chip rendered next to the label, e.g. a count. */
  count?: ReactNode;
};

export type StickyTabStripProps<K extends Key = string> = {
  items: StickyTabItem<K>[];
  activeKey: K;
  onChange: (key: K) => void;
  /** Anything rendered on the right side of the strip (e.g. a reference date). */
  extra?: ReactNode;
};

export function StickyTabStrip<K extends Key = string>({
  items,
  activeKey,
  onChange,
  extra,
}: StickyTabStripProps<K>) {
  const { styles, cx } = useStyles();

  return (
    <div className={styles.root}>
      <div className={styles.tabs}>
        {items.map((it) => {
          const active = it.key === activeKey;
          return (
            <div
              key={String(it.key)}
              onClick={() => onChange(it.key)}
              className={cx(styles.tab, active && styles.tabActive)}
            >
              {it.label}
              {it.count !== undefined && it.count !== null && (
                <span className={cx(styles.count, active && styles.countActive)}>
                  {it.count}
                </span>
              )}
              {active && <span className={styles.underline} />}
            </div>
          );
        })}
      </div>
      {extra && <div className={styles.extra}>{extra}</div>}
    </div>
  );
}
