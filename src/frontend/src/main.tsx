import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { ConfigProvider, App as AntApp } from 'antd';
import { QueryClientProvider } from '@tanstack/react-query';
import { ReactQueryDevtools } from '@tanstack/react-query-devtools';
import { RouterProvider } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';

import { queryClient } from '@/app/query-client';
import { router } from '@/app/router';
import { appTheme } from '@/app/theme';

// i18n initialises on import; the type augmentation lives in ./i18n/types.
import '@/i18n';
import '@/i18n/types';
import { antdLocaleFor } from '@/i18n';

function App() {
  const { i18n } = useTranslation();
  const locale = antdLocaleFor(i18n.resolvedLanguage ?? i18n.language);
  return (
    <ConfigProvider theme={appTheme} locale={locale}>
      <AntApp>
        <RouterProvider router={router} />
        {import.meta.env.DEV && <ReactQueryDevtools />}
      </AntApp>
    </ConfigProvider>
  );
}

const rootElement = document.getElementById('root');
if (!rootElement) throw new Error('Root element #root not found');

createRoot(rootElement).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <App />
    </QueryClientProvider>
  </StrictMode>,
);
