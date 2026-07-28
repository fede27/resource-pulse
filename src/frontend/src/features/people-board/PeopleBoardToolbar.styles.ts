import { createStyles } from 'antd-style';

// Only what is specific to THIS toolbar's content. The card, its rows, the
// dividers, the labels, the right-hand spacer and the result count come from the
// shared `BoardToolbar` — do not re-declare them here.
export const useStyles = createStyles(({ token, css }) => ({
  dimIcon: css`
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextTertiary};
  `,
  search: css`
    width: 200px;
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
}));
