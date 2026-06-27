import { createStyles } from 'antd-style';

export const useStyles = createStyles(({ token, css }) => ({
  mb: css`
    margin-block-end: ${token.margin}px;
  `,
}));
