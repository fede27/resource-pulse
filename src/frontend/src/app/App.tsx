import { ConfigProvider, App as AntApp } from 'antd';
import { ReactQueryDevtools } from '@tanstack/react-query-devtools';
import { RouterProvider } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';

import { router } from '@/app/router';
import { appTheme } from '@/app/theme';
import { antdLocaleFor } from '@/i18n';

export function App() {
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
