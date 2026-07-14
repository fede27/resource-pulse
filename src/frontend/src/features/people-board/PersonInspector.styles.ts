import { createStyles } from 'antd-style';

export const useStyles = createStyles(({ token, css }) => ({
  personHeader: css`
    display: flex;
    align-items: center;
    gap: ${token.marginSM}px;
    margin-block-end: ${token.margin}px;

    h3 {
      margin: 0;
      font-size: 16px;
      font-weight: 600;
    }
  `,
  personSub: css`
    font-size: 13px;
    color: ${token.colorTextTertiary};
  `,
  sectionTitle: css`
    font-size: 11px;
    font-weight: 600;
    letter-spacing: 0.06em;
    text-transform: uppercase;
    color: ${token.colorTextTertiary};
    margin-block: ${token.margin}px ${token.marginXS}px;

    &:first-of-type {
      margin-block-start: 0;
    }
  `,
  periodRow: css`
    margin-block-end: ${token.marginXS}px;
  `,
  customRow: css`
    margin-block-end: ${token.marginXS}px;

    .ant-picker {
      width: 100%;
    }
  `,
  bandBox: css`
    display: flex;
    align-items: center;
    gap: ${token.marginSM}px;
    padding: 12px ${token.paddingSM}px;
    border-radius: ${token.borderRadiusLG}px;
    margin-block-end: ${token.marginXXS}px;
  `,
  bandValue: css`
    font-size: 30px;
    font-weight: 700;
    line-height: 1;
    font-variant-numeric: tabular-nums;
  `,
  bandMeta: css`
    font-size: ${token.fontSizeSM}px;
    min-width: 0;

    > div:first-child {
      font-weight: 600;
      text-transform: capitalize;
    }

    > div:last-child {
      opacity: 0.85;
      font-variant-numeric: tabular-nums;
    }
  `,
  bandNote: css`
    margin-inline-start: auto;
    font-size: 11px;
    opacity: 0.7;
    white-space: nowrap;
  `,
  avgHint: css`
    font-size: 11px;
    color: ${token.colorTextQuaternary};
    margin-block-end: ${token.marginXXS}px;
  `,
  compRow: css`
    display: flex;
    align-items: center;
    gap: ${token.marginXS}px;
    padding: 6px 0;
    border-block-end: 1px solid ${token.colorSplit};
    font-size: 13px;
  `,
  compDot: css`
    width: 8px;
    height: 8px;
    border-radius: 2px;
    flex-shrink: 0;
  `,
  compName: css`
    flex: 1;
    min-width: 0;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;

    em {
      color: ${token.colorTextQuaternary};
      font-size: ${token.fontSizeSM}px;
    }
  `,
  compValue: css`
    font-variant-numeric: tabular-nums;
    font-weight: 600;
  `,
  totalRow: css`
    display: flex;
    justify-content: space-between;
    padding: ${token.paddingXS}px 0;
    font-size: ${token.fontSize}px;
    font-weight: 600;

    span:last-child {
      font-variant-numeric: tabular-nums;
      font-weight: 700;
    }
  `,
  subRow: css`
    display: flex;
    align-items: center;
    gap: ${token.marginXS}px;
    font-size: ${token.fontSizeSM}px;
    padding: 2px 0;
  `,
  subLabel: css`
    width: 48px;
    flex-shrink: 0;
    color: ${token.colorTextTertiary};
    font-variant-numeric: tabular-nums;
  `,
  subTrack: css`
    flex: 1;
    height: 12px;
    background: ${token.colorFillQuaternary};
    border-radius: 3px;
    overflow: hidden;
    position: relative;
  `,
  subFill: css`
    position: absolute;
    inset-block: 0;
    inset-inline-start: 0;
    opacity: 0.55;
  `,
  subValue: css`
    width: 40px;
    flex-shrink: 0;
    text-align: end;
    font-variant-numeric: tabular-nums;
    font-weight: 600;
  `,
  hoursRow: css`
    margin-block-end: ${token.marginXS}px;
  `,
  hoursHead: css`
    display: flex;
    align-items: center;
    gap: ${token.marginXS}px;
    font-size: 13px;
    margin-block-end: 4px;

    em {
      color: ${token.colorTextQuaternary};
      font-size: ${token.fontSizeSM}px;
    }
  `,
  hoursName: css`
    flex: 1;
    min-width: 0;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  `,
  hoursValue: css`
    font-variant-numeric: tabular-nums;
    font-weight: 600;
  `,
  hoursTrack: css`
    height: 6px;
    background: ${token.colorFillQuaternary};
    border-radius: 3px;
    overflow: hidden;
  `,
  hoursFill: css`
    height: 100%;
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
  emptyNote: css`
    font-size: 13px;
    color: ${token.colorTextTertiary};
  `,
  tentativeNote: css`
    font-size: 13px;
    color: ${token.colorTextTertiary};
    margin-block-start: ${token.marginXS}px;
  `,
  coverageSubtitle: css`
    font-size: 13px;
    color: ${token.colorTextTertiary};
    margin-block-end: 10px;
    line-height: 1.5;
  `,
  blockCard: css`
    padding: ${token.paddingXS}px ${token.paddingSM}px;
    border: 1px solid ${token.colorBorderSecondary};
    border-radius: ${token.borderRadiusLG}px;
    margin-block-end: ${token.marginXS}px;
    font-size: 13px;

    > div {
      display: flex;
      justify-content: space-between;
      gap: ${token.marginXS}px;
      padding: 2px 0;
    }

    span:first-child {
      color: ${token.colorTextTertiary};
    }

    span:last-child {
      font-variant-numeric: tabular-nums;
    }
  `,
}));
