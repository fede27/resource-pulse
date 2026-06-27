import { createStyles } from 'antd-style';

export const useStyles = createStyles(({ token, css }) => ({
  root: css`
    background: ${token.colorBgContainer};
    border: 1px solid ${token.colorBorderSecondary};
    border-radius: ${token.borderRadiusLG}px;
    overflow: hidden;
  `,
  header: css`
    padding: ${token.padding}px 20px;
    border-bottom: 1px solid ${token.colorBorderSecondary};
    display: flex;
    justify-content: space-between;
    align-items: flex-start;
    gap: ${token.margin}px;
  `,
  headerText: css`
    min-width: 0;
  `,
  title: css`
    font-size: ${token.fontSizeLG}px;
    font-weight: 600;
  `,
  subtitle: css`
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextTertiary};
    margin-block-start: 3px;
    max-width: 640px;
    line-height: 1.5;
  `,
  dirtyBadge: css`
    flex-shrink: 0;
    height: 22px;
    padding: 0 ${token.paddingXS}px;
    border-radius: 11px;
    font-size: ${token.fontSizeSM}px;
    font-weight: 500;
    background: ${token.colorWarningBg};
    border: 1px solid ${token.colorWarningBorder};
    color: ${token.colorWarningText};
    display: inline-flex;
    align-items: center;
    gap: 5px;
  `,
  dirtyDot: css`
    width: 6px;
    height: 6px;
    border-radius: 50%;
    background: ${token.colorWarning};
  `,
  body: css`
    padding: 20px;
  `,
  footer: css`
    padding: ${token.paddingSM}px 20px;
    border-top: 1px solid ${token.colorBorderSecondary};
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: ${token.marginSM}px;
    background: ${token.colorFillQuaternary};
  `,
  footerNote: css`
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextTertiary};
  `,
  footerActions: css`
    display: flex;
    gap: ${token.marginXS}px;
  `,
}));
