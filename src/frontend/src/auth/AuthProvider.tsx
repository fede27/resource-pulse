import { useEffect, type ReactNode } from 'react';
import { AuthProvider as OidcProvider, useAuth } from 'react-oidc-context';
import { Alert, Button, Flex, Spin, Typography } from 'antd';
import { useTranslation } from 'react-i18next';

import { isOidcConfigured, oidcConfig, type SigninState } from '@/auth/config';
import { CALLBACK_PATH, isSignInPath } from '@/auth/routes';
import { publishAuthHandlers } from '@/auth/token-store';
import { useAuthStyles } from '@/auth/auth.styles';

/**
 * Wraps the app in the OIDC session (ADR-0029).
 *
 * When no identity provider is configured — the default local dev loop, where
 * the API runs its FakeAuth scheme — this is a pass-through. That keeps `npm run
 * dev` and the whole test suite working without a running Zitadel container.
 */
export function AuthProvider({ children }: { children: ReactNode }) {
  if (!isOidcConfigured) return <>{children}</>;

  // The sign-in screen must render for somebody who has NO session — that is its
  // whole job. Mounting the gate over it would have it redirect to the identity
  // provider, which redirects back to the sign-in screen, forever (ADR-0031). It
  // drives its own `UserManager` when it needs an authorization request, so it
  // needs nothing from the context either.
  if (isSignInPath(window.location.pathname)) return <>{children}</>;

  return (
    <OidcProvider
      {...oidcConfig}
      onSigninCallback={(user) => {
        // Two things have to happen here, and missing either one strands the user.
        //
        // 1. Drop ?code=&state= so a reload cannot replay a spent authorization code.
        // 2. Leave the callback path. It only exists to catch the redirect; it
        //    renders a spinner and nothing ever navigates away from it, so
        //    keeping the path (as `window.location.pathname` does) parks an
        //    authenticated user on a permanent "signing in…" screen.
        //
        // The URL is rewritten BEFORE the app shell mounts — the gate below only
        // renders children once authenticated — so the router initialises on the
        // destination rather than on the callback route.
        const returnTo = (user?.state as SigninState | undefined)?.returnTo;
        const target = returnTo && returnTo !== CALLBACK_PATH ? returnTo : '/';
        window.history.replaceState({}, document.title, target);
      }}
    >
      <AuthGate>{children}</AuthGate>
    </OidcProvider>
  );
}

/**
 * Publishes the session to the axios layer and gates rendering on it: no part of
 * the app should mount — and start firing queries — before there is a token to
 * attach.
 */
function AuthGate({ children }: { children: ReactNode }) {
  const auth = useAuth();
  const { t } = useTranslation();
  const { styles } = useAuthStyles();

  useEffect(() => {
    publishAuthHandlers({
      readAccessToken: () => auth.user?.access_token,
      silentRenew: async () => {
        const user = await auth.signinSilent();
        return user?.access_token ?? undefined;
      },
      signInRedirect: () => {
        void auth.signinRedirect();
      },
    });
  }, [auth]);

  useEffect(() => {
    if (!auth.isLoading && !auth.isAuthenticated && !auth.error && !auth.activeNavigator) {
      // Carry the current location through the identity provider and back, so a
      // deep link survives the login round-trip instead of dumping everyone on
      // the home page. The callback path itself is never a destination.
      const here = window.location.pathname + window.location.search;
      const returnTo = window.location.pathname === CALLBACK_PATH ? '/' : here;
      void auth.signinRedirect({ state: { returnTo } satisfies SigninState });
    }
  }, [auth]);

  if (auth.error) {
    return (
      <Flex className={styles.centered} vertical gap="middle" align="center" justify="center">
        <Alert
          type="error"
          showIcon
          message={t('auth.errorTitle')}
          description={auth.error.message}
        />
        <Button type="primary" onClick={() => void auth.signinRedirect()}>
          {t('auth.retry')}
        </Button>
      </Flex>
    );
  }

  if (!auth.isAuthenticated) {
    return (
      <Flex className={styles.centered} vertical gap="middle" align="center" justify="center">
        <Spin size="large" />
        <Typography.Text type="secondary">{t('auth.signingIn')}</Typography.Text>
      </Flex>
    );
  }

  return <>{children}</>;
}
