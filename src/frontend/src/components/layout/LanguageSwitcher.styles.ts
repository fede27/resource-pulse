import { createStyles } from 'antd-style';

export const useStyles = createStyles(({ token, css }) => ({
  trigger: css`
    display: inline-flex;
    align-items: center;
    gap: ${token.marginXXS}px;
    cursor: pointer;
    padding: ${token.paddingXXS}px ${token.paddingXS}px;
    font-size: ${token.fontSizeSM}px;
  `,
  icon: css`
    font-size: ${token.fontSizeLG}px;
  `,
  code: css`
    text-transform: uppercase;
    font-weight: 500;
  `,
}));
