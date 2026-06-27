import { createStyles } from 'antd-style';

export const useStyles = createStyles(({ token, css }) => ({
  loading: css`
    display: flex;
    justify-content: center;
    padding: 80px;
  `,
  nav: css`
    margin-block-end: 20px;
    display: flex;
    gap: ${token.marginXS}px;
    flex-wrap: wrap;
  `,
  navChip: css`
    font-size: ${token.fontSizeSM}px;
    color: ${token.colorTextSecondary};
    cursor: pointer;
    padding: 5px ${token.paddingSM}px;
    border-radius: ${token.borderRadius}px;
    background: ${token.colorBgContainer};
    border: 1px solid ${token.colorBorderSecondary};
    transition: border-color ${token.motionDurationFast};
    &:hover {
      border-color: ${token.colorPrimary};
    }
  `,
  cards: css`
    display: flex;
    flex-direction: column;
    gap: 20px;
    max-width: 900px;
  `,
}));
