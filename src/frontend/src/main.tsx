import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { QueryClientProvider } from '@tanstack/react-query';
import { UserManager } from 'oidc-client-ts';

import { queryClient } from '@/app/query-client';
import { App } from '@/app/App';
import { AuthProvider } from '@/auth/AuthProvider';
import { isOidcConfigured, oidcConfig } from '@/auth/config';

// i18n initialises on import; the type augmentation lives in ./i18n/types.
import '@/i18n';
import '@/i18n/types';

const rootElement = document.getElementById('root');
if (!rootElement) throw new Error('Root element #root not found');

// Silent renew loads the SPA in a hidden iframe. Booting the whole application
// there would mount a second auth provider, which would itself try to sign in —
// so handle the callback and stop, rendering nothing.
if (isSilentRenewFrame()) {
  void new UserManager(oidcConfig).signinSilentCallback();
} else {
  // AuthProvider sits ABOVE the query client: no query should fire before there
  // is a token to attach to it.
  createRoot(rootElement).render(
    <StrictMode>
      <AuthProvider>
        <QueryClientProvider client={queryClient}>
          <App />
        </QueryClientProvider>
      </AuthProvider>
    </StrictMode>,
  );
}

function isSilentRenewFrame(): boolean {
  if (!isOidcConfigured || !oidcConfig.silent_redirect_uri) return false;
  return window.location.pathname === new URL(oidcConfig.silent_redirect_uri).pathname;
}
