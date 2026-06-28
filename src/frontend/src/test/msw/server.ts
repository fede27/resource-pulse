import { setupServer } from 'msw/node';
import { handlers } from './handlers';

// Single MSW server for the whole Node (jsdom) test run. Lifecycle is wired in
// src/test/setup.ts. Tests override per-endpoint behaviour with `server.use(...)`.
export const server = setupServer(...handlers);
