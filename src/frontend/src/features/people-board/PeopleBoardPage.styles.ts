import { createStyles } from 'antd-style';

export const useStyles = createStyles(({ token, css }) => ({
  headerRow: css`
    display: flex;
    align-items: flex-end;
    justify-content: space-between;
    gap: ${token.marginLG}px;
    flex-wrap: wrap;
  `,
  kpis: css`
    display: flex;
    gap: ${token.marginLG}px;
    margin-block-end: ${token.margin}px;
  `,
  kpi: css`
    min-width: 180px;
  `,
  groupHeader: css`
    position: sticky;
    left: 0;
    display: flex;
    align-items: center;
    gap: ${token.marginXS}px;
    padding: 6px ${token.paddingSM}px;
    background: ${token.colorFillQuaternary};
    border-block-end: 1px solid ${token.colorBorderSecondary};
    font-size: 13px;
    font-weight: 600;

    span:last-child {
      color: ${token.colorTextTertiary};
      font-weight: 400;
      font-variant-numeric: tabular-nums;
    }
  `,
  legend: css`
    display: flex;
    flex-wrap: wrap;
    gap: ${token.marginXS}px ${token.marginLG}px;
    align-items: center;
    padding: ${token.paddingXS}px ${token.paddingSM}px;
    margin-block-start: ${token.marginSM}px;
    background: ${token.colorFillQuaternary};
    border: 1px solid ${token.colorBorderSecondary};
    border-radius: ${token.borderRadiusLG}px;
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextSecondary};
  `,
  legendTitle: css`
    font-weight: 600;
    color: ${token.colorText};
  `,
  legendItem: css`
    display: inline-flex;
    align-items: center;
    gap: 6px;
  `,
  legendSwatch: css`
    width: 14px;
    height: 12px;
    border-radius: 3px;
  `,
  legendNote: css`
    color: ${token.colorTextQuaternary};
  `,
  footnote: css`
    display: flex;
    align-items: center;
    gap: ${token.marginXS}px;
    margin-block-start: ${token.marginSM}px;
    color: ${token.colorTextTertiary};
    font-size: ${token.fontSizeSM}px;
  `,
  fetchingHint: css`
    display: inline-flex;
    align-items: center;
  `,
}));
