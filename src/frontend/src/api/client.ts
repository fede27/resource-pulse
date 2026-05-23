import axios, { type AxiosError, type AxiosRequestConfig } from 'axios';

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
  // Auth slot — currently no-op.
  // When IDP is wired, attach Bearer token from auth context here:
  // const token = getAuthToken(); if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

instance.interceptors.response.use(
  (response) => response,
  (error: AxiosError<ProblemDetails>) => Promise.reject(error),
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
