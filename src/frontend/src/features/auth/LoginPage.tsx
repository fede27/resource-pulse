import { useEffect, useState } from 'react';
import { Alert, Button, Divider, Form, Input, Result, Typography } from 'antd';
import { ClockCircleOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';

import { isOidcConfigured, startSignInRedirect } from '@/auth/config';
import { SsoButtons } from './SsoButtons';
import { navigation, useLogin } from './useLogin';
import { useStyles } from './LoginPage.styles';

/** Zitadel appends this when it hands an authorization request to our page. */
const AUTH_REQUEST_PARAM = 'authRequest';

type CredentialsForm = { loginName: string; password: string };
type PasswordForm = { newPassword: string; confirmPassword: string };

/**
 * Our own sign-in screen (ADR-0031), replacing Zitadel's hosted login.
 *
 * It lives OUTSIDE the app shell and outside `AccessGate`: the caller has no
 * token yet, so there is no tenant, no membership and nothing to gate. What it
 * produces is a callback URL; everything after that — code exchange, PKCE, tenant
 * resolution, membership — is the flow that already existed (ADR-0029/0030).
 */
export function LoginPage() {
  const { t } = useTranslation();
  const { styles } = useStyles();
  const authRequestId = useAuthRequestId();
  const { phase, error, submitting, redirecting, submitCredentials, submitNewPassword } =
    useLogin(authRequestId);

  // Two ways to arrive here without an authorization request: a bookmark, or a
  // reload after the id has been spent. Neither can complete a sign-in, so bounce
  // through the identity provider, which creates one and sends the browser back
  // with it. Self-healing, and the deep link the user was after still survives the
  // round trip because `signinRedirect` carries it in `state`.
  useEffect(() => {
    if (!isOidcConfigured) {
      // FakeAuth is the default development provider: there is no identity
      // provider to sign in against and no login page to show.
      navigation.replace('/');
      return;
    }
    if (!authRequestId) startSignInRedirect('/');
  }, [authRequestId]);

  if (!isOidcConfigured || !authRequestId) {
    return (
      <div className={styles.page}>
        <div className={styles.column}>
          <Brand />
          <div className={styles.card}>
            <Typography.Text type="secondary">{t('auth.signingIn')}</Typography.Text>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className={styles.page}>
      <div className={styles.column}>
        <Brand />

        <div className={styles.card}>
          {phase === 'additionalFactor' ? (
            <Result
              status="info"
              title={t('auth.login.additionalFactorTitle')}
              subTitle={t('auth.login.additionalFactorBody')}
            />
          ) : (
            <>
              <div className={styles.title}>
                {phase === 'passwordChange' ? t('auth.login.changeTitle') : t('auth.login.title')}
              </div>
              <div className={styles.subtitle}>
                {phase === 'passwordChange'
                  ? t('auth.login.changeSubtitle')
                  : t('auth.login.subtitle')}
              </div>

              {redirecting ? (
                <Alert
                  className={styles.alert}
                  type="success"
                  showIcon
                  message={t('auth.login.redirecting')}
                />
              ) : null}
              {error ? (
                <Alert className={styles.alert} type="error" showIcon message={error} />
              ) : null}

              {phase === 'passwordChange' ? (
                <PasswordChangeForm
                  submitting={submitting || redirecting}
                  onSubmit={(v) => void submitNewPassword(v.newPassword)}
                />
              ) : (
                <CredentialsForm
                  submitting={submitting || redirecting}
                  onSubmit={(v) => void submitCredentials(v.loginName, v.password)}
                />
              )}
            </>
          )}

          {/* Federated sign-in belongs to the credentials step only: mid-flow it
              would abandon a password the user has already proved. */}
          {phase === 'credentials' ? (
            <>
              <Divider className={styles.divider} plain>
                {t('auth.login.ssoDivider')}
              </Divider>
              <SsoButtons />
            </>
          ) : null}
        </div>

        <div className={styles.footer}>
          {t('auth.login.noAccount')}{' '}
          {/* Plain anchors, not <Link>: this screen sits outside the app shell on
              purpose, and there is no state worth preserving between two static
              pages. A router dependency would only tie the sign-in screen to the
              thing it deliberately renders before. */}
          <Typography.Link href="/login/help">{t('auth.login.contactAdmin')}</Typography.Link>
        </div>
      </div>
    </div>
  );
}

function Brand() {
  const { t } = useTranslation();
  const { styles } = useStyles();

  return (
    <div className={styles.brand}>
      <div className={styles.mark}>
        <ClockCircleOutlined />
      </div>
      <div>
        <div className={styles.productName}>{t('auth.login.productName')}</div>
        <div className={styles.productTagline}>{t('auth.login.productTagline')}</div>
      </div>
    </div>
  );
}

function CredentialsForm({
  submitting,
  onSubmit,
}: {
  submitting: boolean;
  onSubmit: (values: CredentialsForm) => void;
}) {
  const { t } = useTranslation();
  const { styles } = useStyles();

  return (
    <Form<CredentialsForm> layout="vertical" onFinish={onSubmit} requiredMark={false}>
      <Form.Item
        name="loginName"
        label={t('auth.login.loginNameLabel')}
        rules={[{ required: true, message: t('auth.login.loginNameRequired') }]}
      >
        <Input
          size="large"
          autoComplete="username"
          autoFocus
          placeholder={t('auth.login.loginNamePlaceholder')}
        />
      </Form.Item>

      <Form.Item
        name="password"
        label={t('auth.login.passwordLabel')}
        rules={[{ required: true, message: t('auth.login.passwordRequired') }]}
      >
        <Input.Password
          size="large"
          autoComplete="current-password"
          placeholder={t('auth.login.passwordPlaceholder')}
        />
      </Form.Item>

      {/* On its own row, as in the mockup — and deliberately NOT inside the
          label: nesting a link there makes the field's accessible name read
          "Password Password dimenticata?". */}
      <div className={styles.forgotRow}>
        <Typography.Link href="/login/help">{t('auth.login.forgotPassword')}</Typography.Link>
      </div>

      <Button type="primary" htmlType="submit" block size="large" loading={submitting}>
        {submitting ? t('auth.login.submitting') : t('auth.login.submit')}
      </Button>
    </Form>
  );
}

function PasswordChangeForm({
  submitting,
  onSubmit,
}: {
  submitting: boolean;
  onSubmit: (values: PasswordForm) => void;
}) {
  const { t } = useTranslation();

  return (
    <Form<PasswordForm> layout="vertical" onFinish={onSubmit} requiredMark={false}>
      <Form.Item
        name="newPassword"
        label={t('auth.login.newPasswordLabel')}
        rules={[{ required: true, message: t('auth.login.newPasswordRequired') }]}
      >
        <Input.Password
          size="large"
          autoComplete="new-password"
          autoFocus
          placeholder={t('auth.login.newPasswordPlaceholder')}
        />
      </Form.Item>

      <Form.Item
        name="confirmPassword"
        label={t('auth.login.confirmPasswordLabel')}
        dependencies={['newPassword']}
        rules={[
          { required: true, message: t('auth.login.newPasswordRequired') },
          // The complexity rules themselves stay with Zitadel's password policy:
          // duplicating them here is how the two drift apart.
          ({ getFieldValue }) => ({
            validator: (_, value: string) =>
              !value || value === getFieldValue('newPassword')
                ? Promise.resolve()
                : Promise.reject(new Error(t('auth.login.confirmPasswordMismatch'))),
          }),
        ]}
      >
        <Input.Password size="large" autoComplete="new-password" />
      </Form.Item>

      <Button type="primary" htmlType="submit" block size="large" loading={submitting}>
        {t('auth.login.changeSubmit')}
      </Button>
    </Form>
  );
}

/** The authorization request id from the query string, read once per render. */
function useAuthRequestId(): string | null {
  const [id] = useState(() => new URLSearchParams(window.location.search).get(AUTH_REQUEST_PARAM));
  return id;
}
