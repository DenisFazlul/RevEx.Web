# RevEx Keycloak development setup

Keycloak is started by `../docker-compose.yml` on `http://localhost:8081`. The realm
configuration and custom login theme are mounted read-only into the container.

## Files

| Path | Purpose |
| --- | --- |
| `revex-realm.json` | Realm, clients, development users, audience mapper, locale, theme selection. |
| `themes/revex/login/theme.properties` | Theme metadata and parent fallback. |
| `themes/revex/login/login.ftl` | Fully custom login-page markup. |
| `themes/revex/login/resources/css/login.css` | Standalone login-page styles. |
| `themes/revex/login/resources/img/revex-mark.svg` | RevEx icon and SVG favicon. |
| `themes/revex/login/resources/js/login.js` | Password visibility toggle. |

The login page deliberately does not use PatternFly markup. Keep it compact and
dependency-free. Preserve Keycloak form bindings such as `${url.loginAction}`,
`login.username`, `usernameHidden`, realm remember-me settings, and authentication
messages when editing `login.ftl`.

To replace the icon, update `revex-mark.svg` while keeping its path and square
`viewBox`. No template or CSS change should be necessary.

## Realm contract

- Realm: `revex`, default/supported locale: Russian.
- Desktop client: `revex-desktop`, public Authorization Code client with PKCE S256.
- Allowed desktop redirect: `http://127.0.0.1:7890/`.
- API bearer audience: `revex-api`, added by desktop and frontend client audience mappers.
- Local users and effective realm roles:
  - `user` — `user` (read-only catalog access); password from `REVEX_USER_PASSWORD`.
  - `moder` — `moderator`, inherited `user` (content editing); password from
    `REVEX_MODERATOR_PASSWORD`.
  - `admin` — `administrator`, inherited `moderator` and `user` (full access);
    password from `REVEX_ADMIN_PASSWORD`.
- Realm roles are hierarchical: `administrator -> moderator -> user`.
- Desktop and frontend clients emit effective realm roles as the multivalued `roles`
  claim in access and ID tokens. The API uses that claim for authorization policies.

## Apply and verify

Run from `WEB/`:

```bash
cp .env.example .env
# Replace every change-me value in .env before starting the stack.
jq empty Keycloak/revex-realm.json
docker compose config --quiet
docker compose up -d --force-recreate keycloak
docker compose logs --tail=100 keycloak
```

Use `prompt=login` when testing the authorization endpoint; otherwise an existing
browser SSO session may immediately complete authentication and hide the login page.
Check both desktop width and a narrow viewport after CSS changes. Also verify an
invalid login so that the error alert is rendered correctly.

An import does not overwrite an existing realm. For the new frontend release-management
API, apply the audience mapper to the existing local realm (Node.js 22+, credentials
from `.env`; no password/token is printed):

```bash
node Keycloak/configure-frontend-api-audience.mjs
```

This changes only `revex-frontend` audience and role-claim mappers, not user roles, PKCE, grants,
or API authorization. Sign in again afterward. On a fresh deployment the realm import
already contains the mapper. In a customer's existing realm add an audience mapper
with included client audience `revex-api`, access token on and ID token off.
Also mirror existing realm roles into the multivalued `roles` claim (access and ID
tokens) as configured for the desktop client. Do not assign additional user roles.

This is development configuration. Passwords are supplied through the untracked
`WEB/.env` file and substituted into the realm import at startup. The stack uses HTTP
and starts Keycloak in development mode. Use unique values and do not reuse them in
production.

Общая документация стенда, фактические порты и жизненный цикл паролей: [docs/OPERATIONS.md](../docs/OPERATIONS.md).
