/* eslint-disable no-restricted-syntax --
 * The only sanctioned hex outside src/app/palette.ts. These are Google's and
 * Microsoft's registered brand marks: their colours are fixed by the respective
 * brand guidelines, are not part of our design language, and must NOT be derived
 * from our palette or re-themed. Deriving them would be both wrong and a
 * trademark problem.
 */
import { Button, Tooltip } from 'antd';
import { useTranslation } from 'react-i18next';

import { useStyles } from './LoginPage.styles';

function GoogleMark() {
  return (
    <svg width="18" height="18" viewBox="0 0 48 48" aria-hidden focusable="false">
      <path
        fill="#FFC107"
        d="M43.6 20.5H42V20H24v8h11.3C33.7 32.7 29.3 36 24 36c-6.6 0-12-5.4-12-12s5.4-12 12-12c3.1 0 5.9 1.1 8 3l5.7-5.7C34.6 6.1 29.6 4 24 4 12.9 4 4 12.9 4 24s8.9 20 20 20 20-8.9 20-20c0-1.3-.1-2.7-.4-3.5z"
      />
      <path
        fill="#FF3D00"
        d="M6.3 14.7l6.6 4.8C14.6 15.9 18.9 13 24 13c3.1 0 5.9 1.1 8 3l5.7-5.7C34.6 6.1 29.6 4 24 4 16.3 4 9.7 8.3 6.3 14.7z"
      />
      <path
        fill="#4CAF50"
        d="M24 44c5.5 0 10.4-1.9 14.2-5.1l-6.6-5.4C29.5 35.4 26.9 36 24 36c-5.3 0-9.7-3.3-11.3-8l-6.6 5.1C9.6 39.6 16.3 44 24 44z"
      />
      <path
        fill="#1976D2"
        d="M43.6 20.5H42V20H24v8h11.3c-.8 2.3-2.3 4.3-4.2 5.6l6.6 5.4C41.5 36.3 44 30.7 44 24c0-1.3-.1-2.7-.4-3.5z"
      />
    </svg>
  );
}

function MicrosoftMark() {
  return (
    <svg width="18" height="18" viewBox="0 0 23 23" aria-hidden focusable="false">
      <path fill="#f35325" d="M1 1h10v10H1z" />
      <path fill="#81bc06" d="M12 1h10v10H12z" />
      <path fill="#05a6f0" d="M1 12h10v10H1z" />
      <path fill="#ffba08" d="M12 12h10v10H12z" />
    </svg>
  );
}

/**
 * Federated sign-in, shown but not yet wired (ADR-0031).
 *
 * Deliberately rendered `disabled` rather than hidden, against the repo's usual
 * "hide, don't disable" rule. That rule is about *capabilities a role does not
 * have* — hiding those avoids teasing the user with something they can never do.
 * Here the gap is temporal, not permissional: these providers are planned, and
 * showing them tells a user arriving with a corporate identity that the door is
 * being built rather than letting them conclude it will never exist.
 *
 * Wiring them up means configuring the identity providers in Zitadel and adding
 * the IDP-intent branch to the login service — one handler each, no change to
 * the surrounding page.
 */
export function SsoButtons() {
  const { t } = useTranslation();
  const { styles } = useStyles();

  return (
    <div className={styles.ssoStack}>
      <Tooltip title={t('auth.login.ssoComingSoon')}>
        {/* A disabled button receives no pointer events, so the tooltip needs a
            live element to hang off. */}
        <span>
          <Button block size="large" disabled icon={<GoogleMark />}>
            {t('auth.login.ssoGoogle')}
          </Button>
        </span>
      </Tooltip>
      <Tooltip title={t('auth.login.ssoComingSoon')}>
        <span>
          <Button block size="large" disabled icon={<MicrosoftMark />}>
            {t('auth.login.ssoMicrosoft')}
          </Button>
        </span>
      </Tooltip>
    </div>
  );
}
