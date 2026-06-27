import { createStyles } from 'antd-style';

export const useStyles = createStyles(({ token, css }) => ({
  shell: css`
    min-height: 100vh;
  `,
  body: css`
    background: ${token.colorBgLayout};
  `,
  header: css`
    background: ${token.colorBgContainer};
    padding: 0 ${token.padding}px 0 ${token.paddingXS}px;
    display: flex;
    justify-content: space-between;
    align-items: center;
    border-bottom: 1px solid ${token.colorBorderSecondary};
    position: sticky;
    top: 0;
    z-index: 10;
  `,
  breadcrumb: css`
    font-size: ${token.fontSizeSM}px;
  `,
  divider: css`
    width: 1px;
    height: ${token.controlHeightSM}px;
    background: ${token.colorBorderSecondary};
    display: inline-block;
  `,
  bellIcon: css`
    font-size: 18px;
  `,
  user: css`
    cursor: pointer;
  `,
  avatar: css`
    background: ${token.colorPrimary};
  `,
  userName: css`
    font-size: ${token.fontSize}px;
  `,
  content: css`
    background: ${token.colorBgLayout};
  `,
}));
