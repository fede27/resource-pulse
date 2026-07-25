import { createStyles } from 'antd-style';

export const useStyles = createStyles(({ token, css }) => ({
  viewSwitch: css`
    margin-bottom: ${token.marginLG}px;
  `,
}));
