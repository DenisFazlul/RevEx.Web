// Run only against the disposable API container described in WEB/docs/DESKTOP_RELEASES.md.
// Credentials stay local: authorization-code + PKCE uses existing development users.
import { readFile } from 'node:fs/promises';
import { randomBytes, randomUUID, createHash } from 'node:crypto';
import assert from 'node:assert/strict';

const api = process.env.REVEX_TEST_API_URL ?? 'http://localhost:18082';
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
const activate = (version, isActive = true, platform) => json(frontendAdmin, { version, isActive, platform });
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

await upload('.windows.zip', '99.0.4 (Windows only)');
await check('/api/desktop-releases/99.0.4/activate', 204, 'Publish Windows independently', activate('99.0.4', true, 'win-x64'));
const targets = await (await check('/api/desktop-installers', 200, 'Public platform-specific installation targets')).json();
assert.equal(targets.targets['win-x64'].version, '99.0.4');
assert.equal(targets.targets['osx-x64'].version, '99.0.1');
assert.equal(targets.targets['osx-arm64'].version, '99.0.1');
assert.ok(targets.installers['win-x64'].includes('/99.0.4/'));
assert.ok(targets.installers['osx-x64'].includes('/99.0.1/'));
await check('/api/categories', 200, 'Windows API accepts independently published version', { headers: { ...user, 'X-RevEx-Desktop-Version': '99.0.4', 'X-RevEx-Desktop-Platform': 'win-x64' } });
await check('/api/categories', 409, 'Windows support does not authorize macOS with same version', { headers: { ...user, 'X-RevEx-Desktop-Version': '99.0.4', 'X-RevEx-Desktop-Platform': 'osx-x64' } });
await check('/api/categories', 409, 'Partial release requires platform header', { headers: { ...user, 'X-RevEx-Desktop-Version': '99.0.4' } });
await check('/api/categories', 409, 'Unknown platform rejected in ordinary API', { headers: { ...user, 'X-RevEx-Desktop-Version': '99.0.4', 'X-RevEx-Desktop-Platform': 'linux-x64' } });
const legacy = await (await check('/api/desktop-release', 200, 'Legacy protocol retains latest common active version')).json();
assert.equal(legacy.recommendedDesktopVersion, '99.0.1');
const windowsLegacy = await (await check('/api/desktop-release?platform=win-x64', 200, 'Explicit platform selects independent target')).json();
assert.equal(windowsLegacy.recommendedDesktopVersion, '99.0.4');
await check('/api/desktop-release?platform=linux-x64', 400, 'Invalid platform cannot select legacy release');
await upload('.mac.zip', '99.0.4 (macOS appended)');
await check('/api/desktop-releases/99.0.4/files/RevEx-win-x64.exe', 200, 'Appending macOS preserves Windows installer');
await check('/api/desktop-releases/99.0.4/files/RevEx-osx-x64.pkg', 404, 'Appended macOS installer is private draft');
await check('/api/desktop-releases/99.0.4/files/releases.osx-x64.json', 404, 'Appended macOS feed is private draft');
await check('/api/desktop-releases/99.0.4/files/RevEx-99.0.4-osx-x64-full.nupkg', 404, 'Appended macOS package is private draft');
const winOffer = await (await check('/api/desktop-updates/check', 200, 'Windows gets independently selected update', json({}, identity))).json();
assert.equal(winOffer.targetVersion, '99.0.4'); assert.equal(winOffer.package.platform, 'win-x64');
const macIdentity = { installationId: randomUUID(), installationKey: randomBytes(32).toString('hex'), platform: 'osx-x64', version: '99.0.1' };
const macCurrent = await (await check('/api/desktop-updates/check', 200, 'macOS keeps its current version before publication', json({}, macIdentity))).json();
assert.equal(macCurrent.decision, 'current');
await check('/api/desktop-releases/99.0.4/activate', 204, 'Publish macOS Intel independently', activate('99.0.4', true, 'osx-x64'));
await check('/api/categories', 200, 'Published macOS version now accepted', { headers: { ...user, 'X-RevEx-Desktop-Version': '99.0.4', 'X-RevEx-Desktop-Platform': 'osx-x64' } });
await check('/api/desktop-releases/99.0.4/files/RevEx-osx-arm64.pkg', 404, 'Publishing Intel does not expose Apple Silicon draft');
await check('/api/desktop-releases/99.0.4/activate', 204, 'Rollback Windows without changing macOS', activate('99.0.4', false, 'win-x64'));
const changed = await (await check('/api/desktop-installers', 200, 'Installer links reflect independent rollback')).json();
assert.equal(changed.targets['win-x64'].version, '99.0.1'); assert.equal(changed.targets['osx-x64'].version, '99.0.4');
await check('/api/categories', 409, 'Rolled-back Windows version rejected despite active macOS', { headers: { ...user, 'X-RevEx-Desktop-Version': '99.0.4', 'X-RevEx-Desktop-Platform': 'win-x64' } });
await check('/api/categories', 200, 'macOS remains supported after Windows rollback', { headers: { ...user, 'X-RevEx-Desktop-Version': '99.0.4', 'X-RevEx-Desktop-Platform': 'osx-x64' } });
await check('/' + winOffer.downloadPath, 200, 'Pinned Windows offer remains downloadable after platform rollback', { headers: { 'X-RevEx-Installation-Key': identity.installationKey } });
await check('/api/desktop-releases/99.0.1/activate', 409, 'Active macOS cannot allow disabling last Windows version', activate('99.0.1', false, 'win-x64'));
console.log('PASS: Independent platform publication, compatibility, downloads and rollback verified over HTTP');
