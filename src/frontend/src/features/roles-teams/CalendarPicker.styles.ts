import { createStyles } from 'antd-style';

export const useStyles = createStyles(({ token, css }) => ({
  list: css`
    display: flex;
    flex-direction: column;
    gap: ${token.marginXS}px;
  `,
  option: css`
    padding: ${token.paddingSM}px ${token.padding}px;
    border: 1px solid ${token.colorBorderSecondary};
    border-radius: ${token.borderRadiusLG}px;
    cursor: pointer;
    background: ${token.colorBgContainer};
    display: flex;
    align-items: center;
    gap: ${token.marginSM}px;
    &:hover {
      border-color: ${token.colorPrimaryBorderHover};
    }
  `,
  optionActive: css`
    border-color: ${token.colorPrimary};
    background: ${token.colorPrimaryBg};
  `,
  radio: css`
    width: 18px;
    height: 18px;
    border-radius: 50%;
    border: 2px solid ${token.colorBorder};
    flex-shrink: 0;
    display: flex;
    align-items: center;
    justify-content: center;
  `,
  radioActive: css`
    border-color: ${token.colorPrimary};
  `,
  radioDot: css`
    width: 8px;
    height: 8px;
    border-radius: 50%;
    background: ${token.colorPrimary};
  `,
  body: css`
    flex: 1;
    min-width: 0;
  `,
  name: css`
    font-size: ${token.fontSize}px;
    font-weight: 500;
  `,
  defaultTag: css`
    margin-left: ${token.marginXS}px;
    font-size: 11px;
    color: ${token.colorWarningText};
  `,
  sub: css`
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextTertiary};
  `,
}));
