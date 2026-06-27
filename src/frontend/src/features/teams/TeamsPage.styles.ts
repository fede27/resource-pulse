import { createStyles } from 'antd-style';

export const useStyles = createStyles(({ token, css }) => ({
  statsRow: css`
    margin-block-end: ${token.margin}px;
  `,
  toolbar: css`
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: ${token.margin}px;
    margin-block-end: ${token.marginSM}px;
    flex-wrap: wrap;
  `,
  switchGroup: css`
    margin-inline-start: ${token.marginXXS}px;
  `,
  switchLabel: css`
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextTertiary};
  `,
  legend: css`
    display: flex;
    align-items: center;
    gap: ${token.marginSM}px;
    flex-wrap: wrap;
  `,
  legendTitle: css`
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextTertiary};
  `,
  legendItems: css`
    display: flex;
    align-items: center;
    gap: ${token.marginSM}px;
  `,
  legendItem: css`
    display: inline-flex;
    align-items: center;
    gap: 6px;
  `,
  swatch: css`
    width: 12px;
    height: 12px;
    border-radius: 3px;
    flex-shrink: 0;
  `,
  legendLabel: css`
    font-size: ${token.fontSizeSM}px;
    font-weight: 500;
    color: ${token.colorTextSecondary};
  `,
  legendRange: css`
    font-size: 11px;
    color: ${token.colorTextTertiary};
    font-variant-numeric: tabular-nums;
  `,
  footnote: css`
    margin-block-start: ${token.marginSM}px;
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextTertiary};
    display: flex;
    align-items: center;
    gap: 6px;
  `,
  footnoteDot: css`
    width: 3px;
    height: 3px;
    border-radius: 50%;
    background: ${token.colorTextQuaternary};
    display: inline-block;
  `,
  emptyHint: css`
    margin-block-start: ${token.marginXXS}px;
  `,
}));
