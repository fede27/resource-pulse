/// <reference types="vite/client" />

// OIDC configuration, generated into .env.local by ResourcePulse.DevBootstrap
// (ADR-0029). All optional: when they are absent the SPA runs against the API's
// FakeAuth development scheme. None of them is a secret — a SPA is a public
// client and holds no client secret by design.
interface ImportMetaEnv {
  readonly VITE_OIDC_AUTHORITY?: string;
  readonly VITE_OIDC_CLIENT_ID?: string;
  readonly VITE_OIDC_REDIRECT_URI?: string;
  readonly VITE_OIDC_SILENT_REDIRECT_URI?: string;
  readonly VITE_OIDC_POST_LOGOUT_REDIRECT_URI?: string;
  /** Reserved Zitadel scope that puts the API's project id into the token's `aud`. */
  readonly VITE_OIDC_PROJECT_SCOPE?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
