import { createStyles } from 'antd-style';
import { LEFT_W } from '@/components/board/BoardTimeline.styles';

// Row geometry shared between styles and component logic — single-sourced here.
export const ROW_H = 52;
export const PLANE_H = 30;

// Decorative-only patterns (not part of the token language).
export const TENT_HATCH_ALPHA = 0.12;
// Discreet diagonal hatch for the "fuori calendario" cell (active blocks on a
// zero-capacity bucket): neutral by design — never a band colour.
export const OFF_CALENDAR_HATCH =
  'repeating-linear-gradient(135deg, rgba(0,0,0,0.07) 0 3px, transparent 3px 8px)';

export const useStyles = createStyles(({ token, css }) => ({
  block: css`
    border-block-end: 1px solid ${token.colorBorderSecondary};
  `,
  row: css`
    display: flex;
    align-items: stretch;
    height: ${ROW_H}px;
  `,
  rowAlt: css`
    background: rgba(0, 0, 0, 0.008);
  `,
  labelCell: css`
    width: ${LEFT_W}px;
    flex-shrink: 0;
    position: sticky;
    left: 0;
    z-index: 4;
    background: ${token.colorBgContainer};
    border-inline-end: 1px solid ${token.colorBorderSecondary};
    display: flex;
    align-items: center;
    gap: 6px;
    padding: 0 ${token.paddingSM}px 0 4px;
  `,
  chevron: css`
    cursor: pointer;
    color: ${token.colorTextTertiary};
    padding: 4px;
    display: inline-flex;
    flex-shrink: 0;
    transition: transform 0.2s;
  `,
  chevronOpen: css`
    transform: rotate(90deg);
  `,
  labelMain: css`
    flex: 1;
    min-width: 0;
    cursor: pointer;
  `,
  personName: css`
    font-size: 13px;
    font-weight: 500;
    line-height: 1.2;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  `,
  personSub: css`
    font-size: 11px;
    color: ${token.colorTextTertiary};
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  `,
  peakPill: css`
    flex-shrink: 0;
    display: inline-flex;
    align-items: center;
    gap: 4px;
    height: 20px;
    padding: 0 ${token.paddingXS}px;
    border-radius: 10px;
    font-size: 11px;
    font-weight: 600;
    font-variant-numeric: tabular-nums;
    white-space: nowrap;
  `,
  axisCell: css`
    flex-shrink: 0;
    position: relative;
  `,
  heatCell: css`
    position: absolute;
    top: 6px;
    bottom: 6px;
    border-radius: 4px;
    cursor: pointer;
    display: flex;
    align-items: center;
    justify-content: center;
    overflow: hidden;
    transition: box-shadow 0.15s;

    &:hover {
      box-shadow: ${token.boxShadowTertiary};
    }

    span {
      font-size: 11px;
      font-weight: 500;
      font-variant-numeric: tabular-nums;
    }
  `,
  lanes: css`
    background: rgba(0, 0, 0, 0.012);
    border-block-start: 1px solid ${token.colorSplit};
  `,
  lane: css`
    display: flex;
    align-items: stretch;
    height: ${PLANE_H}px;
    /* Border inside the height: the derived row height (personRowHeight in
       peopleBoardModel.ts) counts exactly PLANE_H per lane. */
    box-sizing: border-box;
    border-block-end: 1px solid ${token.colorSplit};

    &:last-child {
      border-block-end: none;
    }
  `,
  laneLabel: css`
    width: ${LEFT_W}px;
    flex-shrink: 0;
    position: sticky;
    left: 0;
    z-index: 4;
    /* Opaque: the sticky label must fully cover bars scrolling underneath. */
    background: linear-gradient(${token.colorFillQuaternary}, ${token.colorFillQuaternary}),
      ${token.colorBgContainer};
    border-inline-end: 1px solid ${token.colorBorderSecondary};
    display: flex;
    align-items: center;
    gap: 7px;
    padding: 0 ${token.paddingSM}px 0 40px;
  `,
  laneDot: css`
    width: 8px;
    height: 8px;
    border-radius: 2px;
    flex-shrink: 0;
  `,
  laneName: css`
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextSecondary};
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  `,
  laneGhostDot: css`
    width: 8px;
    height: 8px;
    border-radius: 50%;
    border: 1.5px dashed ${token.colorTextQuaternary};
    flex-shrink: 0;
  `,
  laneFreeName: css`
    font-size: 11px;
    color: ${token.colorTextQuaternary};
    white-space: nowrap;
  `,
  bar: css`
    position: absolute;
    top: 4px;
    bottom: 4px;
    border-radius: 4px;
    cursor: pointer;
    overflow: hidden;
    display: flex;
    align-items: center;
    gap: 5px;
    padding: 0 ${token.paddingXS}px;
    transition: box-shadow 0.15s;

    &:hover {
      box-shadow: ${token.boxShadowTertiary};
    }

    span {
      font-size: 11px;
      font-weight: 600;
      font-variant-numeric: tabular-nums;
      white-space: nowrap;
    }
  `,
  freeLane: css`
    position: relative;
    height: 100%;
    flex: 1;
    cursor: crosshair;
  `,
  freeHint: css`
    position: absolute;
    left: ${token.paddingXS}px;
    top: 0;
    bottom: 0;
    display: flex;
    align-items: center;
    font-size: 11px;
    color: ${token.colorTextQuaternary};
    pointer-events: none;
    white-space: nowrap;
  `,
  dragGhost: css`
    position: absolute;
    top: 4px;
    bottom: 4px;
    border-radius: 4px;
    background: ${token.colorFillSecondary};
    border: 1px dashed ${token.colorTextTertiary};
    display: flex;
    align-items: center;
    justify-content: center;
    pointer-events: none;

    span {
      font-size: 10px;
      font-weight: 700;
      color: ${token.colorTextSecondary};
      white-space: nowrap;
      font-variant-numeric: tabular-nums;
    }
  `,
  popoverAnchor: css`
    position: absolute;
    top: 100%;
    width: 0;
    height: 0;
  `,
}));
