import type { ThemeConfig } from 'antd';
import type { GetCustomToken } from 'antd-style';
import { alpha, blue } from './palette';

/**
 * Tier 1 — the single source of truth for the global design language
 * (colour, radius, spacing, typography). Applied through antd-style's
 * `<ThemeProvider>` in `App.tsx`, which renders the underlying AntD
 * `ConfigProvider`.
 *
 * Components must read these values via the `token` argument of
 * `createStyles` (or `theme.useToken()`), never by hard-coding hex/spacing.
 * See `src/frontend/CLAUDE.md` → "Styling".
 */
export const appTheme: ThemeConfig = {
  token: {
    // Brand. AntD's default already matches, but pinning it here makes the
    // decision explicit and single-sourced — `getCustomToken` derives the
    // logo gradient from it.
    colorPrimary: blue[5],
    borderRadius: 6,
    fontSize: 14,
  },
  components: {
    // Per-component overrides land here as they emerge. Keep this as the home
    // for "every Button/Card/… should look like X" decisions.
  },
};

/**
 * Custom design tokens with no direct AntD equivalent: brand identity and the
 * shared layout geometry that the app chrome agrees on. Surfaced to
 * `createStyles` as extra `token.*` keys via the module augmentation below.
 *
 * Defined as a function so brand colours derive from `appTheme.token` — the
 * primary colour stays single-sourced.
 */
export interface CustomTokens {
  /** Diagonal brand gradient for the logo mark and brand accents. */
  brandGradient: string;
  /** Drop shadow under the brand logo mark. */
  brandLogoShadow: string;
  /** Height of the sticky app header; sub-headers (tab strips) stick below it. */
  layoutHeaderHeight: number;
  /** Standard padding around routed page content. */
  pageGutter: number;
  /** Max content width for centred pages (full-bleed pages opt out). */
  pageMaxWidth: number;
}

export const getCustomToken: GetCustomToken<CustomTokens> = ({ token }) => ({
  brandGradient: `linear-gradient(135deg, ${token.colorPrimary} 0%, ${token.colorPrimaryActive} 100%)`,
  brandLogoShadow: `0 2px 6px ${alpha(blue[5], 0.25)}`,
  layoutHeaderHeight: 64,
  pageGutter: 24,
  pageMaxWidth: 1440,
});

declare module 'antd-style' {
  // Augment antd-style's CustomToken so `createStyles(({ token }) => …)` sees
  // the keys above as strongly-typed `token.*` members.
  // eslint-disable-next-line @typescript-eslint/no-empty-object-type
  export interface CustomToken extends CustomTokens {}
}
