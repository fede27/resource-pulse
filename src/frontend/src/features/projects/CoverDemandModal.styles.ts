import { createStyles } from 'antd-style';

export const COVER_MODAL_WIDTH = 640;

export const useStyles = createStyles(({ token, css }) => ({
  // ── The demand: required / already covered / this dialog / left ────────
  demand: css`
    margin-block: ${token.marginSM}px ${token.margin}px;
  `,
  demandHead: css`
    display: flex;
    align-items: baseline;
    flex-wrap: wrap;
    gap: ${token.marginXS}px;
    margin-block-end: 6px;
  `,
  demandTitle: css`
    font-size: ${token.fontSize}px;
    font-weight: 600;
    color: ${token.colorText};
  `,
  demandMeta: css`
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextSecondary};
    font-variant-numeric: tabular-nums;
  `,
  track: css`
    display: flex;
    height: 10px;
    border-radius: 3px;
    overflow: hidden;
    border: 1px solid ${token.colorBorder};
    background: ${token.colorBgContainer};
  `,
  trackCovered: css`
    background: ${token.colorPrimaryBorder};
  `,
  trackAdding: css`
    background: ${token.colorPrimary};
  `,
  balance: css`
    display: flex;
    flex-wrap: wrap;
    gap: ${token.margin}px;
    margin-block-start: 6px;
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextSecondary};
    font-variant-numeric: tabular-nums;

    b {
      color: ${token.colorText};
    }
  `,
  balanceAdding: css`
    && {
      color: ${token.colorPrimaryText};
      font-weight: 600;
    }
  `,
  unitRow: css`
    display: flex;
    align-items: center;
    gap: ${token.marginXS}px;
    margin-block-end: ${token.marginXS}px;
    font-size: ${token.fontSize}px;
  `,
  unitSpacer: css`
    margin-inline-start: auto;
  `,
  // ── One row = one coverage ─────────────────────────────────────────────
  row: css`
    border: 1px solid ${token.colorBorderSecondary};
    border-radius: ${token.borderRadiusLG}px;
    padding: ${token.paddingSM}px;
    margin-block-end: ${token.marginXS}px;
  `,
  rowHead: css`
    display: flex;
    align-items: center;
    gap: ${token.marginXS}px;
  `,
  person: css`
    flex: 1;
    min-width: 0;
  `,
  amount: css`
    width: 124px;
    flex-shrink: 0;
  `,
  derived: css`
    width: 96px;
    flex-shrink: 0;
    text-align: end;
    font-size: ${token.fontSize}px;
    font-variant-numeric: tabular-nums;
    color: ${token.colorText};
    white-space: nowrap;
  `,
  links: css`
    display: flex;
    flex-wrap: wrap;
    gap: ${token.marginSM}px;
    margin-block-start: 6px;
    font-size: ${token.fontSizeSM}px;
  `,
  fact: css`
    margin-block-start: 6px;
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextSecondary};
    line-height: 1.5;
    font-variant-numeric: tabular-nums;
  `,
  factWarn: css`
    color: ${token.colorWarningText};
  `,
  buckets: css`
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(132px, 1fr));
    gap: 6px;
    margin-block-start: ${token.marginXS}px;
  `,
  bucket: css`
    border: 1px solid ${token.colorBorderSecondary};
    border-radius: ${token.borderRadius}px;
    padding: 6px 8px;
    min-width: 0;
  `,
  bucketLabel: css`
    font-size: 11px;
    color: ${token.colorTextSecondary};
    white-space: nowrap;
  `,
  bucketChips: css`
    display: flex;
    align-items: center;
    gap: 4px;
    margin-block-start: 4px;
    color: ${token.colorTextSecondary};
  `,
  bucketHours: css`
    margin-block-start: 3px;
    font-size: 11px;
    color: ${token.colorTextSecondary};
    font-variant-numeric: tabular-nums;
  `,
  chip: css`
    display: inline-flex;
    align-items: center;
    justify-content: center;
    min-width: 42px;
    height: 20px;
    padding: 0 4px;
    border-radius: ${token.borderRadiusSM}px;
    border: 1px solid transparent;
    font-size: 11.5px;
    font-weight: 600;
    font-variant-numeric: tabular-nums;
  `,
  // ── The open picker: free hours and needed quota next to each name ─────
  option: css`
    display: flex;
    align-items: baseline;
    gap: ${token.marginXS}px;
  `,
  optionName: css`
    flex: 1;
    min-width: 0;
    overflow: hidden;
    text-overflow: ellipsis;
  `,
  optionFacts: css`
    flex-shrink: 0;
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextSecondary};
    font-variant-numeric: tabular-nums;
  `,
  footer: css`
    display: flex;
    align-items: center;
    gap: ${token.marginXS}px;
  `,
  footerNote: css`
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextSecondary};
  `,
  footerEnd: css`
    margin-inline-start: auto;
    display: flex;
    gap: ${token.marginXS}px;
  `,
}));
