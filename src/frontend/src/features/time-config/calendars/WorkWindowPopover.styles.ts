import { createStyles } from 'antd-style';

export const useStyles = createStyles(({ token, css }) => ({
  form: css`
    width: 300px;
  `,
  fullWidth: css`
    width: 100%;
  `,
  half: css`
    width: 50%;
  `,
  caption: css`
    font-size: ${token.fontSizeSM}px;
  `,
  footer: css`
    margin-block-start: ${token.marginSM}px;
    display: flex;
    justify-content: space-between;
    gap: ${token.marginXS}px;
  `,
  inspect: css`
    width: 300px;
  `,
  inspectTitle: css`
    font-weight: 600;
    margin-block-end: ${token.marginXS}px;
  `,
  factRow: css`
    display: flex;
    justify-content: space-between;
    gap: ${token.marginSM}px;
    padding: 6px 0;
    border-bottom: 1px solid ${token.colorSplit};
    font-size: ${token.fontSize}px;
  `,
  factLabel: css`
    color: ${token.colorTextTertiary};
  `,
  factValue: css`
    font-variant-numeric: tabular-nums;
    text-align: right;
  `,
  stateRow: css`
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding: ${token.paddingXS}px 0 2px;
  `,
  note: css`
    margin-block-start: ${token.marginXS}px;
    padding: ${token.paddingXS}px ${token.paddingSM}px;
    background: ${token.colorFillQuaternary};
    border-radius: ${token.borderRadius}px;
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextSecondary};
    line-height: 1.5;
  `,
  muted: css`
    color: ${token.colorTextTertiary};
  `,
}));
