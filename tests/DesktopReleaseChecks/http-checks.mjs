// Run only against the disposable API container described in WEB/DESKTOP_RELEASES.md.
// Credentials stay local: authorization-code + PKCE uses existing development users.
import { readFile } from 'node:fs/promises';
import { randomBytes, randomUUID, createHash } from 'node:crypto';
import assert from 'node:assert/strict';

const api = process.env.REVEX_TEST_API_URL ?? 'http://localhost:18082';
const issuer = 'http://localhost:8081/realms/revex/protocol/openid-connect';
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
  assert.ok(location?.startsWith(redirect), `Login succeeded for ${user}`);
  const code = new URL(location).searchParams.get('code');
  const response = await fetch(issuer + '/token', { method: 'POST', body: new URLSearchParams({
    grant_type: 'authorization_code', client_id: client, redirect_uri: redirect,
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
for (let attempt = 0; attempt < 30; attempt++) {
  try { if ((await fetch(api + '/api/desktop-release')).status === 404) break } catch {}
  await new Promise(resolve => setTimeout(resolve, 500));
}
await check('/api/desktop-release', 404, 'Fresh isolated API has no active release');
await check('/api/desktop-releases', 401, 'Anonymous admin access denied');
const frontendAdmin = { Authorization: 'Bearer ' + await token('admin', env.REVEX_ADMIN_PASSWORD, 'revex-frontend') };
await check('/api/desktop-releases', 200, 'Frontend admin token can call release API', { headers: frontendAdmin });
const admin = { Authorization: 'Bearer ' + await token('admin', env.REVEX_ADMIN_PASSWORD) };
const user = { Authorization: 'Bearer ' + await token('user', env.REVEX_USER_PASSWORD) };
const moderator = { Authorization: 'Bearer ' + await token('moder', env.REVEX_MODERATOR_PASSWORD) };
await check('/api/desktop-releases', 403, 'Frontend user cannot manage releases', {
  headers: { Authorization: 'Bearer ' + await token('user', env.REVEX_USER_PASSWORD, 'revex-frontend') } });
await check('/api/desktop-releases', 403, 'Frontend moderator cannot manage releases', {
  headers: { Authorization: 'Bearer ' + await token('moder', env.REVEX_MODERATOR_PASSWORD, 'revex-frontend') } });
await check('/api/desktop-releases', 403, 'User cannot manage releases', { headers: user });
await check('/api/desktop-releases', 403, 'Moderator cannot manage releases', { headers: moderator });
await check('/api/desktop-releases', 200, 'Administrator can list releases', { headers: admin });

const json = (headers, body) => ({ method: 'POST', headers: { ...headers, 'Content-Type': 'application/json' }, body: JSON.stringify(body) });
const activate = (version, isActive = true) => json(frontendAdmin, { version, isActive });
async function upload(suffix, version) {
  const form = new FormData();
  form.append('file', new Blob([await readFile('/private/tmp/revex-release-fixture.zip' + suffix)]), 'fixture.zip');
  await check('/api/desktop-releases', 200, 'Upload full release ' + version, { method: 'POST', headers: frontendAdmin, body: form });
}
const identity = { installationId: randomUUID(), installationKey: randomBytes(32).toString('hex'), version: '99.0.1', platform: 'win-x64' };
await check('/api/desktop-updates/check', 503, 'No decision without an active release', json({}, identity));
await upload('', '99.0.1');
await check('/api/desktop-releases/99.0.1/files/RevEx-win-x64.exe', 404, 'Draft installer is private');
await check('/api/desktop-releases/99.0.1/activate', 204, 'Publish and enable first version', activate('99.0.1'));
await check('/api/desktop-releases/99.0.1/activate', 409, 'Cannot disable last active version', activate('99.0.1', false));
const active = await (await check('/api/desktop-release', 200, 'Public installation links available')).json();
assert.equal(active.recommendedDesktopVersion, '99.0.1');
assert.equal(active.minimumDesktopVersion, active.recommendedDesktopVersion);
assert.equal(active.maximumDesktopVersion, active.recommendedDesktopVersion);
const installer = await check('/' + active.installers['win-x64'], 206, 'Installer supports Range', { headers: { Range: 'bytes=0-3' } });
assert.equal((await installer.arrayBuffer()).byteLength, 4);
await check('/api/desktop-releases/99.0.1/files/releases.osx-arm64.json', 200, 'Legacy feed retained for migration to new updater');
await check('/api/desktop-releases/99.0.1/files/record.json', 404, 'Private release metadata inaccessible');
await check('/api/categories', 409, 'Unknown desktop version cannot use protected API', { headers: { ...user, 'X-RevEx-Desktop-Version': '1.0.0' } });
await check('/api/categories', 200, 'Active desktop version can use protected API', { headers: { ...user, 'X-RevEx-Desktop-Version': '99.0.1' } });
await check('/api/categories', 200, 'Frontend requests are unaffected', { headers: user });
const current = await (await check('/api/desktop-updates/check', 200, 'Current client gets decision', json({}, identity))).json();
assert.equal(current.decision, 'current'); assert.equal(current.package, null);
await check('/api/desktop-releases/99.0.1', 409, 'Published release cannot be deleted', { method: 'DELETE', headers: frontendAdmin });
await check('/api/desktop-releases/99.0.1', 403, 'User cannot delete releases', { method: 'DELETE', headers: user });
await check('/api/desktop-releases/99.0.1', 403, 'Moderator cannot edit releases', { method: 'PATCH', headers: { ...moderator, 'Content-Type': 'application/json' }, body: JSON.stringify({ notes: 'forbidden' }) });
await check('/api/desktop-releases/99.0.1', 200, 'Administrator edits description', { method: 'PATCH', headers: { ...frontendAdmin, 'Content-Type': 'application/json' }, body: JSON.stringify({ notes: 'Edited via frontend API' }) });
await upload('.second.zip', '99.0.2');
await check('/api/desktop-releases/99.0.2', 204, 'Draft may be moved to trash', { method: 'DELETE', headers: frontendAdmin });
await check('/api/desktop-releases/99.0.2/activate', 400, 'Trashed draft cannot be published', activate('99.0.2'));
await check('/api/desktop-releases/99.0.2/restore', 204, 'Administrator restores draft', { method: 'POST', headers: frontendAdmin });
await check('/api/desktop-releases/99.0.2/activate', 204, 'Publish second version without disabling first', activate('99.0.2'));
const optional = await (await check('/api/desktop-updates/check', 200, 'Supported old client receives offer', json({}, identity))).json();
assert.equal(optional.decision, 'optional'); assert.equal(optional.targetVersion, '99.0.2'); assert.equal(optional.package.platform, 'win-x64');
const packageHeaders = { 'X-RevEx-Installation-Key': identity.installationKey };
await check('/' + optional.downloadPath, 404, 'Package requires correct installation key', { headers: { 'X-RevEx-Installation-Key': 'wrong' } });
const part = await check('/' + optional.downloadPath, 206, 'Tracked package supports Range', { headers: { ...packageHeaders, Range: 'bytes=0-3' } });
assert.equal((await part.arrayBuffer()).byteLength, 4);
await upload('.third.zip', '99.0.3');
await check('/api/desktop-releases/99.0.3/activate', 204, 'Publish third release', activate('99.0.3'));
await check('/api/desktop-releases/99.0.3/activate', 204, 'Disable third to trigger rollback', activate('99.0.3', false));
const rollback = await (await check('/api/desktop-updates/check', 200, 'Disabled newest client gets decision', json({}, { ...identity, version: '99.0.3' }))).json();
assert.equal(rollback.decision, 'required'); assert.equal(rollback.targetVersion, '99.0.2'); assert.equal(rollback.isDowngrade, true);
await check('/api/desktop-releases/99.0.2/activate', 204, 'Change target after issuing offer', activate('99.0.2', false));
const bytes = await (await check('/' + optional.downloadPath, 200, 'Pinned package remains available after target changes', { headers: packageHeaders })).arrayBuffer();
assert.equal(bytes.byteLength, optional.package.size);
assert.equal(createHash('sha256').update(new Uint8Array(bytes)).digest('hex').toLowerCase(), optional.package.sha256.toLowerCase());
await check('/api/desktop-updates/report', 401, 'User attribution requires authentication', json({}, identity));
await check('/api/desktop-updates/report', 401, 'Installation key cannot be replaced using a token', json(user, { ...identity, installationKey: 'D'.repeat(64) }));
await check('/api/desktop-updates/report', 400, 'Wrong installed version cannot confirm an update', json(user, { ...identity, completedOfferId: optional.offerId }));
const completed = { ...identity, version: '99.0.2', completedOfferId: optional.offerId };
await check('/api/desktop-updates/report', 204, 'Authenticated restart confirms installed version even if target changed', json(user, completed));
await check('/api/desktop-updates/report', 204, 'Duplicate confirmation is accepted without a new event', json(user, completed));
await check('/api/desktop-updates/history', 401, 'Anonymous tracking history denied');
await check('/api/desktop-updates/history', 403, 'Ordinary user cannot read tracking history', { headers: user });
const history = await (await check('/api/desktop-updates/history?limit=500', 200, 'Administrator can view installation and update history', { headers: frontendAdmin })).json();
const installed = history.installations.find(item => item.installationId === identity.installationId);
assert.equal(installed.version, '99.0.2'); assert.equal(installed.userName, 'user');
assert.ok(history.updates.find(item => item.id === optional.offerId).confirmedAt);
assert.equal(history.updates.filter(item => item.id === optional.offerId).length, 1);
assert.ok(history.downloads.some(item => item.offerId === optional.offerId && item.status === 'transferred' && item.userName === 'user'));
assert.ok(history.downloads.some(item => item.offerId === optional.offerId && item.status === 'partial'));
assert.ok(!JSON.stringify(history).includes(identity.installationKey));
console.log('PASS: Download and confirmation audit is persisted, attributed and contains no installation secret');
