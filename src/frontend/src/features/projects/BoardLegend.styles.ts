import { createStyles } from 'antd-style';

export const useStyles = createStyles(({ token, css }) => ({
  legend: css`
    display: flex;
    flex-direction: column;
    gap: ${token.marginSM}px;
    padding: ${token.paddingSM}px ${token.padding}px;
    background: ${token.colorFillQuaternary};
    border: 1px solid ${token.colorBorderSecondary};
    border-radius: ${token.borderRadiusLG}px;
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextSecondary};
    margin-block-end: ${token.margin}px;
  `,
  row: css`
    display: flex;
    flex-wrap: wrap;
    gap: ${token.marginXS}px ${token.marginLG}px;
    align-items: center;
  `,
  rowSecondary: css`
    padding-block-start: ${token.paddingXS}px;
    border-block-start: 1px solid ${token.colorBorderSecondary};
  `,
  title: css`
    font-weight: 600;
    color: ${token.colorText};
  `,
  item: css`
    display: inline-flex;
    align-items: center;
    gap: 6px;
  `,
  stripe: css`
    width: 4px;
    height: 16px;
    border-radius: 2px;
    flex-shrink: 0;
  `,
  swatch: css`
    width: 22px;
    height: 14px;
    border-radius: 3px;
    flex-shrink: 0;
  `,
  hint: css`
    color: ${token.colorTextQuaternary};
  `,
  divider: css`
    width: 1px;
    height: 16px;
    background: ${token.colorBorder};
  `,
  chipSolid: css`
    border: 1px solid ${token.colorBorder};
    border-radius: 3px;
    padding: 0 4px;
  `,
  chipDashed: css`
    border: 1px dashed ${token.colorBorder};
    border-radius: 3px;
    padding: 0 4px;
  `,
}));
