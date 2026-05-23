import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { ConfigProvider, App as AntApp } from 'antd';
import itIT from 'antd/locale/it_IT';
import { QueryClientProvider } from '@tanstack/react-query';
import { ReactQueryDevtools } from '@tanstack/react-query-devtools';
import { RouterProvider } from '@tanstack/react-router';
import dayjs from 'dayjs';
import 'dayjs/locale/it';

import { queryClient } from '@/app/query-client';
import { router } from '@/app/router';
import { appTheme } from '@/app/theme';

dayjs.locale('it');

const rootElement = document.getElementById('root');
if (!rootElement) throw new Error('Root element #root not found');

createRoot(rootElement).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <ConfigProvider theme={appTheme} locale={itIT}>
        <AntApp>
          <RouterProvider router={router} />
          {import.meta.env.DEV && <ReactQueryDevtools />}
        </AntApp>
      </ConfigProvider>
    </QueryClientProvider>
  </StrictMode>,
);
