// Snapshot the backend OpenAPI spec to openapi/swagger.json.
// Fails loudly (non-zero exit) if the backend is unreachable or errors, so a
// stale/empty snapshot never slips through silently. Override the URL with
// SWAGGER_URL if the backend runs elsewhere.
import { mkdirSync, createWriteStream } from 'node:fs';
import { get } from 'node:http';

const url = process.env.SWAGGER_URL ?? 'http://localhost:5157/swagger/v1/swagger.json';
const out = 'openapi/swagger.json';

const fail = (msg) => {
  console.error(`snapshot:api failed — ${msg}`);
  process.exit(1);
};

mkdirSync('openapi', { recursive: true });

get(url, (res) => {
  if (res.statusCode !== 200) {
    res.resume();
    fail(`${url} returned HTTP ${res.statusCode}`);
    return;
  }
  const file = createWriteStream(out);
  res.pipe(file);
  file.on('finish', () => console.log(`Wrote ${out} from ${url}`));
  file.on('error', (e) => fail(e.message));
}).on('error', (e) => fail(`could not reach ${url} (${e.message})`));
