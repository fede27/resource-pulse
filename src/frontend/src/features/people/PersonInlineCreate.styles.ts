import { createStyles } from 'antd-style';

export const useStyles = createStyles(({ token, css }) => ({
  root: css`
    padding: 14px;
    background: ${token.colorFillQuaternary};
    border-bottom: 1px solid ${token.colorBorderSecondary};
  `,
  header: css`
    display: flex;
    align-items: center;
    justify-content: space-between;
    margin-block-end: ${token.marginSM}px;
  `,
  title: css`
    font-size: ${token.fontSizeSM}px;
  `,
  close: css`
    cursor: pointer;
    color: ${token.colorTextTertiary};
    display: inline-flex;
  `,
  closeGlyph: css`
    font-size: 11px;
  `,
  field: css`
    margin-block-end: ${token.marginXS}px;
  `,
  fieldLast: css`
    margin-block-end: ${token.marginSM}px;
  `,
  footer: css`
    display: flex;
    justify-content: flex-end;
  `,
}));
