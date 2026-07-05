import { createStyles } from 'antd-style';

export const useStyles = createStyles(({ token, css }) => ({
  row: css`
    display: grid;
    grid-template-columns: repeat(3, 1fr);
    gap: ${token.margin}px;
    margin-block-end: ${token.margin}px;

    @media (max-width: 768px) {
      grid-template-columns: 1fr;
    }
  `,
  card: css`
    background: ${token.colorBgContainer};
    border: 1px solid ${token.colorBorderSecondary};
    border-radius: ${token.borderRadiusLG}px;
    padding: ${token.padding}px;
    border-left-width: 3px;
    border-left-style: solid;
  `,
  label: css`
    font-size: ${token.fontSize}px;
    color: ${token.colorTextTertiary};
  `,
  value: css`
    margin-block-start: ${token.marginXXS}px;
    font-size: 28px;
    font-weight: 600;
    font-variant-numeric: tabular-nums;
    line-height: 1.1;
  `,
  foot: css`
    margin-block-start: ${token.marginXXS}px;
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextTertiary};
  `,
}));
