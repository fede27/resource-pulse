import { createStyles } from 'antd-style';

export const useStyles = createStyles(({ token, css }) => ({
  form: css`
    width: 300px;
  `,
  fullWidth: css`
    width: 100%;
  `,
  half: css`
    width: 50%;
  `,
  caption: css`
    font-size: ${token.fontSizeSM}px;
  `,
  footer: css`
    margin-block-start: ${token.marginSM}px;
    display: flex;
    justify-content: space-between;
    gap: ${token.marginXS}px;
  `,
}));
