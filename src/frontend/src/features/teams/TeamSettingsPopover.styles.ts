import { createStyles } from 'antd-style';

export const useStyles = createStyles(({ token, css }) => ({
  content: css`
    width: 248px;
    display: flex;
    flex-direction: column;
    gap: ${token.marginSM}px;
  `,
  label: css`
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextTertiary};
    margin-block-end: ${token.marginXXS}px;
  `,
  toggleRow: css`
    display: flex;
    align-items: center;
    justify-content: space-between;
  `,
  toggleLabel: css`
    font-size: ${token.fontSizeSM}px;
  `,
}));
