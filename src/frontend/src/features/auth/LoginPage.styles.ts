import { createStyles } from 'antd-style';

/**
 * The sign-in screen (ADR-0031), ported from the Claude Design mockup.
 *
 * The mockup is stock Ant Design expressed as inline styles over hand-rolled
 * lookalikes, so every value here maps onto a real token rather than being
 * re-typed: `#f0f0f0` is `colorBorderSecondary`, `rgba(0,0,0,.45)` is
 * `colorTextTertiary`, the 40px control is `controlHeightLG`, the card's
 * elevation is `boxShadowTertiary`, and the brand gradient/shadow are the
 * custom tokens the sidebar's mark already uses.
 */
export const useStyles = createStyles(({ token, css }) => ({
  // Owns the viewport: this screen replaces the whole shell.
  page: css`
    min-height: 100vh;
    display: flex;
    align-items: center;
    justify-content: center;
    padding: ${token.paddingLG}px;
    background: linear-gradient(
      180deg,
      ${token.colorBgLayout} 0%,
      ${token.colorPrimaryBg} 100%
    );
  `,
  // 384px = the mockup's card width; also AntD's small-form comfortable width.
  column: css`
    width: 100%;
    max-width: 384px;
  `,
  brand: css`
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: ${token.marginXS}px;
    margin-block-end: ${token.marginXL}px;
    text-align: center;
  `,
  mark: css`
    width: 44px;
    height: 44px;
    border-radius: ${token.borderRadiusLG}px;
    background: ${token.brandGradient};
    box-shadow: ${token.brandLogoShadow};
    color: ${token.colorWhite};
    font-size: ${token.fontSizeHeading4}px;
    display: flex;
    align-items: center;
    justify-content: center;
  `,
  productName: css`
    font-size: ${token.fontSizeLG}px;
    font-weight: 600;
    line-height: 1.3;
  `,
  productTagline: css`
    font-size: ${token.fontSize}px;
    color: ${token.colorTextTertiary};
    margin-block-start: 2px;
  `,
  card: css`
    background: ${token.colorBgContainer};
    border: 1px solid ${token.colorBorderSecondary};
    border-radius: ${token.borderRadiusLG}px;
    padding: ${token.paddingXL}px;
    box-shadow: ${token.boxShadowTertiary};
  `,
  title: css`
    font-size: ${token.fontSizeHeading4}px;
    font-weight: 600;
    margin-block-end: ${token.marginXXS}px;
  `,
  subtitle: css`
    font-size: ${token.fontSize}px;
    color: ${token.colorTextTertiary};
    margin-block-end: ${token.marginLG}px;
  `,
  alert: css`
    margin-block-end: ${token.margin}px;
  `,
  // The mockup's row under the password field. It carried "Ricordami" on the
  // left too; with that removed the link keeps the row to itself.
  forgotRow: css`
    display: flex;
    justify-content: flex-end;
    margin-block: -${token.marginXS}px ${token.marginLG}px;
    font-size: ${token.fontSize}px;
  `,
  divider: css`
    /* Tightens AntD's default 16px block margin to the mockup's rhythm. */
    margin-block: ${token.marginMD}px;
    color: ${token.colorTextTertiary};
    font-size: ${token.fontSizeSM}px;
  `,
  ssoStack: css`
    display: flex;
    flex-direction: column;
    gap: ${token.marginXS}px;
  `,
  footer: css`
    text-align: center;
    font-size: ${token.fontSize}px;
    color: ${token.colorTextTertiary};
    margin-block-start: ${token.marginLG}px;
  `,
}));
