import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { http, HttpResponse } from 'msw';
import { screen, waitFor } from '@testing-library/react';

import { getLoginLoginMockHandler } from '@/api/generated/login/login.msw';
import { LoginStep } from '@/api/generated/schemas';
import { server } from '@/test/msw/server';
import { renderWithProviders } from '@/test/render';

// The page reads `isOidcConfigured` to decide whether it has any business being
// on screen at all, and the test environment has no VITE_OIDC_* variables (the
// FakeAuth dev loop is the default). Pin it, and capture `startSignInRedirect` so
// the "arrived without an authorization request" branch is observable.
const startSignInRedirect = vi.fn();
vi.mock('@/auth/config', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/auth/config')>()),
  isOidcConfigured: true,
  startSignInRedirect: (returnTo: string) => startSignInRedirect(returnTo),
}));

const { LoginPage } = await import('./LoginPage');
const { navigation } = await import('./useLogin');

const AUTH_REQUEST_ID = 'V2_test_auth_request';

/** Puts the page on the URL Zitadel would have redirected to. */
function arriveFromZitadel(query = `?authRequest=${AUTH_REQUEST_ID}`) {
  window.history.replaceState({}, '', `/login${query}`);
}

async function signIn(user: ReturnType<typeof renderWithProviders>['user']) {
  await user.type(screen.getByLabelText('Email o nome utente'), 'tizio@azienda.it');
  await user.type(screen.getByLabelText('Password'), 'hunter2');
  await user.click(screen.getByRole('button', { name: /^Accedi$/ }));
}

describe('LoginPage', () => {
  beforeEach(() => {
    startSignInRedirect.mockClear();
    vi.spyOn(navigation, 'go').mockImplementation(() => {});
    vi.spyOn(navigation, 'replace').mockImplementation(() => {});
    arriveFromZitadel();
  });

  afterEach(() => vi.restoreAllMocks());

  it('renders the sign-in form with the federated providers held back', async () => {
    renderWithProviders(<LoginPage />);

    expect(await screen.findByText('Bentornato. Inserisci le tue credenziali per continuare.'))
      .toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Continua con Google/ })).toBeDisabled();
    expect(screen.getByRole('button', { name: /Continua con Microsoft/ })).toBeDisabled();
  });

  // The completed step is the only one that navigates, and it navigates the whole
  // document: the callback URL belongs to Zitadel, not to our router.
  it('follows the callback URL once the credentials are accepted', async () => {
    server.use(
      getLoginLoginMockHandler({
        step: LoginStep.Completed,
        callbackUrl: 'http://localhost:8080/callback?code=abc',
      }),
    );

    const { user } = renderWithProviders(<LoginPage />);
    await signIn(user);

    await waitFor(() =>
      expect(navigation.go).toHaveBeenCalledWith('http://localhost:8080/callback?code=abc'),
    );
  });

  // A first sign-in against a real Zitadel user always lands here, so it must be a
  // step rather than an error.
  it('asks for a new password when Zitadel demands the change', async () => {
    server.use(getLoginLoginMockHandler({ step: LoginStep.PasswordChangeRequired }));

    const { user } = renderWithProviders(<LoginPage />);
    await signIn(user);

    expect(await screen.findByText('Aggiorna la password')).toBeInTheDocument();
    expect(screen.getByLabelText('Nuova password')).toBeInTheDocument();
    // Federated sign-in disappears mid-flow: taking it now would throw away the
    // password the user has already proved.
    expect(screen.queryByRole('button', { name: /Continua con Google/ })).not.toBeInTheDocument();
  });

  // Honest dead end rather than a spinner that never resolves.
  it('explains a factor it cannot handle instead of failing silently', async () => {
    server.use(getLoginLoginMockHandler({ step: LoginStep.AdditionalFactorRequired }));

    const { user } = renderWithProviders(<LoginPage />);
    await signIn(user);

    expect(await screen.findByText('Serve un altro fattore')).toBeInTheDocument();
  });

  // One sentence for every credential failure — the server never tells the page
  // whether the account exists, and the page must not invent the difference.
  it("shows the server's message when the credentials are refused", async () => {
    server.use(
      http.post('*/api/auth/login', () =>
        HttpResponse.json(
          { status: 400, errors: { credentials: ['Email o password non corretti.'] } },
          { status: 400 },
        ),
      ),
    );

    const { user } = renderWithProviders(<LoginPage />);
    await signIn(user);

    expect(await screen.findByText('Email o password non corretti.')).toBeInTheDocument();
  });

  it('refuses to submit an empty form', async () => {
    const { user } = renderWithProviders(<LoginPage />);

    await user.click(screen.getByRole('button', { name: /^Accedi$/ }));

    expect(await screen.findByText('Inserisci email o nome utente.')).toBeInTheDocument();
    expect(screen.getByText('Inserisci la password.')).toBeInTheDocument();
  });

  // A bookmark or a reload after the id was spent: nothing can be completed, so
  // the page asks the identity provider for a fresh authorization request rather
  // than sitting on a form that cannot work.
  it('asks for an authorization request when it was opened without one', async () => {
    arriveFromZitadel('');

    renderWithProviders(<LoginPage />);

    await waitFor(() => expect(startSignInRedirect).toHaveBeenCalledWith('/'));
    expect(screen.queryByLabelText('Password')).not.toBeInTheDocument();
  });
});
