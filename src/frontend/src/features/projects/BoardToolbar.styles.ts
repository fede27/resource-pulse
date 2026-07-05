import { createStyles } from 'antd-style';

export const useStyles = createStyles(({ token, css }) => ({
  toolbar: css`
    background: ${token.colorBgContainer};
    border: 1px solid ${token.colorBorderSecondary};
    border-radius: ${token.borderRadiusLG}px;
    margin-block-end: ${token.margin}px;
  `,
  controls: css`
    display: flex;
    align-items: center;
    gap: ${token.marginXS}px;
    padding: ${token.paddingXS}px ${token.paddingSM}px;
    flex-wrap: wrap;
    border-block-end: 1px solid ${token.colorSplit};
  `,
  divider: css`
    width: 1px;
    height: 22px;
    background: ${token.colorSplit};
  `,
  dimLabel: css`
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextTertiary};
  `,
  yearStepper: css`
    display: inline-flex;
    align-items: center;
    height: 32px;
    border: 1px solid ${token.colorBorder};
    border-radius: ${token.borderRadius}px;
    overflow: hidden;
    background: ${token.colorBgContainer};
  `,
  yearLabel: css`
    padding: 0 ${token.paddingXS}px;
    font-size: ${token.fontSize}px;
    font-weight: 600;
    font-variant-numeric: tabular-nums;
    color: ${token.colorTextSecondary};
  `,
  spacer: css`
    margin-inline-start: auto;
    display: inline-flex;
    align-items: center;
    gap: ${token.marginXS}px;
  `,
  chipsRow: css`
    display: flex;
    align-items: center;
    gap: ${token.marginXS}px;
    padding: ${token.paddingXS}px ${token.paddingSM}px;
    flex-wrap: wrap;
  `,
  resultCount: css`
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextTertiary};
    font-weight: 500;

    strong {
      color: ${token.colorTextSecondary};
      font-variant-numeric: tabular-nums;
    }
  `,
  noFilters: css`
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextQuaternary};
  `,
  filterPanel: css`
    width: 320px;
    max-height: 60vh;
    overflow: auto;
  `,
  panelSection: css`
    font-size: 11px;
    font-weight: 600;
    letter-spacing: 0.05em;
    text-transform: uppercase;
    color: ${token.colorTextTertiary};
    margin-block: ${token.marginSM}px ${token.marginXXS}px;

    &:first-of-type {
      margin-block-start: 0;
    }
  `,
  checkRow: css`
    display: flex;
    align-items: center;
    gap: ${token.marginXS}px;
    padding: 3px 0;
    cursor: pointer;
    font-size: ${token.fontSize}px;
  `,
  dot: css`
    width: 8px;
    height: 8px;
    border-radius: 50%;
    flex-shrink: 0;
  `,
  roleGrid: css`
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 0 ${token.marginSM}px;
  `,
  panelHeader: css`
    display: flex;
    align-items: center;
    justify-content: space-between;
    margin-block-end: ${token.marginXS}px;

    span {
      font-weight: 600;
    }
  `,
  fullWidth: css`
    width: 100%;
  `,
}));
