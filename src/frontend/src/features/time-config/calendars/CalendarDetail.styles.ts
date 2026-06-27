import { createStyles } from 'antd-style';

export const useStyles = createStyles(({ token, css }) => ({
  stack: css`
    display: flex;
    flex-direction: column;
    gap: ${token.margin}px;
  `,
  headerRow: css`
    display: flex;
    align-items: flex-start;
    justify-content: space-between;
    gap: ${token.margin}px;
  `,
  flex1: css`
    flex: 1;
    min-width: 0;
  `,
  titleSpace: css`
    margin-block-end: ${token.marginXXS}px;
  `,
  renameInput: css`
    max-width: 320px;
  `,
  titleReset: css`
    margin: 0;
  `,
  viewBar: css`
    margin-block-start: ${token.margin}px;
    padding-block-start: ${token.margin}px;
    border-top: 1px solid ${token.colorBorderSecondary};
    display: flex;
    align-items: center;
    gap: ${token.marginSM}px;
    flex-wrap: wrap;
  `,
  caption: css`
    font-size: ${token.fontSizeSM}px;
  `,
  linkBtn: css`
    padding: 0;
  `,
  tnum: css`
    font-variant-numeric: tabular-nums;
  `,
}));
