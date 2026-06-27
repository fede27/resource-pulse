import { createStyles } from 'antd-style';

export const useStyles = createStyles(({ token, css }) => ({
  statsRow: css`
    margin-block-end: ${token.margin}px;
  `,
  col: css`
    margin-block-end: ${token.margin}px;
  `,
  emptyIcon: css`
    font-size: 48px;
    color: ${token.colorTextQuaternary};
  `,
  noneTitle: css`
    font-weight: 500;
    margin-block-end: ${token.marginXXS}px;
  `,
  noneDesc: css`
    color: ${token.colorTextTertiary};
    font-size: ${token.fontSizeSM}px;
  `,
}));
