import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';
import path from 'node:path';

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      '@': path.resolve(import.meta.dirname, 'src'),
    },
  },
  test: {
    environment: 'jsdom',
    globals: false,
    setupFiles: ['./src/test/setup.ts'],
    include: ['src/**/*.{test,spec}.{ts,tsx}'],
    css: false,
    // jsdom + Ant Design + v8 coverage instrumentation is heavy; the default 5s
    // is too tight for multi-card page renders under full parallel load.
    testTimeout: 15_000,
    hookTimeout: 20_000,
    coverage: {
      provider: 'v8',
      reporter: ['text', 'text-summary', 'html'],
      // Measure hand-written application code only.
      include: ['src/**/*.{ts,tsx}'],
      exclude: [
        'src/api/generated/**', // orval client + schemas + *.msw.ts mocks
        'src/main.tsx', // app entrypoint
        'src/app/router.tsx', // router bootstrap
        'src/app/routes/**', // thin route shims (forward to features/)
        'src/app/theme.ts', // design tokens (data)
        'src/i18n/locales/**', // translation data
        'src/i18n/types.ts', // type augmentation
        'src/**/*.styles.ts', // createStyles CSS-in-JS — presentational, no logic
        'src/**/*.d.ts',
        'src/**/*.{test,spec}.{ts,tsx}',
        'src/test/**', // test harness (render/providers/msw/setup)
      ],
      // FLOOR, not a target. Hand-written code must stay above this.
      thresholds: {
        lines: 65,
        branches: 65,
      },
    },
  },
});
