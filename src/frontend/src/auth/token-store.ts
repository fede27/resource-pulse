// The bridge between the React auth context and the axios interceptor.
//
// Interceptors are plain module-level functions: they cannot call hooks, so the
// token cannot be read from React context at request time. The auth provider
// publishes it here whenever it changes, and the interceptor reads it.

type AccessTokenReader = () => string | undefined;
type SilentRenew = () => Promise<string | undefined>;
type SignInRedirect = () => void;

let readAccessToken: AccessTokenReader = () => undefined;
let silentRenew: SilentRenew = async () => undefined;
let signInRedirect: SignInRedirect = () => {};

export function publishAuthHandlers(handlers: {
  readAccessToken: AccessTokenReader;
  silentRenew: SilentRenew;
  signInRedirect: SignInRedirect;
}): void {
  readAccessToken = handlers.readAccessToken;
  silentRenew = handlers.silentRenew;
  signInRedirect = handlers.signInRedirect;
}

export function getAccessToken(): string | undefined {
  return readAccessToken();
}

/** Attempts a silent renew; resolves to the fresh token, or undefined on failure. */
export function renewAccessToken(): Promise<string | undefined> {
  return silentRenew();
}

/** Last resort after a failed renew: send the user back through the login flow. */
export function redirectToSignIn(): void {
  signInRedirect();
}
