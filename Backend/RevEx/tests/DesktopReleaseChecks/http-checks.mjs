// Run only against the disposable API container described in WEB/DESKTOP_RELEASES.md.
// Credentials stay local: authorization-code + PKCE uses existing development users.
import { readFile } from 'node:fs/promises';
import { randomBytes, createHash } from 'node:crypto';
import assert from 'node:assert/strict';

const api = 'http://localhost:18080';
const issuer = 'http://localhost:8081/realms/revex/protocol/openid-connect';
const env = Object.fromEntries((await readFile(new URL('../../../../.env', import.meta.url), 'utf8'))
  .split('\n').filter(line => /^[A-Z_]+=/.test(line)).map(line => {
    const at = line.indexOf('='); return [line.slice(0, at), line.slice(at + 1).trim().replace(/^['"]|['"]$/g, '')];
  }));
async function token(user, password) {
  const verifier = randomBytes(32).toString('base64url');
  const auth = new URL(issuer + '/auth');
  auth.search = new URLSearchParams({ client_id: 'revex-desktop', redirect_uri: 'http://127.0.0.1:7890/',
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
  assert.ok(location?.startsWith('http://127.0.0.1:7890/'), `Login succeeded for ${user}`);
  const code = new URL(location).searchParams.get('code');
  const response = await fetch(issuer + '/token', { method: 'POST', body: new URLSearchParams({
    grant_type: 'authorization_code', client_id: 'revex-desktop', redirect_uri: 'http://127.0.0.1:7890/',
    code, code_verifier: verifier }) });
  assert.equal(response.status, 200);
  return (await response.json()).access_token;
}
async function check(path, expected, name, options = {}) {
  const response = await fetch(api + path, options);
  assert.equal(response.status, expected, name);
  console.log('PASS: ' + name);
  return response;
}
await check('/api/desktop-release', 404, 'Fresh isolated API has no active release');
await check('/api/desktop-releases', 401, 'Anonymous admin access denied');
const admin = { Authorization: 'Bearer ' + await token('admin', env.REVEX_ADMIN_PASSWORD) };
const user = { Authorization: 'Bearer ' + await token('user', env.REVEX_USER_PASSWORD) };
const moderator = { Authorization: 'Bearer ' + await token('moder', env.REVEX_MODERATOR_PASSWORD) };
await check('/api/desktop-releases', 403, 'User cannot manage releases', { headers: user });
await check('/api/desktop-releases', 403, 'Moderator cannot manage releases', { headers: moderator });
await check('/api/desktop-releases', 200, 'Administrator can list releases', { headers: admin });
const form = new FormData();
form.append('file', new Blob([await readFile('/private/tmp/revex-release-fixture.zip')]), 'fixture.zip');
await check('/api/desktop-releases', 200, 'Administrator uploads complete release', { method: 'POST', headers: admin, body: form });
await check('/api/desktop-releases/99.0.1/files/RevEx-win-x64.exe', 404, 'Uploaded installer stays private before activation');
const activate = (minimum, maximum) => ({ method: 'POST', headers: { ...admin, 'Content-Type': 'application/json' },
  body: JSON.stringify({ version: '99.0.1', minimumDesktopVersion: minimum, maximumDesktopVersion: maximum }) });
await check('/api/desktop-releases/99.0.1/activate', 400, 'Invalid compatibility policy rejected', activate('99.0.2', '99.0.3'));
await check('/api/desktop-releases/99.0.1/activate', 204, 'Valid release activated', activate('99.0.0', '99.0.1'));
const active = await (await check('/api/desktop-release', 200, 'Public metadata available before login')).json();
assert.equal(active.recommendedDesktopVersion, '99.0.1');
const download = await check('/' + active.installers['win-x64'], 206, 'Public installer supports resumed downloads', { headers: { Range: 'bytes=0-3' } });
assert.equal((await download.arrayBuffer()).byteLength, 4);
await check('/api/desktop-releases/99.0.1/files/releases.osx-arm64.json', 200, 'Platform update feed available');
await check('/api/desktop-releases/99.0.1/files/record.json', 404, 'Private release metadata inaccessible');
await check('/api/categories', 409, 'Unsupported desktop is blocked by API', { headers: { ...user, 'X-RevEx-Desktop-Version': '1.0.0' } });
await check('/api/categories', 200, 'Compatible desktop can use API', { headers: { ...user, 'X-RevEx-Desktop-Version': '99.0.0' } });
await check('/api/categories', 200, 'Frontend requests are unaffected', { headers: user });
