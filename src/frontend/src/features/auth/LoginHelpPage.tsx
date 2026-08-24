import { Button, Result } from 'antd';
import { useTranslation } from 'react-i18next';

import { useStyles } from './LoginPage.styles';

/**
 * Where "forgot your password?" and "contact your administrator" land today.
 *
 * Deliberately an honest dead end rather than a missing link (ADR-0031): the
 * self-service reset flow — Zitadel's password-reset code, the mail round trip and
 * a second form — is a slice of its own. A link that says what the situation is
 * costs one screen; a link that 404s, or no link at all on a page where every
 * other product puts one, costs a support request.
 */
export function LoginHelpPage() {
  const { t } = useTranslation();
  const { styles } = useStyles();

  return (
    <div className={styles.page}>
      <div className={styles.column}>
        <div className={styles.card}>
          <Result
            status="info"
            title={t('auth.login.forgotTitle')}
            subTitle={t('auth.login.forgotBody')}
            extra={
              <Button type="primary" href="/login">
                {t('auth.login.backToLogin')}
              </Button>
            }
          />
        </div>
      </div>
    </div>
  );
}
