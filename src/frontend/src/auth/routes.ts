/**
 * The paths the auth flow occupies, named once.
 *
 * They live in their own module because three unrelated places have to agree on
 * them and importing between those three would be circular: the router mounts
 * them, `AuthProvider` must NOT gate them, and the dev bootstrap registers the
 * sign-in path with Zitadel as the application's login URI (ADR-0031).
 */

/** OIDC redirect target — the callback the authorization code comes back to. */
export const CALLBACK_PATH = '/auth/callback';

/**
 * Our own sign-in screen. Zitadel redirects an authorization request here
 * (`/login?authRequest=…`) because the SPA application's `loginVersion` points at
 * this origin.
 *
 * The path itself is <b>Zitadel's</b>, not ours to choose: it appends `/login` to
 * the configured base URI, which is why `Bootstrap__SpaLoginUri` in the AppHost is
 * the SPA's origin and not this path.
 */
export const LOGIN_PATH = '/login';

/**
 * True on the sign-in screen and its sub-pages — the screens that must render for
 * a caller who has no session at all.
 *
 * <b>The callback path is deliberately NOT one of them.</b> It has no session
 * either, but it is mid-exchange: it must stay behind the gate, which is what
 * holds the shell back until `onSigninCallback` has rewritten the URL to the real
 * destination. Letting the router mount on `/auth/callback` would strand the user
 * on the spinner, because a bare `history.replaceState` does not re-route.
 */
export function isSignInPath(pathname: string): boolean {
  return pathname === LOGIN_PATH || pathname.startsWith(`${LOGIN_PATH}/`);
}
