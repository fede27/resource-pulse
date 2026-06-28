import { defineConfig } from 'orval';

export default defineConfig({
  resourcePulse: {
    input: {
      // Snapshot of the backend spec (refresh with: npm run snapshot:api).
      // Decouples client + MSW mock generation from a running backend / CI.
      target: './openapi/swagger.json',
    },
    output: {
      mode: 'tags-split',
      target: 'src/api/generated/endpoints.ts',
      schemas: 'src/api/generated/schemas',
      client: 'react-query',
      httpClient: 'axios',
      // Emit MSW request handlers (one <tag>.msw.ts per tag) used by the test
      // harness as the always-on baseline server. Never hand-write mock JSON.
      mock: true,
      prettier: true,
      clean: true,
      override: {
        mutator: {
          path: 'src/api/client.ts',
          name: 'apiClient',
        },
      },
    },
  },
});
