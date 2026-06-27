import { createStyles } from 'antd-style';

export const useStyles = createStyles(({ token, css }) => ({
  content: css`
    width: 340px;
  `,
  searchIcon: css`
    color: ${token.colorTextQuaternary};
  `,
  search: css`
    margin-block-end: ${token.marginSM}px;
  `,
  countRow: css`
    display: flex;
    justify-content: space-between;
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextTertiary};
    padding: 0 2px 8px;
  `,
  list: css`
    max-height: 340px;
    overflow: auto;
    margin: 0 -6px;
  `,
  row: css`
    display: flex;
    align-items: center;
    gap: 10px;
    padding: 6px;
    border-radius: ${token.borderRadius}px;
    background: transparent;
  `,
  rowMember: css`
    background: ${token.colorPrimaryBg};
  `,
  rowBody: css`
    flex: 1;
    min-width: 0;
  `,
  name: css`
    font-size: ${token.fontSizeSM}px;
    font-weight: 500;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  `,
  role: css`
    font-size: 11px;
    color: ${token.colorTextTertiary};
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  `,
  teamTag: css`
    margin-inline-end: 0;
    font-size: 10px;
    line-height: 16px;
  `,
  empty: css`
    margin: ${token.marginSM}px 0;
  `,
}));
