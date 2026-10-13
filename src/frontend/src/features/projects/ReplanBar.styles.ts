import { createStyles } from 'antd-style';

export const ACCOUNT_WIDTH = 720;

export const useStyles = createStyles(({ token, css }) => ({
  // Floats over the bottom of the board: the gesture happens on the timeline,
  // and a bar that pushed it down would move what the planner is looking at.
  dock: css`
    position: fixed;
    inset-block-end: ${token.marginLG}px;
    inset-inline: 0;
    z-index: ${token.zIndexPopupBase};
    display: flex;
    justify-content: center;
    padding-inline: ${token.paddingLG}px;
    pointer-events: none;
  `,
  bar: css`
    pointer-events: auto;
    width: 100%;
    max-width: 780px;
    background: ${token.colorBgElevated};
    border: 1px solid ${token.colorPrimaryBorder};
    border-radius: ${token.borderRadiusLG}px;
    box-shadow: ${token.boxShadowSecondary};
    overflow: hidden;
  `,
  receipt: css`
    border-color: ${token.colorBorder};
  `,
  head: css`
    display: flex;
    align-items: baseline;
    flex-wrap: wrap;
    gap: ${token.marginSM}px;
    padding: ${token.paddingXS}px ${token.padding}px;
    background: ${token.colorPrimaryBg};
  `,
  headTitle: css`
    font-size: ${token.fontSize}px;
    font-weight: 600;
    color: ${token.colorPrimaryText};
  `,
  headSub: css`
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextSecondary};
  `,
  row: css`
    display: grid;
    grid-template-columns: 88px minmax(0, 1fr) auto;
    gap: ${token.marginSM}px;
    align-items: center;
    padding: ${token.paddingXS}px ${token.padding}px;
    border-block-start: 1px solid ${token.colorSplit};
  `,
  rowKind: css`
    font-size: 10px;
    font-weight: 700;
    letter-spacing: 0.04em;
    text-transform: uppercase;
    color: ${token.colorTextSecondary};
  `,
  rowText: css`
    font-size: ${token.fontSize}px;
    color: ${token.colorText};
    line-height: 1.4;
  `,
  rowSub: css`
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextSecondary};
    line-height: 1.45;
  `,
  rowValue: css`
    display: flex;
    align-items: center;
    justify-content: flex-end;
    gap: 6px;
    white-space: nowrap;
    font-size: ${token.fontSizeSM}px;
    font-variant-numeric: tabular-nums;
    color: ${token.colorText};
  `,
  arrow: css`
    color: ${token.colorTextSecondary};
  `,
  foot: css`
    display: flex;
    align-items: center;
    gap: ${token.marginXS}px;
    padding: ${token.paddingXS}px ${token.padding}px;
    border-block-start: 1px solid ${token.colorSplit};
  `,
  footEnd: css`
    margin-inline-start: auto;
    display: flex;
    gap: ${token.marginXS}px;
  `,
  receiptBody: css`
    display: flex;
    align-items: center;
    flex-wrap: wrap;
    gap: ${token.marginSM}px;
    padding: ${token.paddingSM}px ${token.padding}px;
  `,
  receiptWhat: css`
    font-size: ${token.fontSize}px;
    color: ${token.colorText};
  `,
  chip: css`
    display: inline-flex;
    align-items: center;
    gap: 5px;
    height: 20px;
    padding: 0 6px;
    border-radius: ${token.borderRadiusSM}px;
    border: 1px solid transparent;
    font-size: 11px;
    font-weight: 600;
    white-space: nowrap;
    font-variant-numeric: tabular-nums;
  `,
  // ── The account (three sections, behind "see the account") ─────────────
  section: css`
    padding: ${token.paddingSM}px 0;

    & + & {
      border-block-start: 1px solid ${token.colorSplit};
    }
  `,
  sectionHead: css`
    display: flex;
    align-items: baseline;
    gap: ${token.marginXS}px;
    margin-block-end: ${token.marginXS}px;
  `,
  sectionIndex: css`
    width: 18px;
    height: 18px;
    border-radius: ${token.borderRadiusSM}px;
    background: ${token.colorFillSecondary};
    color: ${token.colorTextSecondary};
    font-size: 10px;
    font-weight: 700;
    display: inline-flex;
    align-items: center;
    justify-content: center;
    flex-shrink: 0;
  `,
  sectionTitle: css`
    font-size: ${token.fontSize}px;
    font-weight: 600;
  `,
  sectionHint: css`
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextSecondary};
  `,
  sectionBody: css`
    padding-inline-start: 26px;
    display: grid;
    gap: ${token.marginXS}px;
  `,
  diffRow: css`
    display: grid;
    grid-template-columns: minmax(0, 1.2fr) minmax(0, 1fr) 14px minmax(0, 1fr) 64px;
    gap: ${token.marginXS}px;
    align-items: center;
    font-size: ${token.fontSizeSM}px;
    font-variant-numeric: tabular-nums;
  `,
  diffName: css`
    font-size: ${token.fontSize}px;
    color: ${token.colorText};
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  `,
  muted: css`
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextSecondary};
  `,
  diffDelta: css`
    text-align: end;
    color: ${token.colorTextSecondary};
  `,
  line: css`
    display: flex;
    align-items: center;
    flex-wrap: wrap;
    gap: ${token.marginXS}px;
    font-size: ${token.fontSize}px;
    font-variant-numeric: tabular-nums;
  `,
  note: css`
    padding: ${token.paddingXS}px ${token.paddingSM}px;
    border-radius: ${token.borderRadius}px;
    border: 1px solid ${token.colorBorderSecondary};
    background: ${token.colorFillQuaternary};
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextSecondary};
    line-height: 1.5;

    b {
      display: block;
      color: ${token.colorText};
    }
  `,
  noteWarn: css`
    border-color: ${token.colorWarningBorder};
    background: ${token.colorWarningBg};
  `,
}));
