import { createStyles } from 'antd-style';

export const LABEL_W = 220;
export const BUCKET_W = { week: 88, day: 40 } as const;
export const CELL_H = { week: 42, day: 34 } as const;

export const useStyles = createStyles(({ token, css }) => ({
  toolbar: css`
    display: flex;
    align-items: center;
    gap: ${token.margin}px;
    margin-bottom: ${token.marginSM}px;
    flex-wrap: wrap;
  `,
  navGroup: css`
    display: flex;
    gap: ${token.marginXXS}px;
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
    overflow-x: auto;
  `,
  headerRow: css`
    display: flex;
    position: sticky;
    top: 0;
    z-index: 5;
    background: ${token.colorFillQuaternary};
    border-bottom: 1px solid ${token.colorBorderSecondary};
  `,
  labelHead: css`
    flex-shrink: 0;
    padding: ${token.paddingXS}px ${token.padding}px;
    font-size: ${token.fontSize}px;
    font-weight: 600;
    color: ${token.colorTextSecondary};
    position: sticky;
    left: 0;
    z-index: 6;
    background: ${token.colorFillQuaternary};
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
    background: ${token.colorFillQuaternary};
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
