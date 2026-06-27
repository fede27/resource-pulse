import { createStyles } from 'antd-style';

export const useStyles = createStyles(({ token, css }) => ({
  editWrap: css`
    display: inline-flex;
    flex-direction: column;
    gap: 2px;
  `,
  input: css`
    min-width: 180px;
  `,
  error: css`
    font-size: 11px;
    color: ${token.colorError};
  `,
  display: css`
    display: inline-block;
    cursor: text;
    border-radius: ${token.borderRadiusSM}px;
    padding: 1px 4px;
    margin: 0 -4px;
    transition: background ${token.motionDurationFast};
    &:hover {
      background: ${token.colorFillTertiary};
    }
  `,
  displayDisabled: css`
    cursor: default;
    &:hover {
      background: transparent;
    }
  `,
  placeholder: css`
    color: ${token.colorTextTertiary};
  `,
}));
