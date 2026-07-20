import { createStyles } from 'antd-style';

export const useStyles = createStyles(({ token, css }) => ({
  form: css`
    margin-block-start: ${token.margin}px;
  `,
  fullWidth: css`
    width: 100%;
  `,
}));
