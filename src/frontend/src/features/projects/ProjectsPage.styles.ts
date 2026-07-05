import { createStyles } from 'antd-style';

export const useStyles = createStyles(({ token, css }) => ({
  footnote: css`
    margin-block-start: ${token.marginSM}px;
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextTertiary};
    display: flex;
    align-items: center;
    gap: 6px;
  `,
  fetchingHint: css`
    display: inline-flex;
    align-items: center;
    gap: ${token.marginXXS}px;
    margin-inline-start: ${token.marginXS}px;
  `,
}));
