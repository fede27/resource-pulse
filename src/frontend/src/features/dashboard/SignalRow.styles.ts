import { createStyles } from 'antd-style';

export const useStyles = createStyles(({ token, css }) => ({
  // The item stacks the row and its (optional) expander; the row itself is the
  // horizontal band. Keeping them separate is what lets the detail span the full
  // width instead of becoming a fourth flex column.
  item: css`
    border-block-end: 1px solid ${token.colorBorderSecondary};
    transition: background 0.2s;

    &:last-child {
      border-block-end: none;
    }
  `,
  row: css`
    display: flex;
    align-items: flex-start;
    gap: ${token.marginSM}px;
    padding: ${token.paddingSM}px ${token.padding}px;
  `,
  // An accepted risk is dimmed, never hidden: it stays in the queue as a
  // decision somebody took, distinct from a resolved one.
  acknowledged: css`
    background: ${token.colorFillQuaternary};
  `,
  // The zone stripe plus the unseen dot: urgency on the left edge, so the eye
  // gets the ranking before it reads a word.
  gutter: css`
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 6px;
    padding-block-start: 2px;
    flex-shrink: 0;
  `,
  stripe: css`
    width: 4px;
    height: 30px;
    border-radius: 2px;
  `,
  unseenDot: css`
    width: 7px;
    height: 7px;
    border-radius: 50%;
    background: ${token.colorPrimary};
  `,
  body: css`
    flex: 1;
    min-width: 0;
  `,
  chips: css`
    display: flex;
    align-items: center;
    gap: ${token.marginXS}px;
    flex-wrap: wrap;
    margin-block-end: 5px;
  `,
  title: css`
    font-size: ${token.fontSize}px;
    font-weight: 500;
    color: ${token.colorText};
    line-height: 1.45;
  `,
  // Mandatory, always visible: the ranking function decides what exists, so the
  // row has to say why it is here at all.
  reason: css`
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextTertiary};
    margin-block-start: 3px;
    font-variant-numeric: tabular-nums;
  `,
  // Aggregate members: alphabetical, no figures — informative, and structurally
  // incapable of reading as a ranking of people.
  members: css`
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextQuaternary};
    margin-block-start: 5px;
  `,
  ackNote: css`
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextSecondary};
    margin-block-start: 6px;
    padding: 5px ${token.paddingSM}px;
    background: ${token.colorFillQuaternary};
    border-radius: ${token.borderRadius}px;
    border-inline-start: 2px solid ${token.colorBorder};
  `,
  actions: css`
    display: flex;
    align-items: center;
    gap: ${token.marginXXS}px;
    flex-shrink: 0;
  `,
  // The expander is a DECOMPOSITION, not a candidate move: what makes up the
  // breach, never who should fix it.
  detail: css`
    padding: 0 ${token.padding}px ${token.paddingSM}px 46px;
  `,
  detailCard: css`
    padding: ${token.paddingSM}px ${token.paddingSM}px;
    background: ${token.colorFillQuaternary};
    border: 1px solid ${token.colorBorderSecondary};
    border-radius: ${token.borderRadius}px;
  `,
  detailLabel: css`
    font-size: 11px;
    font-weight: 600;
    letter-spacing: 0.06em;
    text-transform: uppercase;
    color: ${token.colorTextQuaternary};
    margin-block-end: 6px;
  `,
  detailList: css`
    display: flex;
    flex-direction: column;
    gap: 4px;
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextSecondary};
  `,
  detailItem: css`
    display: flex;
    justify-content: space-between;
    gap: ${token.marginSM}px;
    font-variant-numeric: tabular-nums;
  `,
  detailNote: css`
    margin-block-start: ${token.marginXS}px;
    padding-block-start: ${token.marginXS}px;
    border-block-start: 1px solid ${token.colorBorderSecondary};
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextQuaternary};
    line-height: 1.5;
  `,
}));
