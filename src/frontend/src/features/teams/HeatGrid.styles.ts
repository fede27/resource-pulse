import { createStyles } from 'antd-style';

// Row heights for the heatmap lanes.
export const TEAM_H = 50;
export const PERSON_H = 38;
export const ADD_H = 38;

// Decorative loading/hatch patterns — pure shimmer ornament, intentionally not
// part of the token design language.
export const LOADING_BG =
  'repeating-linear-gradient(45deg, #f0f0f0, #f0f0f0 4px, #f7f7f7 4px, #f7f7f7 8px)';
export const ADD_ROW_HATCH =
  'repeating-linear-gradient(45deg, #fafafa, #fafafa 6px, #f3f3f3 6px, #f3f3f3 12px)';
// Decorative inset bevel highlight (white at low alpha) — not a themeable colour.
export const BEVEL = 'inset -1px -1px 0 rgba(255,255,255,.5)';

export const useStyles = createStyles(({ token, css }) => ({
  cell: css`
    display: flex;
    align-items: center;
    justify-content: center;
    font-variant-numeric: tabular-nums;
    letter-spacing: -0.02em;
  `,
  reducedDot: css`
    position: absolute;
    top: 3px;
    right: 3px;
    width: 3px;
    height: 3px;
    border-radius: 50%;
    background: ${token.colorTextTertiary};
  `,
  cornerTop: css`
    padding: 0 14px;
    font-size: 11px;
    color: ${token.colorTextTertiary};
    letter-spacing: 0.05em;
    text-transform: uppercase;
  `,
  cornerBottom: css`
    width: 100%;
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 0 14px;
  `,
  cornerBottomTitle: css`
    font-size: ${token.fontSizeSM}px;
    font-weight: 600;
    color: ${token.colorTextSecondary};
  `,
  cornerBottomGrain: css`
    font-size: 11px;
    color: ${token.colorTextTertiary};
  `,
  bucketHead: css`
    height: 100%;
    background: ${token.colorFillQuaternary};
    border-bottom: 1px solid ${token.colorBorder};
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: space-between;
    padding-block-start: 5px;
  `,
  bucketLabel: css`
    font-size: 9.5px;
    font-variant-numeric: tabular-nums;
    white-space: nowrap;
  `,
  todayTag: css`
    font-size: 8px;
    color: ${token.colorPrimary};
    font-weight: 600;
  `,
  heatBar: css`
    width: 100%;
    height: 8px;
    box-shadow: inset 0 1px 0 rgba(255, 255, 255, 0.4);
  `,
  expandBtn: css`
    border: 0;
    background: transparent;
    cursor: pointer;
    padding: 2px;
    color: ${token.colorTextTertiary};
    display: inline-flex;
    transition: transform 0.18s;
    flex-shrink: 0;
    transform: none;
  `,
  expandBtnOpen: css`
    transform: rotate(90deg);
  `,
  expandIcon: css`
    font-size: 11px;
  `,
  teamNameWrap: css`
    flex: 1;
    min-width: 0;
    margin-inline-start: ${token.marginXS}px;
  `,
  teamName: css`
    font-size: ${token.fontSize}px;
    font-weight: 600;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
    line-height: 1.2;
    display: flex;
    align-items: center;
    gap: 6px;
  `,
  inactiveTag: css`
    font-size: 10px;
    line-height: 16px;
    margin-inline-end: 0;
  `,
  teamMeta: css`
    font-size: 11.5px;
    color: ${token.colorTextTertiary};
    margin-block-start: 1px;
  `,
  teamActions: css`
    display: flex;
    align-items: center;
    gap: 2px;
    flex-shrink: 0;
  `,
  nowBadge: css`
    flex-shrink: 0;
    min-width: 42px;
    height: 22px;
    margin-inline-start: 6px;
    padding: 0 ${token.paddingXS}px;
    border-radius: ${token.borderRadius}px;
    font-size: ${token.fontSizeSM}px;
    font-weight: 600;
    display: inline-flex;
    align-items: center;
    justify-content: center;
    font-variant-numeric: tabular-nums;
  `,
  personNameWrap: css`
    flex: 1;
    min-width: 0;
    margin-inline-start: 9px;
  `,
  personName: css`
    font-size: ${token.fontSizeSM}px;
    font-weight: 500;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
    line-height: 1.15;
  `,
  personRole: css`
    font-size: 11px;
    color: ${token.colorTextTertiary};
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  `,
  deleteIcon: css`
    font-size: ${token.fontSizeSM}px;
  `,
  addLabel: css`
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextTertiary};
    margin-inline-start: ${token.marginXXS}px;
  `,
}));
