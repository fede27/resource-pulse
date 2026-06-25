import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { QueryClientProvider } from '@tanstack/react-query';

import { queryClient } from '@/app/query-client';
import { App } from '@/app/App';

// i18n initialises on import; the type augmentation lives in ./i18n/types.
import '@/i18n';
import '@/i18n/types';

const rootElement = document.getElementById('root');
if (!rootElement) throw new Error('Root element #root not found');

createRoot(rootElement).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <App />
    </QueryClientProvider>
  </StrictMode>,
);
