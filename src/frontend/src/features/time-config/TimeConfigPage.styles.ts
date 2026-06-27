import { createStyles } from 'antd-style';

export const useStyles = createStyles(({ token, css }) => ({
  refIcon: css`
    font-size: ${token.fontSizeSM}px;
  `,
  refDate: css`
    font-variant-numeric: tabular-nums;
    color: ${token.colorTextSecondary};
  `,
  content: css`
    padding: ${token.pageGutter}px;
    max-width: ${token.pageMaxWidth}px;
  `,
}));
