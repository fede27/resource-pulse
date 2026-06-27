import { createStyles } from 'antd-style';

export const useStyles = createStyles(({ token, css }) => ({
  titleStrong: css`
    font-weight: 600;
  `,
  emptyWrap: css`
    padding: ${token.paddingLG}px;
  `,
  row: css`
    position: relative;
    padding: 14px ${token.padding}px;
    cursor: pointer;
    border-bottom: 1px solid ${token.colorBorderSecondary};
    background: transparent;
    transition: background ${token.motionDurationFast};
  `,
  rowSelected: css`
    background: ${token.colorPrimaryBg};
  `,
  accent: css`
    position: absolute;
    left: 0;
    top: 0;
    bottom: 0;
    width: 3px;
    background: ${token.colorPrimary};
  `,
  rowHead: css`
    margin-block-end: ${token.marginXXS}px;
  `,
  nameStrong: css`
    font-weight: 500;
  `,
  tagItem: css`
    margin: 0;
  `,
  summary: css`
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextTertiary};
    line-height: 1.5;
  `,
  hours: css`
    font-size: 11px;
    color: ${token.colorTextTertiary};
    margin-block-start: ${token.marginXXS}px;
    font-variant-numeric: tabular-nums;
  `,
  formRoot: css`
    padding: 14px;
    background: ${token.colorFillQuaternary};
    border-bottom: 1px solid ${token.colorBorderSecondary};
  `,
  formError: css`
    color: ${token.colorError};
    font-size: ${token.fontSizeSM}px;
    margin-block-start: ${token.marginXXS}px;
  `,
  checkboxRow: css`
    margin-block-start: ${token.marginSM}px;
  `,
  defaultHint: css`
    margin-block-start: ${token.marginXXS}px;
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextTertiary};
    margin-inline-start: 24px;
  `,
  formFooter: css`
    margin-block-start: ${token.marginSM}px;
    display: flex;
    justify-content: flex-end;
    gap: ${token.marginXS}px;
  `,
}));
