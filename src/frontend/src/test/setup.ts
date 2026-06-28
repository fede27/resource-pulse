import '@testing-library/jest-dom/vitest';
import { afterAll, afterEach, beforeAll, vi } from 'vitest';
import { cleanup } from '@testing-library/react';

import { server } from './msw/server';
import i18n from '@/i18n';

// Force the Italian locale for deterministic assertions. Without this, the
// LanguageDetector picks up jsdom's en-US navigator and t() returns English,
// while the product (and these tests) target the it_IT locale.
beforeAll(async () => {
  await i18n.changeLanguage('it');
});

// ── MSW lifecycle ──────────────────────────────────────────────────────────
// Baseline handlers come from orval mocks. `onUnhandledRequest: 'error'` keeps
// tests honest: any un-mocked call fails loudly instead of hitting the network.
beforeAll(() => server.listen({ onUnhandledRequest: 'error' }));
afterEach(() => {
  cleanup();
  server.resetHandlers();
});
afterAll(() => server.close());

// ── jsdom polyfills required by Ant Design ──────────────────────────────────
// jsdom implements none of these; AntD touches them on render (responsive
// observers, motion, popups). Stub them so component tests don't explode.
if (!window.matchMedia) {
  window.matchMedia = (query: string): MediaQueryList =>
    ({
      matches: false,
      media: query,
      onchange: null,
      addListener: vi.fn(),
      removeListener: vi.fn(),
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      dispatchEvent: vi.fn(),
    }) as unknown as MediaQueryList;
}

class ResizeObserverStub {
  observe(): void {}
  unobserve(): void {}
  disconnect(): void {}
}
globalThis.ResizeObserver ??= ResizeObserverStub as unknown as typeof ResizeObserver;

if (!Element.prototype.scrollTo) {
  Element.prototype.scrollTo = (() => {}) as Element['scrollTo'];
}
