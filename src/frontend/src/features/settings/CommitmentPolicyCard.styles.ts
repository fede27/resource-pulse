import { createStyles } from 'antd-style';

export const useStyles = createStyles(({ token, css }) => ({
  enableLabel: css`
    font-size: ${token.fontSizeSM}px;
    font-weight: 500;
    margin-block-end: ${token.marginSM}px;
  `,
  list: css`
    display: flex;
    flex-direction: column;
    gap: ${token.marginXS}px;
  `,
  option: css`
    display: flex;
    align-items: center;
    gap: ${token.marginSM}px;
    padding: ${token.paddingXS}px ${token.paddingSM}px;
    cursor: pointer;
    border-radius: ${token.borderRadiusLG}px;
    border: 1px solid ${token.colorBorderSecondary};
    background: ${token.colorBgContainer};
    transition: all ${token.motionDurationMid};
  `,
  optionOn: css`
    border-color: ${token.colorPrimaryBorder};
    background: ${token.colorPrimaryBg};
  `,
  optionBody: css`
    flex: 1;
  `,
  optionTitle: css`
    font-size: ${token.fontSize}px;
    font-weight: 500;
    display: flex;
    align-items: center;
    gap: ${token.marginXS}px;
  `,
  permitsChip: css`
    height: 18px;
    padding: 0 ${token.paddingXXS}px;
    border-radius: 9px;
    font-size: 11px;
    font-weight: 500;
    background: ${token.colorSuccessBg};
    border: 1px solid ${token.colorSuccessBorder};
    color: ${token.colorSuccessText};
    display: inline-flex;
    align-items: center;
  `,
  optionDesc: css`
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextTertiary};
    margin-block-start: ${token.marginXXS}px;
  `,
  error: css`
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorError};
    margin-block-start: ${token.marginSM}px;
  `,
  summary: css`
    margin-block-start: ${token.marginSM}px;
    padding: ${token.paddingXS}px ${token.paddingSM}px;
    background: ${token.colorInfoBg};
    border: 1px solid ${token.colorInfoBorder};
    border-radius: ${token.borderRadius}px;
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorText};
  `,
}));
