import { useCallback, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { isAxiosError } from 'axios';

import { loginChangePassword, loginLogin } from '@/api/generated/login/login';
import { LoginStep } from '@/api/generated/schemas';
import type { ProblemDetails } from '@/api/client';

/** What the page is currently asking for. Mirrors the backend's `LoginStep`. */
export type LoginPhase = 'credentials' | 'passwordChange' | 'additionalFactor';

/**
 * The one place the application leaves the SPA.
 *
 * A named seam rather than a bare `window.location.assign` because this is a
 * genuine boundary — the callback URL belongs to Zitadel and the router has no
 * say in it — and because a document navigation is the one thing a test cannot
 * observe any other way.
 */
export const navigation = {
  go: (url: string) => window.location.assign(url),
  replace: (url: string) => window.location.replace(url),
};

export type LoginState = {
  phase: LoginPhase;
  /** Server-side failure to show above the form. Field errors stay on the fields. */
  error: string | null;
  submitting: boolean;
  /** True from the moment a callback URL is in hand: the page is on its way out. */
  redirecting: boolean;
};

/**
 * Drives the sign-in state machine against our login proxy (ADR-0031).
 *
 * The success path deliberately ends in a full-page navigation rather than a
 * router transition: the callback URL belongs to Zitadel, which answers with a
 * redirect back to `/auth/callback?code=…`. From there the ordinary PKCE flow
 * takes over untouched — which is precisely why nothing downstream had to change.
 */
export function useLogin(authRequestId: string | null) {
  const { t } = useTranslation();
  const [state, setState] = useState<LoginState>({
    phase: 'credentials',
    error: null,
    submitting: false,
    redirecting: false,
  });

  const apply = useCallback(
    (step: LoginStep, callbackUrl: string | null | undefined) => {
      if (step === LoginStep.Completed && callbackUrl) {
        setState((s) => ({ ...s, submitting: false, error: null, redirecting: true }));
        navigation.go(callbackUrl);
        return;
      }

      setState((s) => ({
        ...s,
        submitting: false,
        error: null,
        phase:
          step === LoginStep.PasswordChangeRequired
            ? 'passwordChange'
            : step === LoginStep.AdditionalFactorRequired
              ? 'additionalFactor'
              : s.phase,
      }));
    },
    [],
  );

  const fail = useCallback(
    (e: unknown) => {
      setState((s) => ({ ...s, submitting: false, error: messageFor(e) ?? t('auth.login.genericError') }));
    },
    [t],
  );

  const submitCredentials = useCallback(
    async (loginName: string, password: string) => {
      if (!authRequestId) return;
      setState((s) => ({ ...s, submitting: true, error: null }));

      try {
        const result = await loginLogin({ authRequestId, loginName, password });
        apply(result.step, result.callbackUrl);
      } catch (e) {
        fail(e);
      }
    },
    [authRequestId, apply, fail],
  );

  const submitNewPassword = useCallback(
    async (newPassword: string) => {
      if (!authRequestId) return;
      setState((s) => ({ ...s, submitting: true, error: null }));

      try {
        const result = await loginChangePassword({ authRequestId, newPassword });
        apply(result.step, result.callbackUrl);
      } catch (e) {
        fail(e);
      }
    },
    [authRequestId, apply, fail],
  );

  return { ...state, submitCredentials, submitNewPassword };
}

/**
 * The server's own sentence, when it has one.
 *
 * `useApiError` is the app-wide funnel, but it raises an AntD `message` toast —
 * wrong here: the mockup puts the failure inside the card, where the eye already
 * is, and a sign-in error is about the form rather than about the app.
 */
function messageFor(e: unknown): string | null {
  if (!isAxiosError(e)) return null;

  const data = e.response?.data as ProblemDetails | undefined;
  const firstFieldError = Object.values(data?.errors ?? {})
    .flat()
    .find((m): m is string => typeof m === 'string' && m.length > 0);

  return firstFieldError ?? data?.detail ?? null;
}
