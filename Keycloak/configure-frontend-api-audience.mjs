// Apply only the frontend API audience mapper to an existing LOCAL development realm.
// Realm imports do not update an already-created realm on container restart.
import { readFile } from 'node:fs/promises';
import assert from 'node:assert/strict';
const settings = Object.fromEntries((await readFile(new URL('../.env', import.meta.url), 'utf8'))
  .split('\n').filter(line => /^[A-Z_]+=/.test(line)).map(line => {
    const at = line.indexOf('='); return [line.slice(0, at), line.slice(at + 1).trim().replace(/^['"]|['"]$/g, '')];
  }));
const base = 'http://localhost:8081';
const login = await fetch(base + '/realms/master/protocol/openid-connect/token', {
  method: 'POST', body: new URLSearchParams({ grant_type: 'password', client_id: 'admin-cli',
    username: settings.KEYCLOAK_ADMIN_USERNAME, password: settings.KEYCLOAK_ADMIN_PASSWORD }) });
assert.equal(login.status, 200, 'Local Keycloak administrator login failed');
const token = (await login.json()).access_token;
async function request(path, options = {}) {
  const result = await fetch(base + '/admin/realms/revex/' + path, { ...options,
    headers: { Authorization: 'Bearer ' + token, 'Content-Type': 'application/json' } });
  assert.ok(result.ok, 'Keycloak request failed: ' + result.status);
  return result;
}
const clients = await (await request('clients?clientId=revex-frontend')).json();
assert.equal(clients.length, 1, 'Expected exactly one frontend client');
const path = `clients/${clients[0].id}/protocol-mappers/models`;
const mapper = JSON.parse(await readFile(new URL('./revex-realm.json', import.meta.url), 'utf8'))
  .clients.find(client => client.clientId === 'revex-frontend').protocolMappers[0];
const existing = (await (await request(path)).json()).find(item => item.name === mapper.name);
if (existing) {
  assert.equal(existing.protocolMapper, mapper.protocolMapper, 'Unexpected existing mapper type');
  await request(path + '/' + existing.id, { method: 'PUT', body: JSON.stringify({ ...existing, ...mapper }) });
} else await request(path, { method: 'POST', body: JSON.stringify(mapper) });
console.log('Frontend API audience configured. Sign in again to obtain a new token.');
