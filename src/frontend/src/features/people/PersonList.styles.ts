import { createStyles } from 'antd-style';

export const useStyles = createStyles(({ token, css }) => ({
  root: css`
    background: ${token.colorBgContainer};
    border: 1px solid ${token.colorBorderSecondary};
    border-radius: ${token.borderRadiusLG}px;
    overflow: hidden;
    display: flex;
    flex-direction: column;
  `,
  header: css`
    padding: ${token.paddingSM}px ${token.padding}px;
    display: flex;
    align-items: center;
    justify-content: space-between;
    border-bottom: 1px solid ${token.colorBorderSecondary};
    gap: ${token.marginXS}px;
  `,
  searchWrap: css`
    padding: ${token.paddingXS}px ${token.paddingSM}px;
    border-bottom: 1px solid ${token.colorBorderSecondary};
  `,
  searchIcon: css`
    color: ${token.colorTextTertiary};
    font-size: ${token.fontSizeSM}px;
  `,
  list: css`
    overflow: auto;
    flex: 1;
    max-height: 620px;
  `,
  row: css`
    position: relative;
    padding: ${token.paddingSM}px ${token.padding}px;
    cursor: pointer;
    border-bottom: 1px solid ${token.colorBorderSecondary};
    background: transparent;
    display: flex;
    align-items: center;
    gap: ${token.marginSM}px;
    transition: background ${token.motionDurationFast};
    &:hover {
      background: ${token.colorFillTertiary};
    }
  `,
  rowSelected: css`
    background: ${token.colorPrimaryBg};
    &:hover {
      background: ${token.colorPrimaryBg};
    }
  `,
  accent: css`
    position: absolute;
    left: 0;
    top: 0;
    bottom: 0;
    width: 3px;
    background: ${token.colorPrimary};
  `,
  rowBody: css`
    flex: 1;
    min-width: 0;
  `,
  name: css`
    font-size: ${token.fontSize}px;
    font-weight: 500;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
    color: ${token.colorText};
  `,
  nameInactive: css`
    color: ${token.colorTextTertiary};
  `,
  secondary: css`
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextTertiary};
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  `,
  pendingBadge: css`
    .ant-badge-count {
      background: ${token.colorWarningBg};
      color: ${token.colorWarningText};
      border: 1px solid ${token.colorWarningBorder};
      box-shadow: none;
      font-variant-numeric: tabular-nums;
    }
  `,
  emptyWrap: css`
    padding: ${token.paddingLG}px;
  `,
  emptyIcon: css`
    font-size: 44px;
    color: ${token.colorTextQuaternary};
  `,
}));
