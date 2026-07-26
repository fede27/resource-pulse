import { createStyles } from 'antd-style';

import type { Grain } from '@/components/timeline';

export const LABEL_W = 220;
export const BUCKET_W: Record<Grain, number> = { day: 40, week: 88, month: 96 };
export const CELL_H: Record<Grain, number> = { day: 34, week: 42, month: 42 };

// Row heights DERIVED FROM STATE (px, border-box incl. the 1px bottom border) —
// the single source for both the rendered height and the windowing math
// (`useWindowedRows` needs exact heights or the gap spacers drift). The person
// row is avatar-driven (32px avatar + 2×paddingXS) so it is constant across
// grains; the team row tracks the aggregate-cell height (CELL_H + 6).
export const PERSON_ROW_H = 49;
export const teamRowHeight = (grain: Grain) => CELL_H[grain] + 7;

export const useStyles = createStyles(({ token, css }) => ({
  toolbar: css`
    display: flex;
    align-items: center;
    gap: ${token.margin}px;
    margin-bottom: ${token.marginSM}px;
    flex-wrap: wrap;
  `,
  // Horizontal windowing spacer: holds the width of the off-screen columns so
  // the scroll extent and the sticky header stay aligned with the rendered slice.
  colGap: css`
    flex-shrink: 0;
  `,
  span: css`
    font-size: ${token.fontSize}px;
    color: ${token.colorTextTertiary};
    font-variant-numeric: tabular-nums;
  `,
  frame: css`
    background: ${token.colorBgContainer};
    border: 1px solid ${token.colorBorderSecondary};
    border-radius: ${token.borderRadiusLG}px;
    overflow: hidden;
  `,
  scroll: css`
    overflow: auto;
    /* Bounded to the viewport-remaining height (measured by useFrameMaxHeight)
       so rows scroll INSIDE the frame: the sticky header/labels keep a
       scrollport and the vertical windowing has a bounded viewport to track.
       Unmeasurable layout (jsdom) → none → document flow, full render. */
    max-height: var(--board-max-h, none);
    scrollbar-gutter: stable;
  `,
  headerRow: css`
    display: flex;
    position: sticky;
    top: 0;
    z-index: 5;
    /* Same chrome as BoardTimeline's header: container-white, and OPAQUE (not
       the translucent colorFillQuaternary) so the sticky header hides the rows
       scrolling underneath it. */
    background: ${token.colorBgContainer};
    border-bottom: 1px solid ${token.colorBorderSecondary};
  `,
  labelHead: css`
    flex-shrink: 0;
    display: flex;
    align-items: center;
    padding: ${token.paddingXS}px ${token.padding}px;
    /* Mirrors BoardTimeline's headerLeftTitle: the label column's header reads
       as an axis caption on every timed view, not as a table heading. */
    font-size: ${token.fontSizeSM}px;
    font-weight: 600;
    letter-spacing: 0.04em;
    text-transform: uppercase;
    color: ${token.colorTextTertiary};
    position: sticky;
    left: 0;
    z-index: 6;
    /* Opaque sticky corner (top + left) — see headerRow. */
    background: ${token.colorBgContainer};
    border-right: 1px solid ${token.colorBorderSecondary};
  `,
  headCell: css`
    flex-shrink: 0;
    padding: ${token.paddingXXS}px 2px;
    text-align: center;
    border-right: 1px solid ${token.colorFillQuaternary};
    position: relative;
  `,
  headCellToday: css`
    background: ${token.colorPrimaryBg};
  `,
  headDate: css`
    font-size: ${token.fontSizeSM}px;
    font-weight: 600;
    color: ${token.colorTextSecondary};
    font-variant-numeric: tabular-nums;
  `,
  headDateToday: css`
    color: ${token.colorPrimary};
  `,
  headWeekTag: css`
    font-size: 10px;
    color: ${token.colorTextQuaternary};
  `,
  headDow: css`
    font-size: 11px;
    color: ${token.colorTextSecondary};
  `,
  headDowWeekend: css`
    color: ${token.colorTextQuaternary};
  `,
  closureMark: css`
    position: absolute;
    bottom: 0;
    left: 0;
    right: 0;
    height: 3px;
    background: ${token.colorTextQuaternary};
  `,
  teamRow: css`
    display: flex;
    box-sizing: border-box;
    border-bottom: 1px solid ${token.colorBorderSecondary};
    background: ${token.colorFillQuaternary};
  `,
  teamLabel: css`
    flex-shrink: 0;
    padding: ${token.paddingXS}px ${token.padding}px;
    display: flex;
    align-items: center;
    gap: ${token.marginXS}px;
    position: sticky;
    left: 0;
    z-index: 4;
    /* Keeps the team row's fill tone while staying opaque, the same idiom as the
       boards' groupHeaderLabel: stack the translucent fill over the container
       colour instead of substituting a different grey. */
    background: linear-gradient(${token.colorFillQuaternary}, ${token.colorFillQuaternary}),
      ${token.colorBgContainer};
    border-right: 1px solid ${token.colorBorderSecondary};
    cursor: pointer;
  `,
  teamChevron: css`
    color: ${token.colorTextTertiary};
    display: inline-flex;
    transition: transform 0.2s;
  `,
  teamChevronOpen: css`
    transform: rotate(90deg);
  `,
  teamName: css`
    font-size: ${token.fontSize}px;
    font-weight: 600;
  `,
  teamCount: css`
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextQuaternary};
    font-variant-numeric: tabular-nums;
  `,
  teamAggCell: css`
    flex-shrink: 0;
    display: flex;
    align-items: center;
    justify-content: center;
    border-right: 1px solid ${token.colorFillQuaternary};
    background: ${token.colorFillQuaternary};
    font-size: 11px;
    font-weight: 600;
    color: ${token.colorTextQuaternary};
    font-variant-numeric: tabular-nums;
  `,
  personRow: css`
    display: flex;
    box-sizing: border-box;
    border-bottom: 1px solid ${token.colorFillQuaternary};
  `,
  personLabel: css`
    flex-shrink: 0;
    padding: ${token.paddingXS}px ${token.padding}px ${token.paddingXS}px 34px;
    display: flex;
    align-items: center;
    gap: ${token.marginSM}px;
    position: sticky;
    left: 0;
    z-index: 4;
    background: ${token.colorBgContainer};
    border-right: 1px solid ${token.colorBorderSecondary};
  `,
  personMeta: css`
    min-width: 0;
  `,
  personName: css`
    font-size: ${token.fontSizeSM}px;
    font-weight: 500;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  `,
  personCal: css`
    font-size: 11px;
    color: ${token.colorPrimaryText};
    cursor: pointer;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
    &:hover {
      text-decoration: underline;
    }
  `,
  cell: css`
    flex-shrink: 0;
    padding: 3px;
    border-right: 1px solid ${token.colorFillQuaternary};
    cursor: pointer;
  `,
  chip: css`
    height: 100%;
    border-radius: ${token.borderRadiusSM}px;
    display: flex;
    align-items: center;
    justify-content: center;
    position: relative;
    overflow: hidden;
  `,
  chipHours: css`
    font-weight: 600;
    font-variant-numeric: tabular-nums;
  `,
  markers: css`
    position: absolute;
    top: 2px;
    right: 3px;
    display: flex;
    gap: 2px;
  `,
  marker: css`
    width: 5px;
    height: 5px;
    border-radius: 50%;
  `,
  footer: css`
    margin-top: ${token.marginSM}px;
    display: flex;
    justify-content: space-between;
    align-items: center;
    flex-wrap: wrap;
    gap: ${token.marginSM}px;
  `,
  legend: css`
    display: flex;
    gap: ${token.margin}px;
    flex-wrap: wrap;
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextSecondary};
  `,
  legendItem: css`
    display: inline-flex;
    align-items: center;
    gap: ${token.marginXXS}px;
  `,
  legendSwatch: css`
    width: 12px;
    height: 12px;
    border-radius: ${token.borderRadiusSM}px;
  `,
  hint: css`
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextQuaternary};
  `,
}));
