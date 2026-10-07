import { useEffect, useState } from 'react'
import { initializeAuth, isAdministrator, keycloak } from '../auth'
import { AdminPage } from '../pages/AdminPage'

export function AdminAccess() {
  const [ready, setReady] = useState(false)
  const [error, setError] = useState('')
  const [, refresh] = useState(0)

  useEffect(() => {
    let active = true
    const changed = () => { if (active) refresh(value => value + 1) }
    keycloak.onAuthLogout = changed
    keycloak.onAuthRefreshSuccess = changed
    keycloak.onAuthRefreshError = () => { keycloak.clearToken(); changed() }
    keycloak.onTokenExpired = () => {
      changed()
      void keycloak.updateToken(30).then(changed).catch(() => { keycloak.clearToken(); changed() })
    }
    void initializeAuth().then(() => { if (active) setReady(true) }).catch(() => {
      if (active) { setError('Не удалось подключиться к Keycloak. Обновите страницу и попробуйте снова.'); setReady(true) }
    })
    const timer = window.setInterval(() => {
      if (keycloak.authenticated) void keycloak.updateToken(30).then(changed).catch(() => { keycloak.clearToken(); changed() })
    }, 15000)
    return () => { active = false; window.clearInterval(timer) }
  }, [])

  const login = () => {
    void keycloak.login({ redirectUri: `${window.location.origin}/admin`, prompt: 'login' }).catch(() => setError('Не удалось открыть страницу входа. Попробуйте снова.'))
  }
  const logout = () => {
    void keycloak.logout({ redirectUri: `${window.location.origin}/` }).catch(() => setError('Не удалось выйти. Попробуйте снова.'))
  }

  if (!ready) return <p role="status">Проверяем доступ…</p>
  if (error) return <p role="alert">{error}</p>
  if (!keycloak.authenticated) return <section className="connection-card">
    <h1>Admin</h1><p>Для доступа войдите под учётной записью администратора RevEx.</p>
    <button className="nav-button active" onClick={login}>Войти через Keycloak</button>
  </section>
  if (!isAdministrator()) return <section className="connection-card">
    <h1>Доступ запрещён</h1><p>У пользователя {keycloak.tokenParsed?.preferred_username} нет роли administrator в RevEx.</p>
    <button className="nav-button active" onClick={logout}>Выйти и сменить пользователя</button>
  </section>
  return <><div className="account-bar"><span>{keycloak.tokenParsed?.preferred_username}</span><button className="nav-button" onClick={logout}>Выйти</button></div><AdminPage/></>
}
