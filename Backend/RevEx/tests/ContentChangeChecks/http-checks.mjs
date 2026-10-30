// Uses the local Compose stack. Creates and deletes its own content. Temporarily stops Kafka and restarts the API.
import { readFile } from 'node:fs/promises';
import { randomBytes, randomUUID, createHash } from 'node:crypto';
import assert from 'node:assert/strict';

const api = process.env.REVEX_TEST_API_URL ?? 'http://localhost:8082';
const issuer = (process.env.REVEX_TEST_ISSUER ?? 'http://localhost:8081/realms/revex') + '/protocol/openid-connect';
const env = Object.fromEntries((await readFile(new URL('../../../../.env', import.meta.url), 'utf8'))
  .split('\n').filter(line => /^[A-Z_]+=/.test(line)).map(line => {
    const at = line.indexOf('='); return [line.slice(0, at), line.slice(at + 1).trim().replace(/^['"]|['"]$/g, '')];
  }));
async function token(user, password, client = 'revex-desktop') {
  const redirect = client === 'revex-desktop' ? 'http://127.0.0.1:7890/' : 'http://localhost:3000/admin';
  const verifier = randomBytes(32).toString('base64url');
  const auth = new URL(issuer + '/auth');
  auth.search = new URLSearchParams({ client_id: client, redirect_uri: redirect,
    response_type: 'code', scope: 'openid', code_challenge_method: 'S256',
    code_challenge: createHash('sha256').update(verifier).digest('base64url') });
  const page = await fetch(auth, { redirect: 'manual' });
  const html = await page.text();
  const action = html.match(/<form[^>]*action="([^"]+)"/i)?.[1].replaceAll('&amp;', '&');
  assert.ok(action, 'Login form found');
  const cookie = page.headers.getSetCookie().map(value => value.split(';')[0]).join('; ');
  const logged = await fetch(action, { method: 'POST', redirect: 'manual', headers: { Cookie: cookie },
    body: new URLSearchParams({ username: user, password, credentialId: '' }) });
  const location = logged.headers.get('location');
  if (!location?.startsWith(redirect)) {
    const loginHtml = await logged.text();
    const loginError = loginHtml.match(/id="(?:input-error|kc-error-message)"[^>]*>([\s\S]*?)<\/span>/)?.[1]?.replace(/<[^>]+>/g, '').trim();
    throw new Error(`Test login failed for ${user}: HTTP ${logged.status}; ${loginError ?? 'no redirect'}`);
  }
  const code = new URL(location).searchParams.get('code');
  const response = await fetch(issuer + '/token', { method: 'POST', body: new URLSearchParams({
    grant_type: 'authorization_code', client_id: client, redirect_uri: redirect,
    code, code_verifier: verifier }) });
  assert.equal(response.status, 200);
  return (await response.json()).access_token;
}
const { execFileSync } = await import('node:child_process');
const { fileURLToPath } = await import('node:url');
const cwd = fileURLToPath(new URL('../../../../', import.meta.url));
function compose(...args) {
  return execFileSync('docker', ['compose', ...args], { cwd, encoding: 'utf8', timeout: 60000, stdio: ['pipe', 'pipe', 'pipe'] }).trim();
}
const delay = ms => new Promise(resolve => setTimeout(resolve, ms));
async function eventually(check, label, attempts = 40) {
  for (let i = 0; i < attempts; i++) {
    let passed = false;
    try { passed = await check(); } catch {}
    if (passed) { console.log('PASS: ' + label); return; }
    await delay(1000);
  }
  throw new Error('Timed out: ' + label);
}
const headers = { Authorization: 'Bearer ' + await token('admin', env.REVEX_ADMIN_PASSWORD), 'Content-Type': 'application/json' };
async function request(path, method = 'GET', body) {
  const response = await fetch(api + path, { method, headers, body: body === undefined ? undefined : JSON.stringify(body) });
  assert.ok(response.ok, `${method} ${path}: ${response.status} ${await response.clone().text()}`);
  return response.status === 204 ? null : response.json();
}
function sql(query) {
  return compose('exec', '-T', 'history-postgres', 'psql', '-U', env.POSTGRES_USER || 'revex', '-d', env.POSTGRES_DB || 'revex_history', '-Atc', query);
}
const topic = env.KAFKA_TOPIC || 'content-changes';
const settings = compose('exec', '-T', 'kafka', '/opt/kafka/bin/kafka-configs.sh', '--bootstrap-server', 'kafka:9092', '--entity-type', 'topics', '--entity-name', topic, '--describe');
assert.ok(settings.includes('retention.ms=' + (env.KAFKA_RETENTION_MS || '864000000')));
assert.ok(settings.includes('cleanup.policy=delete'));
assert.ok(settings.includes('retention.bytes=-1'));
console.log('PASS: effective topic retention and delete policy');
assert.equal((await fetch(api + '/api/content')).status, 401);
console.log('PASS: anonymous access still denied');
const categories = await request('/api/categories');
const marker = 'Kafka integration ' + randomUUID();
let content;
let kafkaStopped = false;
try {
  content = await request('/api/content', 'POST', { name: marker, description: 'Created by Kafka integration check', categoryId: categories[0].id });
  const pending = () => Number(sql(`SELECT count(*) FROM content_change_outbox o JOIN content_history_events h ON h.id=o.event_id WHERE h.content_id=${content.id}`));
  await eventually(() => pending() === 0, 'creation delivered and outbox acknowledged');
  await request('/api/content/' + content.id, 'PUT', { name: marker, description: 'Updated with Kafka online', categoryId: categories[0].id });
  await eventually(() => pending() === 0, 'content update delivered');
  compose('stop', 'kafka'); kafkaStopped = true;
  const queued = await request(`/api/content/${content.id}/history`, 'POST', { description: marker + ' outage' });
  assert.equal(pending(), 1);
  console.log('PASS: history request succeeds during outage; event retained in outbox');
  compose('restart', 'revex-api');
  await eventually(async () => { try { return (await fetch(api + '/api/content', { headers })).ok; } catch { return false; } }, 'API starts while Kafka is unavailable');
  assert.equal(pending(), 1);
  console.log('PASS: pending event survives API restart');
  compose('start', 'kafka'); kafkaStopped = false;
  await eventually(() => pending() === 0, 'pending event delivered after Kafka recovery', 60);
  const history = await request(`/api/content/${content.id}/history`);
  assert.equal(history.length, 3);
  let output;
  const args = ['exec', '-T', 'kafka', '/opt/kafka/bin/kafka-console-consumer.sh', '--bootstrap-server', 'kafka:9092', '--topic', topic, '--partition', '0', '--from-beginning', '--timeout-ms', '15000'];
  try { output = compose(...args); } catch (e) { output = e.stdout?.toString() || ''; }
  const events = output.split(/\r?\n/).filter(line => line.startsWith('{')).map(line => JSON.parse(line)).filter(event => event.contentId === content.id);
  for (const entry of history) {
    assert.deepEqual(events.find(event => event.id === entry.id), entry);
  }
  assert.ok(events.some(event => event.id === queued.id));
  console.log('PASS: consumed Kafka JSON matches all three database history entries, including recovered event');
  compose('restart', 'kafka');
  await eventually(() => compose('exec', '-T', 'kafka', '/opt/kafka/bin/kafka-topics.sh', '--bootstrap-server', 'kafka:9092', '--list').split(/\r?\n/).includes(topic), 'topic persists across broker restart', 30);
  let persisted;
  try { persisted = compose(...args); } catch (e) { persisted = e.stdout?.toString() || ''; }
  const recovered = persisted.split(/\r?\n/).filter(line => line.startsWith('{')).map(line => JSON.parse(line));
  for (const entry of history) assert.deepEqual(recovered.find(event => event.id === entry.id), entry);
  console.log('PASS: event data persists across broker restart');
} finally {
  if (kafkaStopped) compose('start', 'kafka');
  if (content) {
    await eventually(async () => { try { return (await fetch(api + '/api/content', { headers })).ok; } catch { return false; } }, 'API available for cleanup');
    await request('/api/content/' + content.id, 'DELETE');
    console.log('PASS: test content deleted (audit history retained)');
  }
}
