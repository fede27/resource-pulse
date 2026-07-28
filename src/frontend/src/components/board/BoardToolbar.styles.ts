import { createStyles } from 'antd-style';

export const useStyles = createStyles(({ token, css }) => ({
  toolbar: css`
    background: ${token.colorBgContainer};
    border: 1px solid ${token.colorBorderSecondary};
    border-radius: ${token.borderRadiusLG}px;
    margin-block-end: ${token.margin}px;
  `,
  row: css`
    display: flex;
    align-items: center;
    gap: ${token.marginXS}px;
    padding: ${token.paddingXS}px ${token.paddingSM}px;
    flex-wrap: wrap;

    /* Bands, not a list: every row after the first is separated by a hairline.
       The separator belongs to the stack, so no row has to know its index. */
    & + & {
      border-block-start: 1px solid ${token.colorSplit};
    }
  `,
  divider: css`
    width: 1px;
    height: 22px;
    background: ${token.colorSplit};
  `,
  label: css`
    display: inline-flex;
    align-items: center;
    gap: ${token.marginXXS}px;
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextTertiary};
  `,
  spacer: css`
    margin-inline-start: auto;
    display: inline-flex;
    align-items: center;
    gap: ${token.marginXS}px;
  `,
  count: css`
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextTertiary};
    font-weight: 500;
    font-variant-numeric: tabular-nums;

    strong {
      color: ${token.colorTextSecondary};
    }
  `,
}));
