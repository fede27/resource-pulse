import { AxiosError } from 'axios';
import { App } from 'antd';
import { useTranslation } from 'react-i18next';
import type { ProblemDetails } from '@/api/client';
import { AuthProblemType } from '@/auth/problems';

export type ApiErrorReporter = (error: unknown, fallback?: string) => void;

export function useApiError(): ApiErrorReporter {
  const { message } = App.useApp();
  const { t } = useTranslation();

  return (error, fallback = 'Si è verificato un errore') => {
    if (error instanceof AxiosError && error.response?.data) {
      const pd = error.response.data as ProblemDetails;

      // An insufficient role is an expected outcome, not a server complaint, so
      // it gets our own localized sentence rather than the API's English detail.
      // The other 403 — an unmapped organization — deliberately keeps the
      // server's wording: it is an operator problem and the detail is the clue.
      if (pd.type === AuthProblemType.insufficientRole) {
        message.warning(t('access.insufficientRole'));
        return;
      }

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
