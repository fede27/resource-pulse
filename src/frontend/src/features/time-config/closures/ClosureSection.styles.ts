import { createStyles } from 'antd-style';

export const useStyles = createStyles(({ token, css }) => ({
  tnum: css`
    font-variant-numeric: tabular-nums;
  `,
  actionTrigger: css`
    display: inline-flex;
    padding: ${token.paddingXXS}px;
    cursor: pointer;
    color: ${token.colorTextTertiary};
  `,
  card: css`
    overflow: hidden;
  `,
  titleRow: css`
    display: flex;
    align-items: center;
    gap: ${token.marginXS}px;
    cursor: pointer;
  `,
  caret: css`
    transition: transform ${token.motionDurationFast};
    transform: none;
    color: ${token.colorTextTertiary};
    font-size: 10px;
  `,
  caretOpen: css`
    transform: rotate(90deg);
  `,
  statusDot: css`
    width: 8px;
    height: 8px;
    border-radius: 50%;
    display: inline-block;
  `,
  statusUpcoming: css`
    background: ${token.colorPrimary};
  `,
  statusPast: css`
    background: ${token.colorTextDisabled};
  `,
  inlineSlot: css`
    padding: ${token.paddingSM}px;
    background: ${token.colorFillQuaternary};
    border-bottom: 1px solid ${token.colorBorderSecondary};
  `,
  rowClickable: css`
    cursor: pointer;
  `,
}));
