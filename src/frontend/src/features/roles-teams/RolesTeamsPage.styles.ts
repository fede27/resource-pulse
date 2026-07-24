import { createStyles } from 'antd-style';

export const useStyles = createStyles(({ token, css }) => ({
  viewSwitch: css`
    margin-bottom: ${token.marginLG}px;
  `,
  chipRow: css`
    display: flex;
    gap: ${token.marginXS}px;
    flex-wrap: wrap;
  `,
  chip: css`
    display: inline-flex;
    align-items: center;
    gap: ${token.marginXXS}px;
    height: 26px;
    padding: 0 ${token.paddingSM}px;
    border-radius: 13px;
    font-size: ${token.fontSizeSM}px;
    font-weight: 500;
    white-space: nowrap;
  `,
  chipWarn: css`
    background: ${token.colorWarningBg};
    border: 1px solid ${token.colorWarningBorder};
    color: ${token.colorWarningText};
  `,
  chipOk: css`
    background: ${token.colorSuccessBg};
    border: 1px solid ${token.colorSuccessBorder};
    color: ${token.colorSuccessText};
  `,
  chipCount: css`
    font-variant-numeric: tabular-nums;
    font-weight: 700;
  `,
}));
