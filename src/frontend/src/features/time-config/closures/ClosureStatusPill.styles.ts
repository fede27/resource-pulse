import { createStyles, keyframes } from 'antd-style';

// The pulse colour is read from a CSS variable set on the dot (where the theme
// token is in scope) so the module-level keyframes stays token-agnostic.
const pulse = keyframes`
  0%, 100% {
    box-shadow: 0 0 0 0 var(--pulse-color);
  }
  50% {
    box-shadow: 0 0 0 4px transparent;
  }
`;

export const useStyles = createStyles(({ token, css }) => {
  return {
    pill: css`
      display: inline-flex;
      align-items: center;
      gap: 5px;
      height: 22px;
      padding: 0 ${token.paddingXS}px;
      border-radius: 11px;
      font-size: ${token.fontSizeSM}px;
      font-weight: 500;
    `,
    ongoing: css`
      background: ${token.colorErrorBg};
      border: 1px solid ${token.colorErrorBorder};
      color: ${token.colorError};
    `,
    upcoming: css`
      background: ${token.colorPrimaryBg};
      border: 1px solid ${token.colorPrimaryBorder};
      color: ${token.colorPrimaryText};
    `,
    past: css`
      background: ${token.colorFillQuaternary};
      border: 1px solid ${token.colorBorderSecondary};
      color: ${token.colorTextTertiary};
    `,
    dot: css`
      --pulse-color: color-mix(in srgb, ${token.colorError} 40%, transparent);
      width: 6px;
      height: 6px;
      border-radius: 50%;
      background: ${token.colorError};
      animation: ${pulse} 2s ease-in-out infinite;
    `,
  };
});
