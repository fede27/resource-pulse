import { createStyles } from 'antd-style';

export const useStyles = createStyles(({ token, css }) => ({
  statusRow: css`
    display: flex;
    align-items: center;
    gap: ${token.margin}px;
    margin-bottom: ${token.margin}px;
    flex-wrap: wrap;
  `,
  statusText: css`
    font-size: ${token.fontSize}px;
    color: ${token.colorTextTertiary};
  `,
  statusWarn: css`
    color: ${token.colorWarningText};
  `,
  grid: css`
    display: grid;
    grid-template-columns: 280px minmax(0, 1fr);
    gap: ${token.margin}px;
    align-items: flex-start;
  `,
  panel: css`
    background: ${token.colorBgContainer};
    border: 1px solid ${token.colorBorderSecondary};
    border-radius: ${token.borderRadiusLG}px;
    overflow: hidden;
  `,
  panelHead: css`
    padding: ${token.paddingSM}px ${token.padding}px;
    display: flex;
    align-items: center;
    justify-content: space-between;
    border-bottom: 1px solid ${token.colorBorderSecondary};
  `,
  panelTitle: css`
    font-size: ${token.fontSize}px;
    font-weight: 600;
  `,
  addRow: css`
    padding: ${token.paddingSM}px;
    background: ${token.colorFillQuaternary};
    border-bottom: 1px solid ${token.colorBorderSecondary};
    display: flex;
    gap: ${token.marginXS}px;
  `,
  list: css`
    max-height: 600px;
    overflow: auto;
  `,
  catItem: css`
    position: relative;
    padding: ${token.paddingSM}px ${token.padding}px;
    cursor: pointer;
    border-bottom: 1px solid ${token.colorBorderSecondary};
    display: flex;
    align-items: center;
    gap: ${token.marginXS}px;
    transition: background 0.15s;
    &:hover {
      background: ${token.colorFillQuaternary};
    }
  `,
  catItemActive: css`
    background: ${token.colorPrimaryBg};
    &:hover {
      background: ${token.colorPrimaryBg};
    }
  `,
  activeBar: css`
    position: absolute;
    left: 0;
    top: 0;
    bottom: 0;
    width: 3px;
    background: ${token.colorPrimary};
  `,
  catName: css`
    font-size: ${token.fontSize}px;
    font-weight: 500;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  `,
  catSub: css`
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextTertiary};
    margin-top: 1px;
  `,
  catSubWarn: css`
    color: ${token.colorWarningText};
  `,
  catBadge: css`
    font-size: ${token.fontSizeSM}px;
    font-weight: 600;
    font-variant-numeric: tabular-nums;
    min-width: 22px;
    height: 22px;
    padding: 0 ${token.paddingXS}px;
    border-radius: 11px;
    background: ${token.colorFillSecondary};
    color: ${token.colorTextSecondary};
    display: inline-flex;
    align-items: center;
    justify-content: center;
  `,
  catBadgeActive: css`
    background: ${token.colorPrimaryBorder};
    color: ${token.colorPrimaryText};
  `,
  detailStack: css`
    display: flex;
    flex-direction: column;
    gap: ${token.margin}px;
  `,
  detailCard: css`
    background: ${token.colorBgContainer};
    border: 1px solid ${token.colorBorderSecondary};
    border-radius: ${token.borderRadiusLG}px;
    padding: ${token.paddingLG}px;
  `,
  detailHead: css`
    display: flex;
    align-items: flex-start;
    justify-content: space-between;
    gap: ${token.margin}px;
  `,
  nounLabel: css`
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextTertiary};
    text-transform: uppercase;
    letter-spacing: 0.04em;
    margin-left: ${token.marginXS}px;
  `,
  metrics: css`
    margin-top: ${token.margin}px;
    display: flex;
    gap: ${token.marginLG}px;
    font-size: ${token.fontSize}px;
    color: ${token.colorTextSecondary};
    flex-wrap: wrap;
    font-variant-numeric: tabular-nums;
  `,
  metricLabel: css`
    color: ${token.colorTextTertiary};
  `,
  rule: css`
    margin-top: ${token.margin}px;
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextTertiary};
    line-height: 1.5;
  `,
  peopleHead: css`
    padding: ${token.paddingSM}px ${token.padding}px;
    display: flex;
    align-items: center;
    justify-content: space-between;
    border-bottom: 1px solid ${token.colorBorderSecondary};
  `,
  personRow: css`
    display: flex;
    align-items: center;
    gap: ${token.marginSM}px;
    padding: ${token.paddingSM}px ${token.padding}px;
    border-bottom: 1px solid ${token.colorFillQuaternary};
    cursor: pointer;
    transition: background 0.15s;
    &:hover {
      background: ${token.colorFillQuaternary};
    }
  `,
  personName: css`
    font-size: ${token.fontSize}px;
    font-weight: 500;
  `,
  personSub: css`
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextTertiary};
  `,
  personChevron: css`
    color: ${token.colorTextQuaternary};
    display: inline-flex;
  `,
  emptyPeople: css`
    padding: ${token.paddingXL}px ${token.paddingLG}px;
    text-align: center;
    color: ${token.colorTextTertiary};
    font-size: ${token.fontSize}px;
  `,
  grow: css`
    flex: 1;
    min-width: 0;
  `,
  emptyIcon: css`
    font-size: 48px;
    color: ${token.colorTextQuaternary};
  `,
  emptyPanel: css`
    padding: ${token.paddingXL}px;
  `,
}));
