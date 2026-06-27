import { createStyles } from 'antd-style';

export const useStyles = createStyles(({ token, css }) => ({
  root: css`
    width: 100%;
  `,
  createRow: css`
    display: inline-flex;
    align-items: center;
    gap: ${token.marginXS}px;
    width: 100%;
  `,
  createIcon: css`
    color: ${token.colorPrimary};
    font-size: ${token.fontSizeSM}px;
  `,
  prefixIcon: css`
    color: ${token.colorTextTertiary};
    font-size: 11px;
  `,
}));
