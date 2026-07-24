import { createStyles } from 'antd-style';

export const useStyles = createStyles(({ token, css }) => ({
  typeRow: css`
    display: flex;
    gap: ${token.marginXS}px;
    margin-bottom: ${token.margin}px;
  `,
  typeBtn: css`
    flex: 1;
    padding: ${token.paddingSM}px ${token.padding}px;
    border: 1px solid ${token.colorBorder};
    border-radius: ${token.borderRadiusLG}px;
    background: ${token.colorBgContainer};
    color: ${token.colorTextSecondary};
    cursor: pointer;
    font: inherit;
    text-align: left;
  `,
  typeBtnFerie: css`
    border-color: ${token.colorPrimary};
    background: ${token.colorPrimaryBg};
    color: ${token.colorPrimary};
    font-weight: 600;
  `,
  typeBtnExtra: css`
    border-color: ${token.colorWarning};
    background: ${token.colorWarningBg};
    color: ${token.colorWarningText};
    font-weight: 600;
  `,
  typeHint: css`
    font-size: 11px;
    font-weight: 400;
    margin-top: ${token.marginXXS}px;
    opacity: 0.85;
  `,
  field: css`
    margin-bottom: ${token.marginSM}px;
  `,
  label: css`
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextSecondary};
    margin-bottom: ${token.marginXXS}px;
  `,
  err: css`
    font-size: 11px;
    color: ${token.colorError};
    margin-top: ${token.marginXXS}px;
  `,
  twoCol: css`
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: ${token.marginSM}px;
  `,
  fullDayBox: css`
    margin-top: ${token.marginXXS}px;
    padding: ${token.paddingSM}px;
    background: ${token.colorFillQuaternary};
    border-radius: ${token.borderRadiusLG}px;
    border: 1px solid ${token.colorBorderSecondary};
  `,
  hoursRow: css`
    margin-top: ${token.marginSM}px;
    display: flex;
    align-items: center;
    gap: ${token.marginXS}px;
  `,
  hoursHint: css`
    margin-top: ${token.marginXS}px;
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextTertiary};
  `,
  footer: css`
    display: flex;
    justify-content: space-between;
    align-items: center;
  `,
  footerRight: css`
    display: flex;
    gap: ${token.marginXS}px;
  `,
  fullW: css`
    width: 100%;
  `,
}));
