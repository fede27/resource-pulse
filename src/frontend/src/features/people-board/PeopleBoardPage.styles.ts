import { createStyles } from 'antd-style';
import { LEFT_W } from '@/components/board/BoardTimeline.styles';

// Fixed group-header height (outer, border included): the vertical windowing
// derives row offsets from state, so this may not be content-driven.
export const GROUP_HEADER_H = 33;

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
    display: flex;
    align-items: stretch;
    height: ${GROUP_HEADER_H}px;
    /* Border inside the height so the windowed offset math sees GROUP_HEADER_H. */
    box-sizing: border-box;
    background: ${token.colorFillQuaternary};
    border-block-end: 1px solid ${token.colorBorderSecondary};
  `,
  groupHeaderLabel: css`
    width: ${LEFT_W}px;
    flex-shrink: 0;
    position: sticky;
    left: 0;
    z-index: 4;
    /* Opaque: the sticky label must fully cover the backdrop scrolling underneath. */
    background: linear-gradient(${token.colorFillQuaternary}, ${token.colorFillQuaternary}),
      ${token.colorBgContainer};
    border-inline-end: 1px solid ${token.colorBorderSecondary};
    display: flex;
    align-items: center;
    gap: ${token.marginXS}px;
    padding: 0 ${token.paddingSM}px;
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
