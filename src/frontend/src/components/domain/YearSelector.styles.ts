import { createStyles } from 'antd-style';

export const useStyles = createStyles(({ token, css }) => ({
  label: css`
    color: ${token.colorTextSecondary};
    font-size: ${token.fontSizeSM}px;
  `,
}));
