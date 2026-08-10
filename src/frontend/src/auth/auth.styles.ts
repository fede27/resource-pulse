import { createStyles } from 'antd-style';

export const useAuthStyles = createStyles(({ token, css }) => ({
  // The sign-in and callback screens replace the whole shell, so they own the
  // viewport rather than sitting inside the page container.
  centered: css`
    min-height: 100vh;
    padding: ${token.paddingLG}px;
    text-align: center;
  `,
}));

export const useDevRoleSwitcherStyles = createStyles(({ token, css }) => ({
  // Dev-only chrome: readable, but visibly not part of the product surface.
  switcher: css`
    font-size: ${token.fontSizeSM}px;
  `,
}));
