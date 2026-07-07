import { createStyles } from 'antd-style';

export const useStyles = createStyles(({ token, css }) => ({
  group: css`
    display: inline-flex;
    align-items: center;
    gap: ${token.marginXS}px;
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
