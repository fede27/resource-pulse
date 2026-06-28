import { describe, it, expect } from 'vitest';
import { AxiosError } from 'axios';
import { screen } from '@testing-library/react';
import { renderWithProviders } from '@/test/render';
import { useApiError } from './errors';

// Harness: clicking the button reports `error` via useApiError. We assert the
// user-visible AntD message text rather than spying on the message instance.
function Reporter({ error, fallback }: { error: unknown; fallback?: string }) {
  const report = useApiError();
  return (
    <button type="button" onClick={() => report(error, fallback)}>
      report
    </button>
  );
}

function axiosWith(data: unknown): AxiosError {
  const err = new AxiosError('request failed');
  // @ts-expect-error minimal response shape for the test
  err.response = { data };
  return err;
}

describe('useApiError', () => {
  it('flattens ValidationProblemDetails field errors into one message', async () => {
    const { user } = renderWithProviders(
      <Reporter
        error={axiosWith({
          title: 'Validation failed',
          errors: { Name: ['is required'], Email: ['is invalid'] },
        })}
      />,
    );
    await user.click(screen.getByText('report'));
    expect(await screen.findByText(/Name: is required/)).toBeInTheDocument();
    expect(screen.getByText(/Email: is invalid/)).toBeInTheDocument();
  });

  it('falls back to ProblemDetails detail/title when there are no field errors', async () => {
    const { user } = renderWithProviders(
      <Reporter error={axiosWith({ detail: 'Resource is locked' })} />,
    );
    await user.click(screen.getByText('report'));
    expect(await screen.findByText('Resource is locked')).toBeInTheDocument();
  });

  it('shows the supplied fallback for a non-Axios error', async () => {
    const { user } = renderWithProviders(
      <Reporter error={new Error('boom')} fallback="Qualcosa è andato storto" />,
    );
    await user.click(screen.getByText('report'));
    expect(await screen.findByText('Qualcosa è andato storto')).toBeInTheDocument();
  });
});
