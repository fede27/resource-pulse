import type { Key, ReactNode } from 'react';
import { theme } from 'antd';

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
  const { token } = theme.useToken();

  return (
    <div
      style={{
        background: token.colorBgContainer,
        borderBottom: `1px solid ${token.colorBorderSecondary}`,
        padding: '0 24px',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        position: 'sticky',
        top: 64,
        zIndex: 5,
      }}
    >
      <div style={{ display: 'flex', gap: 32 }}>
        {items.map((it) => {
          const active = it.key === activeKey;
          return (
            <div
              key={String(it.key)}
              onClick={() => onChange(it.key)}
              style={{
                padding: '16px 0',
                cursor: 'pointer',
                position: 'relative',
                fontSize: 15,
                fontWeight: active ? 500 : 400,
                color: active ? token.colorPrimary : token.colorText,
                display: 'inline-flex',
                alignItems: 'center',
                gap: 8,
                transition: `color ${token.motionDurationFast}`,
              }}
            >
              {it.label}
              {it.count !== undefined && it.count !== null && (
                <span
                  style={{
                    fontSize: 11,
                    fontWeight: 500,
                    fontVariantNumeric: 'tabular-nums',
                    minWidth: 20,
                    padding: '0 6px',
                    height: 18,
                    borderRadius: 9,
                    background: active ? token.colorPrimaryBg : token.colorFillSecondary,
                    color: active ? token.colorPrimaryText : token.colorTextSecondary,
                    display: 'inline-flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                  }}
                >
                  {it.count}
                </span>
              )}
              {active && (
                <span
                  style={{
                    position: 'absolute',
                    bottom: -1,
                    left: 0,
                    right: 0,
                    height: 2,
                    background: token.colorPrimary,
                  }}
                />
              )}
            </div>
          );
        })}
      </div>
      {extra && (
        <div
          style={{
            fontSize: 12,
            color: token.colorTextTertiary,
            display: 'inline-flex',
            alignItems: 'center',
            gap: 6,
          }}
        >
          {extra}
        </div>
      )}
    </div>
  );
}
