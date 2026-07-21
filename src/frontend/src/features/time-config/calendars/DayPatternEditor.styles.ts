import { createStyles } from 'antd-style';

export const useStyles = createStyles(({ token, css }) => ({
  headerSub: css`
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextTertiary};
  `,
  row: css`
    display: flex;
    align-items: center;
    gap: ${token.marginSM}px;
    padding: ${token.paddingXS}px ${token.padding}px;
    border-bottom: 1px solid ${token.colorSplit};

    &:last-of-type {
      border-bottom: none;
    }
  `,
  rowEmptyWeekend: css`
    background: ${token.colorFillQuaternary};
  `,
  dayLabel: css`
    width: 78px;
    flex-shrink: 0;
    font-size: ${token.fontSize}px;
    font-weight: 500;
    color: ${token.colorText};
  `,
  dayLabelEmpty: css`
    color: ${token.colorTextQuaternary};
  `,
  chips: css`
    flex: 1;
    display: flex;
    align-items: center;
    gap: ${token.marginXXS}px;
    flex-wrap: wrap;
  `,
  chip: css`
    height: 26px;
    padding: 0 ${token.paddingSM}px;
    border-radius: 13px;
    border: 1px solid transparent;
    font: inherit;
    font-size: ${token.fontSizeSM}px;
    font-weight: 500;
    font-variant-numeric: tabular-nums;
    cursor: pointer;
  `,
  chipActive: css`
    border-color: ${token.colorPrimaryBorder};
    background: ${token.colorPrimaryBg};
    color: ${token.colorPrimaryText};

    &:hover {
      border-color: ${token.colorPrimary};
    }
  `,
  chipFuture: css`
    border-color: ${token.colorWarningBorder};
    background: ${token.colorWarningBg};
    color: ${token.colorWarningText};

    &:hover {
      border-color: ${token.colorWarning};
    }
  `,
  chipPast: css`
    border-color: ${token.colorBorderSecondary};
    background: ${token.colorFillQuaternary};
    color: ${token.colorTextTertiary};

    &:hover {
      border-color: ${token.colorText};
    }
  `,
  addChip: css`
    height: 26px;
    padding: 0 ${token.paddingSM}px;
    border-radius: 13px;
    border: 1px dashed ${token.colorBorder};
    background: transparent;
    color: ${token.colorTextTertiary};
    font: inherit;
    font-size: ${token.fontSizeSM}px;
    cursor: pointer;

    &:hover {
      border-color: ${token.colorPrimary};
      color: ${token.colorPrimary};
    }
  `,
  nonWorking: css`
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextQuaternary};
  `,
  hours: css`
    width: 44px;
    text-align: right;
    flex-shrink: 0;
    font-size: ${token.fontSizeSM}px;
    font-variant-numeric: tabular-nums;
    color: ${token.colorTextSecondary};
  `,
  hoursEmpty: css`
    color: ${token.colorTextQuaternary};
  `,
  copyTrigger: css`
    display: inline-flex;
    padding: ${token.paddingXXS}px;
    flex-shrink: 0;
    border-radius: ${token.borderRadiusSM}px;
    color: ${token.colorTextTertiary};
    cursor: pointer;
  `,
  copyTriggerDisabled: css`
    color: ${token.colorTextQuaternary};
    cursor: not-allowed;
  `,
}));
