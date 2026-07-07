import { createStyles } from 'antd-style';

export const useStyles = createStyles(({ token, css }) => ({
  toolbar: css`
    background: ${token.colorBgContainer};
    border: 1px solid ${token.colorBorderSecondary};
    border-radius: ${token.borderRadiusLG}px;
    margin-block-end: ${token.margin}px;
  `,
  controls: css`
    display: flex;
    align-items: center;
    gap: ${token.marginXS}px;
    padding: ${token.paddingXS}px ${token.paddingSM}px;
    flex-wrap: wrap;
    border-block-end: 1px solid ${token.colorSplit};
  `,
  secondRow: css`
    display: flex;
    align-items: center;
    gap: ${token.marginXS}px;
    padding: ${token.paddingXS}px ${token.paddingSM}px;
    flex-wrap: wrap;
  `,
  divider: css`
    width: 1px;
    height: 22px;
    background: ${token.colorSplit};
  `,
  dimLabel: css`
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextTertiary};
  `,
  search: css`
    width: 200px;
  `,
  spacer: css`
    margin-inline-start: auto;
    display: inline-flex;
    align-items: center;
    gap: ${token.marginXS}px;
  `,
  bandButton: css`
    display: inline-flex;
    align-items: center;
    gap: 6px;
    height: 28px;
    padding: 0 ${token.paddingXS}px;
    border-radius: ${token.borderRadius}px;
    border: 1px solid ${token.colorBorder};
    background: ${token.colorBgContainer};
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextSecondary};
    cursor: pointer;
    transition: all 0.15s;

    &:hover {
      border-color: ${token.colorPrimaryBorder};
    }
  `,
  bandButtonOn: css`
    font-weight: 600;
  `,
  bandSwatch: css`
    width: 10px;
    height: 10px;
    border-radius: 2px;
    flex-shrink: 0;
  `,
  resultCount: css`
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextTertiary};
    font-variant-numeric: tabular-nums;

    strong {
      color: ${token.colorTextSecondary};
    }
  `,
}));
