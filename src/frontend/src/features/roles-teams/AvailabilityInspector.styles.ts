import { createStyles } from 'antd-style';

export const useStyles = createStyles(({ token, css }) => ({
  section: css`
    font-size: ${token.fontSizeSM}px;
    font-weight: 600;
    text-transform: uppercase;
    letter-spacing: 0.04em;
    color: ${token.colorTextTertiary};
    margin: ${token.marginLG}px 0 ${token.marginSM}px;
  `,
  sectionFirst: css`
    margin-top: 0;
  `,
  calCard: css`
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: ${token.marginXS}px;
    padding: ${token.paddingSM}px ${token.padding}px;
    background: ${token.colorFillQuaternary};
    border: 1px solid ${token.colorBorderSecondary};
    border-radius: ${token.borderRadiusLG}px;
  `,
  calName: css`
    font-size: ${token.fontSize}px;
    font-weight: 500;
  `,
  calSub: css`
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextTertiary};
  `,
  dayHero: css`
    display: flex;
    align-items: center;
    gap: ${token.margin}px;
    padding: ${token.paddingSM}px ${token.padding}px;
    border-radius: ${token.borderRadiusLG}px;
    margin-bottom: ${token.marginXS}px;
  `,
  heroHours: css`
    font-size: 30px;
    font-weight: 700;
    font-variant-numeric: tabular-nums;
    line-height: 1;
  `,
  heroLabel: css`
    font-size: ${token.fontSizeSM}px;
  `,
  heroState: css`
    font-weight: 600;
  `,
  row: css`
    display: flex;
    justify-content: space-between;
    gap: ${token.marginSM}px;
    padding: ${token.paddingXS}px 0;
    border-bottom: 1px solid ${token.colorFillQuaternary};
    font-size: ${token.fontSize}px;
  `,
  rowLabel: css`
    color: ${token.colorTextTertiary};
  `,
  rowValue: css`
    font-variant-numeric: tabular-nums;
    text-align: right;
  `,
  rowStrong: css`
    font-weight: 700;
  `,
  twoStat: css`
    display: flex;
    gap: ${token.marginXS}px;
    margin-bottom: ${token.marginSM}px;
  `,
  statBox: css`
    flex: 1;
    padding: ${token.paddingSM}px ${token.padding}px;
    border-radius: ${token.borderRadiusLG}px;
    border: 1px solid ${token.colorBorderSecondary};
  `,
  statBoxAccent: css`
    background: ${token.colorPrimaryBg};
    border-color: ${token.colorPrimaryBorder};
  `,
  statLabel: css`
    font-size: 11px;
    color: ${token.colorTextTertiary};
  `,
  statLabelAccent: css`
    color: ${token.colorPrimaryText};
  `,
  statValue: css`
    font-size: 22px;
    font-weight: 700;
    font-variant-numeric: tabular-nums;
    color: ${token.colorTextSecondary};
  `,
  statValueAccent: css`
    color: ${token.colorPrimaryText};
  `,
  delta: css`
    font-size: ${token.fontSizeSM}px;
    margin-bottom: ${token.marginXXS}px;
  `,
  exception: css`
    display: flex;
    align-items: center;
    gap: ${token.marginSM}px;
    padding: ${token.paddingXS}px 0;
    border-bottom: 1px solid ${token.colorFillQuaternary};
    cursor: pointer;
  `,
  excDot: css`
    width: 8px;
    height: 8px;
    border-radius: 2px;
    flex-shrink: 0;
  `,
  excBody: css`
    flex: 1;
    min-width: 0;
  `,
  excReason: css`
    font-size: ${token.fontSize}px;
    font-weight: 500;
  `,
  excMeta: css`
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextTertiary};
    font-variant-numeric: tabular-nums;
  `,
  excTag: css`
    font-size: 11px;
    font-weight: 600;
  `,
  noExc: css`
    font-size: ${token.fontSize}px;
    color: ${token.colorTextTertiary};
  `,
  addRow: css`
    display: flex;
    gap: ${token.marginXS}px;
    margin-top: ${token.marginSM}px;
  `,
  explain: css`
    margin-top: ${token.margin}px;
    padding: ${token.paddingSM}px ${token.padding}px;
    background: ${token.colorFillQuaternary};
    border-radius: ${token.borderRadiusLG}px;
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextSecondary};
    line-height: 1.55;
  `,
}));
