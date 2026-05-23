import { defineConfig } from 'orval';

export default defineConfig({
  resourcePulse: {
    input: {
      target: 'http://localhost:5157/swagger/v1/swagger.json',
    },
    output: {
      mode: 'tags-split',
      target: 'src/api/generated/endpoints.ts',
      schemas: 'src/api/generated/schemas',
      client: 'react-query',
      httpClient: 'axios',
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
