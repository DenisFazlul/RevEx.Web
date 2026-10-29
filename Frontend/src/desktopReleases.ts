import { keycloak } from './auth'

export type DesktopRelease = {
  backendVersion: string
  recommendedDesktopVersion: string
  targets?: Record<string, { version: string; installer: string; notes: string }>
  updatesPath: string
  installers: Record<string, string>
  notes: string
}
export type ReleaseRecord = {
  manifest: { version: string; notes: string; installers: Record<string, string> }
  uploadedAt: string
  publishedAt?: string | null
  isActive: boolean
  platforms?: Record<string, { publishedAt?: string | null; isActive: boolean }>
  deletedAt?: string | null
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
  history: () => request<DesktopUpdateHistory>('api/desktop-updates/history?limit=500', undefined, true),
  active: () => request<DesktopRelease | null>('api/desktop-installers', undefined, false, true),
  list: () => request<ReleaseRecord[]>('api/desktop-releases', undefined, true),
  edit: (version: string, notes: string) => request<ReleaseRecord>(`api/desktop-releases/${encodeURIComponent(version)}`, {
    method: 'PATCH', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ notes }),
  }, true),
  remove: (version: string) => request<void>(`api/desktop-releases/${encodeURIComponent(version)}`, { method: 'DELETE' }, true),
  restore: (version: string) => request<void>(`api/desktop-releases/${encodeURIComponent(version)}/restore`, { method: 'POST' }, true),
  upload: (file: File) => {
    const data = new FormData()
    data.append('file', file)
    return request<ReleaseRecord>('api/desktop-releases', { method: 'POST', body: data }, true)
  },
  activate: (version: string, isActive: boolean, platform?: string) =>
    request<void>(`api/desktop-releases/${encodeURIComponent(version)}/activate`, {
      method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ version, isActive, platform }),
    }, true),
}

export type DesktopInstallation = { installationId: string; platform: string; version: string; lastSeenAt: string; userId?: string; userName?: string }
export type DesktopUpdateEvent = {
  id: string; installationId: string; fromVersion: string; offeredAt: string; confirmedAt?: string;
  userId?: string; userName?: string; failure?: string;
  policy: { targetVersion: string; isDowngrade: boolean; decision: string; package?: { platform: string } | null }
}
export type DesktopDownloadEvent = {
  id: string; offerId?: string; installationId?: string; version: string; fileName: string;
  startedAt: string; finishedAt?: string; status: string; userId?: string; userName?: string
}
export type DesktopUpdateHistory = { installations: DesktopInstallation[]; updates: DesktopUpdateEvent[]; downloads: DesktopDownloadEvent[] }

export type RevitRelease = { id: string; version: string; revitVersion: string; notes: string; fileName: string; uploadedAt: string; isPublished: boolean }
export const revitReleases = {
  list: () => request<RevitRelease[]>('api/revit-releases', undefined, true),
  installers: () => request<RevitRelease[]>('api/revit-releases/installers'),
  upload: (data: FormData) => request<RevitRelease>('api/revit-releases', { method: 'POST', body: data }, true),
  publish: (id: string, isPublished: boolean) => request<void>(`api/revit-releases/${id}/publish`, {
    method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ isPublished }),
  }, true),
}
