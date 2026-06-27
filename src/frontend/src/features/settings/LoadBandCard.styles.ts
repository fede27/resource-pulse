import { createStyles } from 'antd-style';

// Shared grid template for the header + editable rows.
const ROW_COLS = '24px 1fr 170px 32px';

export const useStyles = createStyles(({ token, css }) => ({
  barWrap: css`
    margin-block-end: 20px;
  `,
  barTrack: css`
    position: relative;
    height: 44px;
    border-radius: ${token.borderRadius}px;
    overflow: hidden;
    display: flex;
    border: 1px solid ${token.colorBorderSecondary};
  `,
  bandSegment: css`
    display: flex;
    flex-direction: column;
    justify-content: center;
    padding: 0 ${token.paddingXS}px;
    min-width: 0;
  `,
  bandLabel: css`
    font-size: ${token.fontSizeSM}px;
    font-weight: 500;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  `,
  bandRange: css`
    font-size: 11px;
    color: ${token.colorTextTertiary};
    font-variant-numeric: tabular-nums;
  `,
  probe: css`
    position: absolute;
    top: 0;
    bottom: 0;
    width: 2px;
    background: ${token.colorText};
    transition: left ${token.motionDurationMid};
  `,
  probeDot: css`
    position: absolute;
    top: -1px;
    left: 50%;
    transform: translateX(-50%);
    width: 8px;
    height: 8px;
    border-radius: 50%;
    background: ${token.colorText};
    margin-block-start: -4px;
  `,
  rows: css`
    display: flex;
    flex-direction: column;
    gap: ${token.marginXS}px;
  `,
  gridHeader: css`
    display: grid;
    grid-template-columns: ${ROW_COLS};
    gap: ${token.marginSM}px;
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextTertiary};
    padding: 0 ${token.paddingXXS}px;
  `,
  gridRow: css`
    display: grid;
    grid-template-columns: ${ROW_COLS};
    gap: ${token.marginSM}px;
    align-items: center;
  `,
  swatch: css`
    width: 12px;
    height: 12px;
    border-radius: 3px;
    justify-self: center;
  `,
  boundCell: css`
    display: inline-flex;
    align-items: center;
    gap: ${token.marginXS}px;
  `,
  firstFixed: css`
    display: inline-flex;
    align-items: center;
    gap: ${token.marginXXS}px;
    height: 32px;
    padding: 0 11px;
    border-radius: ${token.borderRadius}px;
    background: ${token.colorFillQuaternary};
    border: 1px solid ${token.colorBorderSecondary};
    font-size: ${token.fontSize}px;
    color: ${token.colorTextTertiary};
    font-variant-numeric: tabular-nums;
    width: 96px;
    box-sizing: border-box;
  `,
  firstFixedLock: css`
    margin-inline-start: auto;
    font-size: 11px;
  `,
  beyond: css`
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextTertiary};
  `,
  boundInput: css`
    width: 120px;
  `,
  removeCell: css`
    justify-self: center;
  `,
  rowError: css`
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorError};
    padding-inline-start: 36px;
  `,
  addBtn: css`
    margin-block-start: ${token.marginXXS}px;
  `,
  probeBox: css`
    margin-block-start: 18px;
    padding: ${token.paddingSM}px;
    background: ${token.colorInfoBg};
    border: 1px solid ${token.colorInfoBorder};
    border-radius: ${token.borderRadius}px;
    display: flex;
    align-items: center;
    gap: ${token.marginSM}px;
    flex-wrap: wrap;
  `,
  probeText: css`
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorText};
  `,
  probeInput: css`
    width: 110px;
  `,
  probeChip: css`
    display: inline-flex;
    align-items: center;
    height: 24px;
    padding: 0 ${token.paddingSM}px;
    border-radius: 12px;
    font-size: ${token.fontSizeSM}px;
    font-weight: 500;
  `,
  probeInvalid: css`
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorError};
  `,
}));
