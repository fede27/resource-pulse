import { createStyles } from 'antd-style';

export const useStyles = createStyles(({ token, css }) => ({
  // `--reveal` drives the delete icon's opacity; the row reveals it on hover so
  // there's no hover state in React.
  root: css`
    display: flex;
    align-items: center;
    gap: ${token.marginSM}px;
    padding: ${token.paddingXS}px;
    margin: 0 -${token.marginXS}px;
    border-radius: ${token.borderRadius}px;
    background: transparent;
    transition: background ${token.motionDurationFast};
    --reveal: transparent;
    &:hover {
      background: ${token.colorFillQuaternary};
      --reveal: ${token.colorTextTertiary};
    }
  `,
  name: css`
    flex: 1;
    min-width: 0;
    font-size: ${token.fontSize}px;
    display: flex;
    align-items: center;
    gap: ${token.marginXS}px;
    flex-wrap: wrap;
  `,
  nameText: css`
    font-size: ${token.fontSize}px;
  `,
  chip: css`
    margin: 0;
  `,
  deleteBtn: css`
    transition: color ${token.motionDurationFast};
    && {
      color: var(--reveal);
    }
  `,
}));
