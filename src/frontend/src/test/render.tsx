import type { ReactElement, ReactNode } from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ConfigProvider, App as AntApp } from 'antd';
import { ThemeProvider } from 'antd-style';
import { I18nextProvider } from 'react-i18next';
import {
  render,
  renderHook,
  type RenderHookOptions,
  type RenderOptions,
} from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import i18n, { antdLocaleFor } from '@/i18n';
import { appTheme, getCustomToken } from '@/app/theme';

// A fresh QueryClient per render: no cross-test cache leakage; retries off so a
// mocked error surfaces immediately instead of being retried.
export function makeTestQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: { retry: false, staleTime: 0, gcTime: 0 },
      mutations: { retry: false },
    },
  });
}

// Mirrors the real provider stack in src/app/App.tsx (minus the router): i18n →
// AntD locale/theme → AntApp (message/modal context) → React Query.
export function AppProviders({
  children,
  queryClient,
}: {
  children: ReactNode;
  queryClient: QueryClient;
}): ReactElement {
  const locale = antdLocaleFor(i18n.resolvedLanguage ?? i18n.language);
  return (
    <I18nextProvider i18n={i18n}>
      <QueryClientProvider client={queryClient}>
        <ConfigProvider locale={locale}>
          <ThemeProvider theme={appTheme} customToken={getCustomToken}>
            <AntApp>{children}</AntApp>
          </ThemeProvider>
        </ConfigProvider>
      </QueryClientProvider>
    </I18nextProvider>
  );
}

export type RenderWithProvidersResult = ReturnType<typeof render> & {
  queryClient: QueryClient;
  user: ReturnType<typeof userEvent.setup>;
};

/** Render a component inside the full app provider stack. */
export function renderWithProviders(
  ui: ReactElement,
  options?: Omit<RenderOptions, 'wrapper'> & { queryClient?: QueryClient },
): RenderWithProvidersResult {
  const queryClient = options?.queryClient ?? makeTestQueryClient();
  const result = render(ui, {
    wrapper: ({ children }) => (
      <AppProviders queryClient={queryClient}>{children}</AppProviders>
    ),
    ...options,
  });
  return { ...result, queryClient, user: userEvent.setup() };
}

/** Render a hook inside the full app provider stack (for query/composition hooks). */
export function renderHookWithProviders<Result, Props>(
  hook: (initialProps: Props) => Result,
  options?: Omit<RenderHookOptions<Props>, 'wrapper'> & { queryClient?: QueryClient },
) {
  const queryClient = options?.queryClient ?? makeTestQueryClient();
  const result = renderHook(hook, {
    wrapper: ({ children }) => (
      <AppProviders queryClient={queryClient}>{children}</AppProviders>
    ),
    ...options,
  });
  return { ...result, queryClient };
}

export * from '@testing-library/react';
export { userEvent };
