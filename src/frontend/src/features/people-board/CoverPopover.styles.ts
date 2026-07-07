import { createStyles } from 'antd-style';

export const useStyles = createStyles(({ token, css }) => ({
  // The popup chrome (background, radius, shadow) comes from the AntD Popover;
  // its container slot is zeroed so the card sections run edge to edge.
  popContainer: css`
    && {
      padding: 0;
    }
  `,
  anchor: css`
    display: inline-block;
    width: 0;
    height: 0;
  `,
  card: css`
    width: 320px;
    border-radius: ${token.borderRadiusLG}px;
    overflow: hidden;
    cursor: default;
  `,
  header: css`
    padding: ${token.paddingXS}px ${token.paddingSM}px;
    border-block-end: 1px solid ${token.colorSplit};
  `,
  headerTitle: css`
    font-size: ${token.fontSize}px;
    font-weight: 600;
  `,
  headerSub: css`
    font-size: 11px;
    color: ${token.colorTextTertiary};
    margin-block-start: 2px;
    font-variant-numeric: tabular-nums;
  `,
  body: css`
    max-height: 260px;
    overflow: auto;
  `,
  sectionTitle: css`
    padding: ${token.paddingXS}px ${token.paddingSM}px 4px;
    font-size: 10px;
    font-weight: 700;
    letter-spacing: 0.06em;
    text-transform: uppercase;
    color: ${token.colorTextQuaternary};
  `,
  demandRow: css`
    display: flex;
    align-items: center;
    gap: ${token.marginXS}px;
    padding: ${token.paddingXS}px ${token.paddingSM}px;
    cursor: pointer;

    &:hover {
      background: ${token.colorFillTertiary};
    }
  `,
  demandDot: css`
    width: 8px;
    height: 8px;
    border-radius: 2px;
    flex-shrink: 0;
  `,
  demandText: css`
    flex: 1;
    min-width: 0;
  `,
  demandProject: css`
    font-size: 13px;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  `,
  demandResidual: css`
    font-size: 11px;
    color: ${token.colorErrorText};
    font-variant-numeric: tabular-nums;
  `,
  demandBestEffort: css`
    font-size: 11px;
    color: ${token.colorTextTertiary};
  `,
  coverWord: css`
    flex-shrink: 0;
    font-size: 11px;
    font-weight: 600;
    color: ${token.colorSuccessText};
    white-space: nowrap;
  `,
  emptyNote: css`
    padding: ${token.paddingXS}px ${token.paddingSM}px;
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextTertiary};
  `,
  chips: css`
    display: flex;
    flex-wrap: wrap;
    gap: 6px;
    padding: 4px ${token.paddingSM}px ${token.paddingSM}px;
  `,
  projectChip: css`
    display: inline-flex;
    align-items: center;
    gap: 5px;
    height: 24px;
    padding: 0 ${token.paddingXS}px;
    border-radius: 12px;
    cursor: pointer;
    background: ${token.colorBgContainer};
    font-size: 11px;

    &:hover {
      background: ${token.colorFillTertiary};
    }
  `,
  chipDot: css`
    width: 7px;
    height: 7px;
    border-radius: 2px;
  `,
  roleSelect: css`
    flex: 1;
  `,
  roleSelectRow: css`
    display: flex;
    align-items: center;
    gap: ${token.marginXS}px;
    padding: 4px ${token.paddingSM}px;
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextSecondary};
  `,
}));
