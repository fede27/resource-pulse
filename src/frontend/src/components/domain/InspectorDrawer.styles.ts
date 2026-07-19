import { createStyles } from 'antd-style';

// The one width every inspector shares — change it here, every surface follows.
export const INSPECTOR_SIZE = 460;

export const useStyles = createStyles(({ token, css }) => ({
  subtitle: css`
    font-size: ${token.fontSizeSM}px;
    font-weight: 400;
    color: ${token.colorTextTertiary};
    margin-block-start: 2px;
  `,
  footer: css`
    display: flex;
    justify-content: flex-end;
    gap: ${token.marginXS}px;
  `,
}));
