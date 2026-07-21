import { createStyles } from 'antd-style';

export const POPOVER_WIDTH = 320;
export const POPOVER_HEIGHT_EST = 300;

export const useStyles = createStyles(({ token, css }) => ({
  card: css`
    position: fixed;
    z-index: ${token.zIndexPopupBase + 100};
    width: ${POPOVER_WIDTH}px;
    background: ${token.colorBgElevated};
    border: 1px solid ${token.colorBorderSecondary};
    border-radius: ${token.borderRadiusLG}px;
    box-shadow: ${token.boxShadowSecondary};
  `,
  header: css`
    padding: ${token.paddingSM}px ${token.padding}px;
    display: flex;
    justify-content: space-between;
    align-items: center;
    gap: ${token.marginXS}px;
    border-bottom: 1px solid ${token.colorBorderSecondary};
  `,
  title: css`
    font-size: ${token.fontSize}px;
    font-weight: 600;
    min-width: 0;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  `,
  close: css`
    cursor: pointer;
    color: ${token.colorTextTertiary};
    flex-shrink: 0;
    display: inline-flex;
  `,
  body: css`
    padding: ${token.paddingXS}px ${token.padding}px ${token.paddingSM}px;
  `,
  factRow: css`
    display: flex;
    justify-content: space-between;
    gap: ${token.marginSM}px;
    padding: 6px 0;
    border-bottom: 1px solid ${token.colorSplit};
    font-size: ${token.fontSize}px;
  `,
  factLabel: css`
    color: ${token.colorTextTertiary};
  `,
  factValue: css`
    font-variant-numeric: tabular-nums;
    text-align: right;
  `,
  muted: css`
    color: ${token.colorTextTertiary};
  `,
  stateRow: css`
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding: ${token.paddingXS}px 0 2px;
  `,
  note: css`
    margin-block-start: ${token.marginXS}px;
    padding: ${token.paddingXS}px ${token.paddingSM}px;
    background: ${token.colorFillQuaternary};
    border-radius: ${token.borderRadius}px;
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextSecondary};
    line-height: 1.5;
  `,
  footer: css`
    padding: ${token.paddingXS}px ${token.paddingSM}px;
    border-top: 1px solid ${token.colorBorderSecondary};
    display: flex;
    justify-content: space-between;
    gap: ${token.marginXS}px;
  `,
}));
