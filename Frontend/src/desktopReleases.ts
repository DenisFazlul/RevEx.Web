import { keycloak } from './auth'

export type DesktopRelease = {
  backendVersion: string
  recommendedDesktopVersion: string
  minimumDesktopVersion: string
  maximumDesktopVersion: string
  updatesPath: string
  installers: Record<string, string>
  notes: string
}
export type ReleaseRecord = {
  manifest: { version: string; notes: string; installers: Record<string, string> }
  uploadedAt: string
}

const baseUrl = (import.meta.env.VITE_API_URL as string | undefined)?.replace(/\/$/, '') ?? ''
export const desktopFileUrl = (path: string) => `${baseUrl}/${path.replace(/^\/+/, '')}`

async function request<T>(path: string, options?: RequestInit, admin = false, allowMissing = false): Promise<T> {
  const headers = new Headers(options?.headers)
  if (admin) {
    if (!keycloak.authenticated) throw new Error('Войдите под учётной записью администратора.')
    await keycloak.updateToken(30)
    headers.set('Authorization', `Bearer ${keycloak.token}`)
  }
  const response = await fetch(desktopFileUrl(path), { ...options, headers, cache: 'no-store' })
  if (allowMissing && response.status === 404) return null as T
  if (!response.ok) {
    const body = await response.json().catch(() => null)
    throw new Error(body?.message ?? body?.title ?? (response.status === 403 ? 'Недостаточно прав администратора.' : `Ошибка API: ${response.status}`))
  }
  return response.status === 204 ? undefined as T : response.json() as Promise<T>
}

export const desktopReleases = {
  active: () => request<DesktopRelease | null>('api/desktop-release', undefined, false, true),
  list: () => request<ReleaseRecord[]>('api/desktop-releases', undefined, true),
  upload: (file: File) => {
    const data = new FormData()
    data.append('file', file)
    return request<ReleaseRecord>('api/desktop-releases', { method: 'POST', body: data }, true)
  },
  activate: (version: string, minimumDesktopVersion: string, maximumDesktopVersion: string) =>
    request<void>(`api/desktop-releases/${encodeURIComponent(version)}/activate`, {
      method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ version, minimumDesktopVersion, maximumDesktopVersion }),
    }, true),
}
