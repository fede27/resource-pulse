import { ConfigProvider, App as AntApp } from 'antd';
import { ThemeProvider } from 'antd-style';
import { ReactQueryDevtools } from '@tanstack/react-query-devtools';
import { RouterProvider } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';

import { router } from '@/app/router';
import { appTheme, getCustomToken } from '@/app/theme';
import { antdLocaleFor } from '@/i18n';

export function App() {
  const { i18n } = useTranslation();
  const locale = antdLocaleFor(i18n.resolvedLanguage ?? i18n.language);
  // antd-style's ThemeProvider owns the AntD theme (tier 1) and injects our
  // custom tokens so `createStyles` can read them. The outer ConfigProvider
  // supplies only the locale, which the nested provider inherits.
  return (
    <ConfigProvider locale={locale}>
      <ThemeProvider theme={appTheme} customToken={getCustomToken}>
        <AntApp>
          <RouterProvider router={router} />
          {import.meta.env.DEV && <ReactQueryDevtools />}
        </AntApp>
      </ThemeProvider>
    </ConfigProvider>
  );
}
