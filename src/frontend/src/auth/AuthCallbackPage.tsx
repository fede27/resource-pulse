import { Navigate } from '@tanstack/react-router';
import { Flex, Spin, Typography } from 'antd';
import { useTranslation } from 'react-i18next';

import { useAuthStyles } from '@/auth/auth.styles';

/**
 * Landing spot for the authorization-code redirect.
 *
 * react-oidc-context performs the code exchange as soon as the provider mounts
 * on a URL carrying `?code=`, and the provider then rewrites the URL to the
 * original destination — so in the normal flow this component never renders.
 *
 * It renders only if the app shell somehow mounts while still sitting on the
 * callback path, and in that case it must **leave**: this route has no content
 * of its own, so staying here is a dead end that looks exactly like a hang.
 * That dead end is the failure this redirect exists to make impossible.
 */
export function AuthCallbackPage() {
  const { t } = useTranslation();
  const { styles } = useAuthStyles();

  // The app shell only mounts once authenticated, so reaching this component
  // means the exchange is done and there is somewhere better to be.
  if (!window.location.search.includes('code=')) {
    return <Navigate to="/" replace />;
  }

  return (
    <Flex className={styles.centered} vertical gap="middle" align="center" justify="center">
      <Spin size="large" />
      <Typography.Text type="secondary">{t('auth.completingSignIn')}</Typography.Text>
    </Flex>
  );
}
