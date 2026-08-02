import axios, {
  type AxiosError,
  type AxiosRequestConfig,
  type InternalAxiosRequestConfig,
} from 'axios';

import { getAccessToken, redirectToSignIn, renewAccessToken } from '@/auth/token-store';

export type ProblemDetails = {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  errors?: Record<string, string[]>;
};

const instance = axios.create({
  baseURL: '/',
  timeout: 30_000,
});

instance.interceptors.request.use((config) => {
  // Undefined when no identity provider is configured (the FakeAuth dev loop),
  // in which case the API authenticates the request without a bearer token.
  const token = getAccessToken();
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

// Requests we have already retried once, so a persistently-401 endpoint cannot
// drive an infinite renew/retry loop.
type RetriedRequestConfig = InternalAxiosRequestConfig & { _authRetried?: boolean };

instance.interceptors.response.use(
  (response) => response,
  async (error: AxiosError<ProblemDetails>) => {
    const original = error.config as RetriedRequestConfig | undefined;

    // 401 means the token is missing, expired or rejected: try one silent renew,
    // replay the request, and fall back to the login redirect if that fails.
    //
    // 403 is deliberately NOT retried. The API returns it when the caller's
    // organization maps to no active tenant (ADR-0029) — a fresh token would
    // carry exactly the same organization, so retrying only hides the problem.
    if (error.response?.status !== 401 || !original || original._authRetried) {
      return Promise.reject(error);
    }

    original._authRetried = true;

    const renewed = await renewAccessToken().catch(() => undefined);
    if (!renewed) {
      redirectToSignIn();
      return Promise.reject(error);
    }

    original.headers.Authorization = `Bearer ${renewed}`;
    return instance(original);
  },
);

// orval-generated code passes `signal: undefined` literally, which collides with
// `exactOptionalPropertyTypes: true` against axios's `signal?: GenericAbortSignal`.
// Loosen the inbound type to accept undefined; cast back when handing to axios.
type GeneratedRequestConfig = Omit<AxiosRequestConfig, 'signal'> & {
  signal?: AbortSignal | undefined;
};

// orval mutator signature: takes the base axios config and optional per-call overrides.
export const apiClient = <T>(
  config: GeneratedRequestConfig,
  options?: AxiosRequestConfig,
): Promise<T> => {
  return instance({ ...config, ...options } as AxiosRequestConfig).then(
    ({ data }) => data as T,
  );
};

export default apiClient;

// Helper type aliases orval consumes via `override.mutator`.
export type ErrorType<E> = AxiosError<E>;
export type BodyType<B> = B;
