import { createStyles } from 'antd-style';

export const useStyles = createStyles(({ token, css }) => ({
  // One inline cluster: grain · year stepper · Oggi · Adatta · range. It brings
  // its own label and divider so a toolbar cannot re-spell the time grammar.
  group: css`
    display: inline-flex;
    align-items: center;
    gap: ${token.marginXS}px;
    flex-wrap: wrap;
  `,
  label: css`
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextTertiary};
  `,
  divider: css`
    width: 1px;
    height: 22px;
    background: ${token.colorSplit};
  `,
  yearStepper: css`
    display: inline-flex;
    align-items: center;
    height: 32px;
    border: 1px solid ${token.colorBorder};
    border-radius: ${token.borderRadius}px;
    overflow: hidden;
    background: ${token.colorBgContainer};
  `,
  yearLabel: css`
    padding: 0 ${token.paddingXS}px;
    font-size: ${token.fontSize}px;
    font-weight: 600;
    font-variant-numeric: tabular-nums;
    color: ${token.colorTextSecondary};
  `,
}));
