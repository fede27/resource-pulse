import { createStyles } from 'antd-style';

export const useStyles = createStyles(({ token, css }) => ({
  root: css`
    display: flex;
    gap: ${token.marginXS}px;
    padding: ${token.paddingXS}px ${token.paddingSM}px;
    margin-block-start: ${token.marginSM}px;
    background: ${token.colorFillQuaternary};
    border: 1px solid ${token.colorBorderSecondary};
    border-radius: ${token.borderRadius}px;
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextSecondary};
    line-height: 1.5;
  `,
  icon: css`
    flex-shrink: 0;
    margin-block-start: 1px;
    color: ${token.colorTextQuaternary};
  `,
  iconGlyph: css`
    font-size: ${token.fontSizeSM}px;
  `,
  label: css`
    color: ${token.colorTextSecondary};
    font-weight: 500;
  `,
}));
