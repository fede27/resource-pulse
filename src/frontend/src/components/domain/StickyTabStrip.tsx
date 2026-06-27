import type { Key, ReactNode } from 'react';
import { createStyles } from 'antd-style';

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

const useStyles = createStyles(({ token, css }) => ({
  root: css`
    background: ${token.colorBgContainer};
    border-bottom: 1px solid ${token.colorBorderSecondary};
    padding: 0 ${token.paddingLG}px;
    display: flex;
    align-items: center;
    justify-content: space-between;
    position: sticky;
    /* Sit directly under the sticky app header. */
    top: ${token.layoutHeaderHeight}px;
    z-index: 5;
  `,
  tabs: css`
    display: flex;
    gap: ${token.marginXL}px;
  `,
  tab: css`
    padding: ${token.padding}px 0;
    cursor: pointer;
    position: relative;
    font-size: 15px;
    font-weight: 400;
    color: ${token.colorText};
    display: inline-flex;
    align-items: center;
    gap: ${token.marginXS}px;
    transition: color ${token.motionDurationFast};
  `,
  tabActive: css`
    font-weight: 500;
    color: ${token.colorPrimary};
  `,
  count: css`
    font-size: 11px;
    font-weight: 500;
    font-variant-numeric: tabular-nums;
    min-width: 20px;
    padding: 0 ${token.paddingXXS}px;
    height: 18px;
    border-radius: 9px;
    background: ${token.colorFillSecondary};
    color: ${token.colorTextSecondary};
    display: inline-flex;
    align-items: center;
    justify-content: center;
  `,
  countActive: css`
    background: ${token.colorPrimaryBg};
    color: ${token.colorPrimaryText};
  `,
  underline: css`
    position: absolute;
    bottom: -1px;
    left: 0;
    right: 0;
    height: 2px;
    background: ${token.colorPrimary};
  `,
  extra: css`
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextTertiary};
    display: inline-flex;
    align-items: center;
    gap: ${token.marginXXS}px;
  `,
}));

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
