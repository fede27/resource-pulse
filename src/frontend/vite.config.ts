import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import path from 'node:path';

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      '@': path.resolve(import.meta.dirname, 'src'),
    },
  },
  server: {
    // Aspire's AddNpmApp injects PORT; fall back to 5173 for standalone `npm run dev`.
    port: Number(process.env.PORT) || 5173,
    strictPort: true,
    proxy: {
      '/api': {
        target: 'http://localhost:5157',
        changeOrigin: true,
      },
    },
  },
});
