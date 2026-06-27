import { createStyles } from 'antd-style';

export const useStyles = createStyles(({ token, css }) => ({
  stack: css`
    width: 100%;
  `,
  headerRow: css`
    display: flex;
    gap: ${token.margin}px;
    align-items: flex-start;
  `,
  headerBody: css`
    flex: 1;
    min-width: 0;
  `,
  title: css`
    margin: 0;
    font-weight: 600;
  `,
  email: css`
    margin-block-start: ${token.marginXXS}px;
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextSecondary};
  `,
  roleRow: css`
    margin-block-start: ${token.marginSM}px;
    display: flex;
    align-items: center;
    gap: ${token.marginXS}px;
    flex-wrap: wrap;
  `,
  caption: css`
    font-size: ${token.fontSizeSM}px;
  `,
  cardTitleRow: css`
    display: flex;
    align-items: center;
    justify-content: space-between;
  `,
  cardTitleText: css`
    font-size: 15px;
  `,
  cardTitleSub: css`
    font-size: ${token.fontSizeSM}px;
  `,
  cardDesc: css`
    display: block;
    margin-block-end: ${token.marginSM}px;
    font-size: ${token.fontSizeSM}px;
  `,
  tagWrap: css`
    display: flex;
    flex-wrap: wrap;
    gap: ${token.marginXS}px;
    align-items: center;
    margin-block-end: ${token.marginSM}px;
    min-height: 24px;
  `,
  muted: css`
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextTertiary};
  `,
  mutedBlock: css`
    display: block;
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextTertiary};
    margin-block-end: ${token.marginSM}px;
  `,
  tagItem: css`
    margin-inline-end: 0;
  `,
  comboWrap: css`
    max-width: 340px;
  `,
  skillList: css`
    display: flex;
    flex-direction: column;
    gap: 2px;
    margin-block-end: ${token.marginSM}px;
  `,
  alert: css`
    margin-block-end: ${token.marginSM}px;
  `,
  roleSelect: css`
    min-width: 220px;
  `,
  roleDivider: css`
    margin: ${token.marginXXS}px 0;
  `,
  roleCreateRow: css`
    padding: ${token.paddingXXS}px ${token.paddingXS}px;
    display: flex;
    gap: 6px;
  `,
}));
