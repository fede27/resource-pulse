import { createStyles } from 'antd-style';

export const useStyles = createStyles(({ token, css }) => ({
  root: css`
    padding: ${token.padding}px;
    background: ${token.colorFillQuaternary};
    border-bottom: 1px solid ${token.colorBorderSecondary};
  `,
  head: css`
    display: flex;
    align-items: center;
    justify-content: space-between;
    margin-bottom: ${token.marginSM}px;
  `,
  title: css`
    font-size: ${token.fontSize}px;
    font-weight: 600;
  `,
  close: css`
    cursor: pointer;
    color: ${token.colorTextTertiary};
    display: inline-flex;
  `,
  twoCol: css`
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: ${token.marginXS}px;
    margin-bottom: ${token.marginXS}px;
  `,
  full: css`
    margin-bottom: ${token.marginXS}px;
  `,
  err: css`
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorError};
    margin-bottom: ${token.marginXS}px;
  `,
  actions: css`
    display: flex;
    justify-content: flex-end;
    gap: ${token.marginXXS}px;
    margin-top: ${token.marginXS}px;
  `,
}));
