import { createStyles } from 'antd-style';
import { gold } from '@/app/palette';

export const useStyles = createStyles(({ token, css }) => ({
  header: css`
    margin-block-end: ${token.margin}px;

    h3 {
      margin: 0;
      font-size: 17px;
      font-weight: 600;
    }

    > div {
      font-size: ${token.fontSize}px;
      color: ${token.colorTextTertiary};
    }
  `,
  personHeader: css`
    display: flex;
    align-items: center;
    gap: ${token.marginSM}px;
    margin-block-end: ${token.margin}px;
  `,
  sectionTitle: css`
    font-size: 11px;
    font-weight: 600;
    letter-spacing: 0.06em;
    text-transform: uppercase;
    color: ${token.colorTextTertiary};
    margin-block: ${token.marginMD}px ${token.marginXXS}px;

    &:first-of-type {
      margin-block-start: 0;
    }
  `,
  miniRow: css`
    display: flex;
    gap: ${token.marginXS}px;
    margin-block-end: ${token.margin}px;
  `,
  miniCard: css`
    flex: 1;
    padding: ${token.paddingXS}px 10px;
    background: ${token.colorFillQuaternary};
    border: 1px solid ${token.colorBorderSecondary};
    border-radius: ${token.borderRadiusLG}px;

    > div:first-child {
      font-size: 11px;
      color: ${token.colorTextTertiary};
    }

    > div:last-child {
      font-size: 18px;
      font-weight: 700;
      font-variant-numeric: tabular-nums;
      margin-block-start: 2px;
    }
  `,
  overWarning: css`
    display: flex;
    align-items: center;
    gap: ${token.marginXS}px;
    margin-block-end: ${token.margin}px;
    padding: ${token.paddingXS}px ${token.paddingSM}px;
    background: ${token.colorWarningBg};
    border: 1px solid ${token.colorWarningBorder};
    border-radius: ${token.borderRadiusLG}px;
    font-size: ${token.fontSizeSM}px;
    color: ${gold[8]};
    line-height: 1.5;
  `,
  demandRow: css`
    padding: ${token.paddingSM}px 0;
    border-block-end: 1px solid ${token.colorBorderSecondary};
  `,
  demandHead: css`
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: ${token.marginXS}px;
    margin-block-end: 6px;

    > span:first-child {
      font-size: ${token.fontSize}px;
      font-weight: 600;
    }
  `,
  demandBadges: css`
    display: inline-flex;
    align-items: center;
    gap: 6px;
  `,
  statusChip: css`
    display: inline-flex;
    align-items: center;
    gap: 5px;
    height: 20px;
    padding: 0 ${token.paddingXS}px;
    border-radius: 10px;
    font-size: 11px;
    font-weight: 600;
    white-space: nowrap;
  `,
  numbersLine: css`
    font-size: ${token.fontSize}px;
    color: ${token.colorTextSecondary};
    font-variant-numeric: tabular-nums;
    margin-block-end: 6px;

    strong {
      color: ${token.colorText};
    }
  `,
  hoursBar: css`
    position: relative;
    height: 8px;
    border-radius: 4px;
    overflow: hidden;
    border: 1px solid rgba(0, 0, 0, 0.05);
  `,
  coverageEntries: css`
    margin-block-start: ${token.marginXS}px;
    display: flex;
    flex-direction: column;
    gap: 6px;
  `,
  coverageEntry: css`
    display: flex;
    align-items: center;
    gap: ${token.marginXS}px;
    font-size: ${token.fontSize}px;

    > span:nth-child(2) {
      flex: 1;
      min-width: 0;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }
  `,
  entryNumbers: css`
    font-variant-numeric: tabular-nums;
    font-weight: 600;
    min-width: 96px;
    text-align: right;
    white-space: nowrap;
  `,
  blockTag: css`
    font-size: 10px;
    font-weight: 600;
    border-radius: 3px;
    padding: 0 5px;
    line-height: 15px;
    white-space: nowrap;
  `,
  mismatchTag: css`
    display: inline-flex;
    align-items: center;
    gap: 3px;
    height: 16px;
    padding: 0 6px;
    border-radius: 4px;
    font-size: 10px;
    font-weight: 600;
    white-space: nowrap;
  `,
  ghostDot: css`
    width: 20px;
    height: 20px;
    border-radius: 50%;
    flex-shrink: 0;
    display: inline-flex;
    align-items: center;
    justify-content: center;
    background: ${token.colorBgContainer};
  `,
  ruleBox: css`
    margin-block-start: ${token.margin}px;
    padding: ${token.paddingSM}px;
    background: ${token.colorFillQuaternary};
    border: 1px solid ${token.colorBorderSecondary};
    border-radius: ${token.borderRadiusLG}px;
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextSecondary};
    line-height: 1.6;
  `,
  hint: css`
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextTertiary};
    line-height: 1.5;
    margin-block-end: 10px;
  `,
  segment: css`
    padding: 6px ${token.paddingXS}px;
    margin: 0 -${token.paddingXS}px;
    border-radius: ${token.borderRadius}px;
  `,
  segmentHead: css`
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: ${token.margin}px;
    font-size: ${token.fontSizeSM}px;
  `,
  segmentWhen: css`
    display: inline-flex;
    align-items: center;
    gap: 7px;
    color: ${token.colorTextSecondary};
    font-variant-numeric: tabular-nums;
  `,
  segmentDot: css`
    width: 7px;
    height: 7px;
    border-radius: 50%;
    flex-shrink: 0;
  `,
  nowPill: css`
    margin-inline-start: 2px;
    height: 15px;
    padding: 0 5px;
    border-radius: 7px;
    background: ${token.colorPrimary};
    color: ${token.colorWhite};
    font-size: 9px;
    font-weight: 700;
    display: inline-flex;
    align-items: center;
  `,
  segmentPct: css`
    font-variant-numeric: tabular-nums;
  `,
  segmentTrack: css`
    margin-block-start: 4px;
    height: 5px;
    background: rgba(0, 0, 0, 0.05);
    border-radius: 3px;
    overflow: hidden;
  `,
  compRow: css`
    display: flex;
    justify-content: space-between;
    gap: ${token.margin}px;
    padding: 7px 0;
    border-block-end: 1px solid ${token.colorSplit};
    font-size: ${token.fontSize}px;

    > span:last-child {
      font-variant-numeric: tabular-nums;
      font-weight: 600;
    }
  `,
  compTotal: css`
    display: flex;
    justify-content: space-between;
    gap: ${token.margin}px;
    padding: 9px 0;
    font-size: ${token.fontSize}px;

    > span:first-child {
      font-weight: 600;
    }

    > span:last-child {
      font-variant-numeric: tabular-nums;
      font-weight: 700;
    }
  `,
  tentativeNote: css`
    margin-block-start: 10px;
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextTertiary};
  `,
  statusRow: css`
    display: flex;
    align-items: center;
    gap: 10px;
    padding: ${token.paddingXS}px 0;
    flex-wrap: wrap;
  `,
  bigPill: css`
    display: inline-flex;
    align-items: center;
    gap: 6px;
    height: 26px;
    padding: 0 ${token.paddingSM}px;
    border-radius: 13px;
    font-size: ${token.fontSize}px;
    font-weight: 600;
  `,
  pillDot: css`
    width: 8px;
    height: 8px;
    border-radius: 50%;
  `,
  todayNote: css`
    font-size: ${token.fontSize}px;
    color: ${token.colorTextTertiary};

    strong {
      font-variant-numeric: tabular-nums;
    }
  `,
  personRow: css`
    display: flex;
    align-items: center;
    gap: 10px;
    padding: 9px 0;
    border-block-end: 1px solid ${token.colorSplit};

    > span:nth-child(2) {
      flex: 1;
      min-width: 0;
      font-size: ${token.fontSize}px;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }
  `,
  peakPill: css`
    display: inline-flex;
    align-items: center;
    gap: 5px;
    height: 20px;
    padding: 0 ${token.paddingXS}px;
    border-radius: 10px;
    font-size: ${token.fontSizeSM}px;
    font-weight: 600;
    font-variant-numeric: tabular-nums;
  `,
  factRow: css`
    display: flex;
    justify-content: space-between;
    gap: ${token.margin}px;
    padding: 7px 0;
    border-block-end: 1px solid ${token.colorSplit};
    font-size: ${token.fontSize}px;

    > span:first-child {
      color: ${token.colorTextTertiary};
    }

    > span:last-child {
      text-align: right;
    }
  `,
  factMono: css`
    font-variant-numeric: tabular-nums;
    font-weight: 600;
  `,
  emptyNote: css`
    font-size: ${token.fontSize}px;
    color: ${token.colorTextTertiary};
    padding: ${token.paddingXS}px 0;
  `,
}));
