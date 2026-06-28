// Prepend `// @ts-nocheck` to every orval-generated file. The MSW mock
// factories construct objects with explicit `undefined` optional props that
// don't satisfy `exactOptionalPropertyTypes`. Excluding generated output from
// type-checking (it is machine-generated, never hand-edited, and consumers
// still type-check against the exported signatures) lets ALL hand-written code
// — app and tests — keep the strict TypeScript flags. Run automatically after
// `orval` via the `generate:api` npm script.
import { readdirSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';

const root = 'src/api/generated';
const DIRECTIVE = '// @ts-nocheck';

function walk(dir) {
  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    const full = join(dir, entry.name);
    if (entry.isDirectory()) {
      walk(full);
    } else if (entry.name.endsWith('.ts')) {
      const body = readFileSync(full, 'utf8');
      if (!body.startsWith(DIRECTIVE)) {
        writeFileSync(full, `${DIRECTIVE}\n${body}`);
      }
    }
  }
}

walk(root);
console.log(`Prepended "${DIRECTIVE}" to generated files under ${root}/`);
