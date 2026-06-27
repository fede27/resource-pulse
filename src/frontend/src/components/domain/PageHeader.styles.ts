import { createStyles } from 'antd-style';

export const useStyles = createStyles(({ token, css }) => ({
  root: css`
    display: flex;
    align-items: flex-end;
    justify-content: space-between;
    gap: ${token.marginLG}px;
    flex-wrap: wrap;
    margin-block-end: ${token.marginLG}px;
  `,
  titleWrap: css`
    min-width: 0;
  `,
  title: css`
    margin: 0;
  `,
  subtitle: css`
    margin-block-start: ${token.marginXXS}px;
    color: ${token.colorTextTertiary};
    font-size: ${token.fontSize}px;
  `,
  actions: css`
    display: flex;
    gap: ${token.marginXS}px;
    align-items: center;
  `,
}));
