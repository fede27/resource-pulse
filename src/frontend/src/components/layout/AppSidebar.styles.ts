import { createStyles } from 'antd-style';

export const useStyles = createStyles(({ token, css }) => ({
  sider: css`
    border-right: 1px solid ${token.colorBorderSecondary};
  `,
  brand: css`
    height: ${token.layoutHeaderHeight}px;
    display: flex;
    align-items: center;
    gap: 10px;
    padding: 0 ${token.padding}px;
    justify-content: flex-start;
    border-bottom: 1px solid ${token.colorBorderSecondary};
    cursor: pointer;
  `,
  brandCollapsed: css`
    padding: 0;
    justify-content: center;
  `,
  mark: css`
    width: 28px;
    height: 28px;
    border-radius: ${token.borderRadius}px;
    background: ${token.brandGradient};
    display: flex;
    align-items: center;
    justify-content: center;
    color: #fff;
    font-weight: 700;
    font-size: ${token.fontSize}px;
    box-shadow: ${token.brandLogoShadow};
    flex-shrink: 0;
  `,
  titleWrap: css`
    min-width: 0;
  `,
  title: css`
    font-weight: 600;
    font-size: 15px;
    line-height: 1;
  `,
  subtitle: css`
    font-size: 11px;
    color: ${token.colorTextTertiary};
    margin-block-start: 3px;
    letter-spacing: 0.02em;
  `,
  menu: css`
    border-right: 0;
  `,
}));
