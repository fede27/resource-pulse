import { createStyles } from 'antd-style';

// Only what is specific to THIS toolbar's content. The card, its rows, the
// dividers, the right-hand spacer and the result count come from the shared
// `BoardToolbar` — do not re-declare them here.
export const useStyles = createStyles(({ token, css }) => ({
  dimIcon: css`
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextTertiary};
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
