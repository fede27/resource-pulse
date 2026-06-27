import { createStyles } from 'antd-style';

export const useStyles = createStyles(({ token, css }) => ({
  frame: css`
    border: 1px solid ${token.colorBorderSecondary};
    border-radius: 10px;
    overflow: hidden;
    background: ${token.colorBgContainer};
  `,
  scroll: css`
    overflow: auto;
    position: relative;
  `,
  overlay: css`
    position: absolute;
    top: 0;
    height: 100%;
    display: none;
    background: color-mix(in srgb, ${token.colorPrimary} 6%, transparent);
    pointer-events: none;
    z-index: 6;
  `,
  stickyHead: css`
    position: sticky;
    top: 0;
    z-index: 30;
  `,
  headBand: css`
    position: relative;
    background: ${token.colorFillQuaternary};
  `,
  corner: css`
    position: sticky;
    left: 0;
    z-index: 42;
    background: ${token.colorFillQuaternary};
    border-right: 1px solid ${token.colorBorder};
    display: flex;
    align-items: center;
  `,
  cornerBorderLight: css`
    border-bottom: 1px solid ${token.colorBorderSecondary};
  `,
  cornerBorderDark: css`
    border-bottom: 1px solid ${token.colorBorder};
  `,
  groupCell: css`
    position: absolute;
    top: 0;
    background: ${token.colorFillQuaternary};
    border-bottom: 1px solid ${token.colorBorderSecondary};
    border-left: 1px solid ${token.colorBorder};
    display: flex;
    align-items: center;
    padding: 0 ${token.paddingSM}px;
    font-size: ${token.fontSizeSM}px;
    font-weight: 600;
    color: ${token.colorTextSecondary};
  `,
  bucketCell: css`
    position: absolute;
    top: 0;
  `,
  rowName: css`
    position: sticky;
    left: 0;
    z-index: 20;
    border-right: 1px solid ${token.colorBorder};
    display: flex;
    align-items: center;
  `,
}));
