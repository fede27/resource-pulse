import { createStyles } from 'antd-style';

export const useStyles = createStyles(({ token, css }) => ({
  row: css`
    display: flex;
    gap: 40px;
    flex-wrap: wrap;
  `,
  fieldLabel: css`
    font-size: ${token.fontSizeSM}px;
    font-weight: 500;
    margin-block-end: ${token.marginXS}px;
  `,
  error: css`
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorError};
    margin-block-start: ${token.marginSM}px;
  `,
  summary: css`
    margin-block-start: ${token.margin}px;
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorText};
  `,
}));
