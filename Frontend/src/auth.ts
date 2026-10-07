import Keycloak from 'keycloak-js'

export const keycloak = new Keycloak({
  url: import.meta.env.VITE_KEYCLOAK_BASE_URL || 'http://localhost:8081',
  realm: 'revex',
  clientId: 'revex-frontend',
})

// One initialization promise also supports React StrictMode's repeated effects.
let initialization: Promise<boolean> | undefined
export function initializeAuth() {
  return initialization ??= keycloak.init({ pkceMethod: 'S256', checkLoginIframe: false })
}

export function isAdministrator() {
  return Boolean(keycloak.authenticated && !keycloak.isTokenExpired() && keycloak.hasRealmRole('administrator'))
}
