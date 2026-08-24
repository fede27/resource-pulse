import { describe, expect, it } from 'vitest';

import { CALLBACK_PATH, isSignInPath, LOGIN_PATH } from '@/auth/routes';

// This predicate decides which screens `AuthProvider` does NOT gate (ADR-0031).
// Both directions are load-bearing and neither failure is loud: too wide and the
// callback stops working, too narrow and the sign-in page redirects forever.
describe('isSignInPath', () => {
  it('covers the sign-in screen and its sub-pages', () => {
    expect(isSignInPath(LOGIN_PATH)).toBe(true);
    expect(isSignInPath(`${LOGIN_PATH}/help`)).toBe(true);
  });

  // The callback has no session either, but it is mid-exchange: it must stay
  // behind the gate, which is what holds the shell back until the URL has been
  // rewritten to the real destination.
  it('does NOT cover the OIDC callback', () => {
    expect(isSignInPath(CALLBACK_PATH)).toBe(false);
  });

  it('does not match an application route that merely starts the same way', () => {
    expect(isSignInPath('/loginish')).toBe(false);
    expect(isSignInPath('/projects')).toBe(false);
    expect(isSignInPath('/')).toBe(false);
  });
});
