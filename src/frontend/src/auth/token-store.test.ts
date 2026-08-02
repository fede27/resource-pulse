import { beforeEach, describe, expect, it, vi } from 'vitest';

import {
  getAccessToken,
  publishAuthHandlers,
  redirectToSignIn,
  renewAccessToken,
} from '@/auth/token-store';

// The bridge between the React auth context and the axios interceptor. An
// interceptor cannot call hooks, so the session has to be published here; if
// that publication breaks, every request silently goes out unauthenticated.
describe('token-store', () => {
  beforeEach(() => {
    publishAuthHandlers({
      readAccessToken: () => undefined,
      silentRenew: async () => undefined,
      signInRedirect: () => {},
    });
  });

  it('returns no token before a session is published', () => {
    expect(getAccessToken()).toBeUndefined();
  });

  it('reads the token through the published handler on every call', () => {
    let current: string | undefined = 'first';
    publishAuthHandlers({
      readAccessToken: () => current,
      silentRenew: async () => undefined,
      signInRedirect: () => {},
    });

    expect(getAccessToken()).toBe('first');

    // Reading lazily is the point: the interceptor holds no snapshot, so a
    // renewed token is picked up without re-publishing.
    current = 'renewed';
    expect(getAccessToken()).toBe('renewed');
  });

  it('delegates renewal and sign-in to the published handlers', async () => {
    const silentRenew = vi.fn(async () => 'fresh-token');
    const signInRedirect = vi.fn();

    publishAuthHandlers({ readAccessToken: () => 'stale', silentRenew, signInRedirect });

    await expect(renewAccessToken()).resolves.toBe('fresh-token');
    expect(silentRenew).toHaveBeenCalledOnce();

    redirectToSignIn();
    expect(signInRedirect).toHaveBeenCalledOnce();
  });

  it('surfaces a failed renewal as undefined rather than throwing', async () => {
    publishAuthHandlers({
      readAccessToken: () => undefined,
      silentRenew: async () => undefined,
      signInRedirect: () => {},
    });

    await expect(renewAccessToken()).resolves.toBeUndefined();
  });
});
