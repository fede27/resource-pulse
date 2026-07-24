import { createStyles } from 'antd-style';

export const useStyles = createStyles(({ token, css }) => ({
  identity: css`
    display: flex;
    gap: ${token.margin}px;
    align-items: flex-start;
    margin-bottom: ${token.marginLG}px;
  `,
  idBody: css`
    flex: 1;
    min-width: 0;
  `,
  email: css`
    margin-top: ${token.marginXXS}px;
    color: ${token.colorTextSecondary};
  `,
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
  roleDesc: css`
    margin-top: ${token.marginXXS}px;
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextTertiary};
  `,
  tagWrap: css`
    display: flex;
    flex-wrap: wrap;
    gap: ${token.marginXS}px;
    align-items: center;
    margin-bottom: ${token.marginSM}px;
  `,
  tagEmpty: css`
    font-size: ${token.fontSize}px;
    color: ${token.colorTextQuaternary};
  `,
  rule: css`
    margin-top: ${token.margin}px;
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextTertiary};
    line-height: 1.5;
  `,
  ruleStrong: css`
    color: ${token.colorTextSecondary};
    font-weight: 600;
  `,
  deleteWrap: css`
    margin-top: ${token.marginLG}px;
    padding-top: ${token.margin}px;
    border-top: 1px solid ${token.colorBorderSecondary};
  `,
  fullSelect: css`
    width: 100%;
  `,
}));
