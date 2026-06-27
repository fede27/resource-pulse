import { createStyles } from 'antd-style';

export const useStyles = createStyles(({ token, css }) => ({
  card: css`
    border: 1px solid ${token.colorPrimary};
    box-shadow: 0 0 0 2px ${token.colorPrimaryBorder};
  `,
  header: css`
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-block-end: ${token.marginSM}px;
  `,
  fieldSm: css`
    margin-block-end: ${token.marginXS}px;
  `,
  fieldMd: css`
    margin-block-end: ${token.marginSM}px;
  `,
  footer: css`
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-block-start: ${token.marginSM}px;
    gap: ${token.marginXS}px;
  `,
  daysTag: css`
    font-variant-numeric: tabular-nums;
    margin: 0;
  `,
}));
