import { createStyles } from 'antd-style';

// Shared board geometry — single-sourced here (styles + component logic import
// these; the styles file imports nothing from components, no cycle).
export const LEFT_W = 292;
export const ENVELOPE_H = 66;
export const LANE_H = 48;
export const HEADER_MAJOR_H = 24;
export const HEADER_FENCE_H = 24;
export const HEADER_TICKS_H = 26;

// Decorative-only patterns (not part of the token language).
export const PAST_HATCH = 'repeating-linear-gradient(135deg, rgba(0,0,0,.02) 0 6px, rgba(0,0,0,0) 6px 12px)';
export const PAST_HATCH_STRONG = 'repeating-linear-gradient(135deg, rgba(0,0,0,.04) 0 5px, rgba(0,0,0,0) 5px 10px)';

export const useStyles = createStyles(({ token, css }) => ({
  frame: css`
    background: ${token.colorBgContainer};
    border: 1px solid ${token.colorBorderSecondary};
    border-radius: ${token.borderRadiusLG}px;
    overflow: hidden;
  `,
  scroll: css`
    overflow: auto;
    /* Bounded to the viewport-remaining height (measured by useFrameMaxHeight)
       so rows scroll INSIDE the frame: the x-scrollbar stays reachable and the
       sticky header has a scrollport. Unmeasurable layout → none (document flow). */
    max-height: var(--board-max-h, none);
    scrollbar-gutter: stable;
  `,
  header: css`
    display: flex;
    align-items: stretch;
    border-block-end: 1px solid ${token.colorBorderSecondary};
    background: ${token.colorBgContainer};
    /* Above the sticky-left row labels (z 4) and the headerLeft corner (z 6,
       inside this stacking context). */
    position: sticky;
    top: 0;
    z-index: 8;
  `,
  headerLeft: css`
    width: ${LEFT_W}px;
    flex-shrink: 0;
    position: sticky;
    left: 0;
    z-index: 6;
    background: ${token.colorBgContainer};
    border-inline-end: 1px solid ${token.colorBorderSecondary};
    display: flex;
    flex-direction: column;
  `,
  headerLeftTitle: css`
    height: ${HEADER_MAJOR_H}px;
    display: flex;
    align-items: center;
    padding: 0 ${token.padding}px;
    border-block-end: 1px solid ${token.colorSplit};
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextTertiary};
    font-weight: 600;
    letter-spacing: 0.04em;
    text-transform: uppercase;
    white-space: nowrap;
  `,
  headerLeftFence: css`
    height: ${HEADER_FENCE_H}px;
    border-block-end: 1px solid ${token.colorSplit};
  `,
  headerLeftUnit: css`
    height: ${HEADER_TICKS_H}px;
    display: flex;
    align-items: center;
    justify-content: flex-end;
    padding: 0 ${token.padding}px;
    font-size: 11px;
    color: ${token.colorTextQuaternary};
  `,
  axis: css`
    flex-shrink: 0;
    position: relative;
  `,
  majorRow: css`
    position: relative;
    height: ${HEADER_MAJOR_H}px;
    border-block-end: 1px solid ${token.colorSplit};
  `,
  majorBand: css`
    position: absolute;
    top: 0;
    bottom: 0;
    display: flex;
    align-items: center;
    padding-inline-start: ${token.paddingXS}px;
    overflow: hidden;
    border-inline-start: 1px solid ${token.colorBorderSecondary};

    span {
      font-size: ${token.fontSizeSM}px;
      font-weight: 700;
      color: ${token.colorTextSecondary};
      font-variant-numeric: tabular-nums;
      text-transform: capitalize;
      white-space: nowrap;
    }
  `,
  fenceRow: css`
    position: relative;
    height: ${HEADER_FENCE_H}px;
    border-block-end: 1px solid ${token.colorBorderSecondary};
  `,
  fenceSeg: css`
    position: absolute;
    top: 0;
    bottom: 0;
    display: flex;
    align-items: center;
    justify-content: center;
    overflow: hidden;

    span {
      display: inline-flex;
      align-items: center;
      gap: 3px;
      font-size: 9px;
      font-weight: 700;
      text-transform: uppercase;
      white-space: nowrap;
      padding: 0 2px;
    }
  `,
  ticksRow: css`
    position: relative;
    height: ${HEADER_TICKS_H}px;
  `,
  tick: css`
    position: absolute;
    top: 0;
    bottom: 0;
    display: flex;
    align-items: flex-start;
    justify-content: center;
    padding-block-start: 3px;
    overflow: hidden;
    border-inline-start: 1px solid ${token.colorSplit};

    span {
      font-size: 10px;
      font-variant-numeric: tabular-nums;
      color: ${token.colorTextTertiary};
      text-transform: capitalize;
      white-space: nowrap;
    }
  `,
  tickFaded: css`
    span {
      color: ${token.colorTextQuaternary};
    }
  `,
  tickToday: css`
    span {
      font-weight: 700;
      color: ${token.colorPrimary};
    }
  `,
  todayLine: css`
    position: absolute;
    top: 0;
    bottom: 0;
    width: 2px;
    background: ${token.colorPrimary};
    transform: translateX(-1px);
    z-index: 2;
  `,
  todayPill: css`
    position: absolute;
    bottom: 1px;
    transform: translateX(-50%);
    z-index: 3;

    span {
      display: inline-flex;
      align-items: center;
      height: 15px;
      padding: 0 6px;
      border-radius: 8px;
      background: ${token.colorPrimary};
      color: ${token.colorWhite};
      font-size: 10px;
      font-weight: 700;
      white-space: nowrap;
    }
  `,
  body: css`
    position: relative;
  `,
  backdrop: css`
    position: absolute;
    top: 0;
    bottom: 0;
    z-index: 0;
    pointer-events: none;
    overflow: hidden;
  `,
  rows: css`
    position: relative;
    z-index: 1;
  `,
  emptyWrap: css`
    position: sticky;
    left: 0;
    width: 100%;
    padding: ${token.paddingXL}px 0;
  `,
  gridline: css`
    position: absolute;
    top: 0;
    bottom: 0;
    width: 1px;
  `,
}));
