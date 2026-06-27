import { createStyles } from 'antd-style';

export const useStyles = createStyles(({ token, css }) => ({
  timelineWrap: css`
    margin-block-end: 20px;
  `,
  track: css`
    position: relative;
    height: 56px;
    border-radius: ${token.borderRadius}px;
    overflow: hidden;
    display: flex;
    border: 1px solid ${token.colorBorderSecondary};
  `,
  zone: css`
    display: flex;
    flex-direction: column;
    justify-content: center;
    padding: 0 ${token.paddingSM}px;
    min-width: 0;
  `,
  zoneLabel: css`
    font-size: ${token.fontSizeSM}px;
    font-weight: 600;
  `,
  zoneSub: css`
    font-size: 11px;
    color: ${token.colorTextTertiary};
  `,
  invalidTrack: css`
    display: flex;
    align-items: center;
    justify-content: center;
    width: 100%;
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorError};
  `,
  axis: css`
    position: relative;
    height: 24px;
    margin-block-start: ${token.marginXXS}px;
    font-size: 11px;
    color: ${token.colorTextTertiary};
    font-variant-numeric: tabular-nums;
  `,
  axisStart: css`
    position: absolute;
    left: 0;
  `,
  axisMark: css`
    position: absolute;
    transform: translateX(-50%);
    white-space: nowrap;
  `,
  editors: css`
    display: flex;
    gap: ${token.marginXL}px;
    flex-wrap: wrap;
  `,
  editorLabel: css`
    font-size: ${token.fontSizeSM}px;
    font-weight: 500;
    margin-block-end: ${token.marginXXS}px;
    display: inline-flex;
    align-items: center;
    gap: ${token.marginXXS}px;
  `,
  editorDot: css`
    width: 8px;
    height: 8px;
    border-radius: 2px;
  `,
  editorInputs: css`
    display: flex;
    gap: ${token.marginXS}px;
    align-items: center;
  `,
  valueInput: css`
    width: 90px;
  `,
  unitSelect: css`
    width: 130px;
  `,
  editorHint: css`
    font-size: 11px;
    color: ${token.colorTextTertiary};
    margin-block-start: ${token.marginXXS}px;
    max-width: 220px;
    line-height: 1.4;
  `,
  invalidDetail: css`
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorError};
    margin-block-start: ${token.marginSM}px;
  `,
}));
