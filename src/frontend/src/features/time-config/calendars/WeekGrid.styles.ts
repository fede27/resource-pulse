import { createStyles } from 'antd-style';

// Grid geometry — shared by the layout (createStyles) and the time math in the
// component (column click → minutes, block top/height). Kept here so the grid
// dimensions live with the grid styling, single-sourced.
export const HOUR_START = 6;
export const HOUR_END = 22;
export const HOURS = HOUR_END - HOUR_START;
export const PX_PER_HOUR = 40;
export const AXIS_WIDTH = 48;

const GRID_COLS = `${AXIS_WIDTH}px repeat(7, minmax(0, 1fr))`;

export const useStyles = createStyles(({ token, css }) => ({
  root: css`
    background: ${token.colorBgContainer};
    border: 1px solid ${token.colorBorderSecondary};
    border-radius: ${token.borderRadiusLG}px;
    padding: ${token.padding}px;
    overflow: auto;
  `,
  headerRow: css`
    display: grid;
    grid-template-columns: ${GRID_COLS};
    margin-block-end: ${token.marginXXS}px;
  `,
  dayHead: css`
    text-align: center;
    padding: ${token.paddingXS}px ${token.paddingXXS}px;
    font-size: ${token.fontSizeSM}px;
    font-weight: 500;
    color: ${token.colorText};
  `,
  dayHeadWeekend: css`
    color: ${token.colorTextTertiary};
  `,
  body: css`
    position: relative;
    display: grid;
    grid-template-columns: ${GRID_COLS};
    height: ${HOURS * PX_PER_HOUR}px;
    border: 1px solid ${token.colorBorderSecondary};
    border-radius: ${token.borderRadiusSM}px;
    overflow: hidden;
    background: ${token.colorBgContainer};
  `,
  axis: css`
    position: relative;
    border-right: 1px solid ${token.colorBorderSecondary};
    background: ${token.colorFillQuaternary};
  `,
  hourLabel: css`
    position: absolute;
    right: 6px;
    font-size: 11px;
    color: ${token.colorTextTertiary};
    font-variant-numeric: tabular-nums;
  `,
  dayCol: css`
    position: relative;
    border-right: 1px solid ${token.colorBorderSecondary};
    background: transparent;
    cursor: crosshair;
  `,
  dayColLast: css`
    border-right: none;
  `,
  dayColWeekend: css`
    background: ${token.colorFillQuaternary};
  `,
  dayColReadOnly: css`
    cursor: default;
  `,
  gridLine: css`
    position: absolute;
    left: 0;
    right: 0;
    border-top: 1px solid ${token.colorSplit};
    pointer-events: none;
  `,
  popAnchor: css`
    position: absolute;
    top: 0;
    left: 0;
    width: 1px;
    height: 1px;
  `,
  legend: css`
    margin-block-start: ${token.marginSM}px;
    display: flex;
    gap: ${token.margin}px;
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextTertiary};
    flex-wrap: wrap;
  `,
  legendHint: css`
    margin-inline-start: auto;
  `,
  block: css`
    position: absolute;
    left: 4px;
    right: 4px;
    border-radius: ${token.borderRadiusSM}px;
    padding: ${token.paddingXXS}px 6px;
    cursor: pointer;
    overflow: hidden;
  `,
  blockTime: css`
    font-size: ${token.fontSizeSM}px;
    font-weight: 500;
    font-variant-numeric: tabular-nums;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  `,
  blockMeta: css`
    font-size: 10px;
    margin-block-start: 2px;
    opacity: 0.9;
  `,
  legendSwatchWrap: css`
    display: inline-flex;
    align-items: center;
    gap: ${token.marginXXS}px;
  `,
  legendSwatch: css`
    display: inline-block;
    width: 12px;
    height: 12px;
    border-radius: 2px;
  `,
}));
