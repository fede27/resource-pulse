import { createStyles } from 'antd-style';

export const useStyles = createStyles(({ token, css }) => ({
  // Deliberately narrower than the boards. This page is a shortlist to read, not
  // a grid to scan: full width would invite the columns it exists without.
  page: css`
    max-width: 980px;
  `,
  // Text, not a gauge — a number out of context invites the comparison with
  // yesterday's number that this page refuses to be about.
  verdict: css`
    padding: ${token.paddingSM}px ${token.padding}px;
    border-radius: ${token.borderRadiusLG}px;
    border: 1px solid ${token.colorBorderSecondary};
    background: ${token.colorFillQuaternary};
    font-size: ${token.fontSizeLG}px;
    line-height: 1.6;
    color: ${token.colorText};
    margin-block-end: ${token.marginLG}px;
  `,
  verdictClean: css`
    background: ${token.colorSuccessBg};
    border-color: ${token.colorSuccessBorder};
  `,
  verdictBreach: css`
    background: ${token.colorWarningBg};
    border-color: ${token.colorWarningBorder};
  `,
  sectionHead: css`
    display: flex;
    align-items: baseline;
    justify-content: space-between;
    gap: ${token.marginSM}px;
    margin-block-end: ${token.marginXS}px;
  `,
  sectionTitle: css`
    font-size: ${token.fontSize}px;
    font-weight: 600;
    color: ${token.colorTextSecondary};
  `,
  // The ranking rule, stated. A queue whose order cannot be explained is a queue
  // people stop trusting.
  sectionHint: css`
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextQuaternary};
  `,
  card: css`
    background: ${token.colorBgContainer};
    border: 1px solid ${token.colorBorderSecondary};
    border-radius: ${token.borderRadiusLG}px;
    overflow: hidden;
  `,
  // Stated, never expanded: the page does not grow past its budget, so the
  // overflow is a sentence rather than a "show more".
  overflow: css`
    padding: ${token.paddingXS}px 4px 0;
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextTertiary};
    font-variant-numeric: tabular-nums;
  `,
  section: css`
    margin-block-start: ${token.marginLG}px;
  `,
  feedRow: css`
    display: flex;
    align-items: center;
    gap: ${token.marginSM}px;
    padding: ${token.paddingXS}px ${token.padding}px;
    border-block-end: 1px solid ${token.colorBorderSecondary};

    &:last-child {
      border-block-end: none;
    }
  `,
  feedText: css`
    flex: 1;
    min-width: 0;
    font-size: ${token.fontSize}px;
    color: ${token.colorText};
  `,
  feedWhen: css`
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextQuaternary};
    flex-shrink: 0;
    font-variant-numeric: tabular-nums;
  `,
  empty: css`
    padding: ${token.paddingXL}px ${token.padding}px;
    text-align: center;
  `,
  emptyTitle: css`
    font-size: ${token.fontSizeLG}px;
    font-weight: 600;
    color: ${token.colorText};
    margin-block-start: ${token.marginXS}px;
  `,
  emptyBody: css`
    font-size: ${token.fontSize}px;
    color: ${token.colorTextSecondary};
    line-height: 1.6;
    max-width: 460px;
    margin: ${token.marginXS}px auto 0;
  `,
}));
