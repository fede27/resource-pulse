import { createStyles } from 'antd-style';

export const useStyles = createStyles(({ token, css }) => ({
  root: css`
    padding: ${token.pageGutter}px;
    max-width: ${token.pageMaxWidth}px;
  `,
  fullBleed: css`
    padding: ${token.pageGutter}px;
  `,
}));
