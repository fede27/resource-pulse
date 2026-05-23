import { AxiosError } from 'axios';
import { App } from 'antd';
import type { ProblemDetails } from '@/api/client';

export type ApiErrorReporter = (error: unknown, fallback?: string) => void;

export function useApiError(): ApiErrorReporter {
  const { message } = App.useApp();

  return (error, fallback = 'Si è verificato un errore') => {
    if (error instanceof AxiosError && error.response?.data) {
      const pd = error.response.data as ProblemDetails;
      if (pd.errors) {
        const flat = Object.entries(pd.errors)
          .flatMap(([field, msgs]) => msgs.map((m) => `${field}: ${m}`))
          .join(' • ');
        message.error(flat || pd.title || fallback);
        return;
      }
      message.error(pd.detail || pd.title || fallback);
      return;
    }
    message.error(fallback);
  };
}
