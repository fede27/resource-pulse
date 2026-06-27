import { createStyles } from 'antd-style';

export const useStyles = createStyles(({ token, css }) => ({
  card: css`
    height: 100%;
  `,
  label: css`
    color: ${token.colorTextTertiary};
    font-size: ${token.fontSizeSM}px;
  `,
  valueRow: css`
    margin-block-start: ${token.marginXXS}px;
    display: flex;
    align-items: baseline;
    gap: ${token.marginXXS}px;
  `,
  value: css`
    font-size: 28px;
    font-weight: ${token.fontWeightStrong};
    font-variant-numeric: tabular-nums;
    /* Falls back to the body text colour when no accent is supplied. */
    color: var(--stat-accent, ${token.colorText});
    line-height: 1.1;
  `,
  suffix: css`
    color: ${token.colorTextTertiary};
    font-size: ${token.fontSizeSM}px;
  `,
}));
