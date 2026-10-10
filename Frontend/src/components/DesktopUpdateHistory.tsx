import { useEffect, useState } from 'react'
import { Search } from 'lucide-react'
import { desktopReleases, type DesktopUpdateHistory as History } from '../desktopReleases'

const date = (value?: string | null) => value ? new Date(value).toLocaleString('ru-RU') : '—'
const status: Record<string, string> = { started: 'Передача начата', transferred: 'Пакет передан', partial: 'Часть пакета передана', cancelled: 'Отменено', failed: 'Ошибка', rejected: 'Запрос отклонён' }
const platforms: Record<string, string> = { 'win-x64': 'Windows · x64', 'osx-x64': 'macOS · Intel', 'osx-arm64': 'macOS · Apple Silicon' }
type EventRow = { id: string; userName?: string; userId?: string; installationId?: string | null; status: string; at?: string; platform: string; detail?: string }

function historyRows(data: History): EventRow[] {
  const installations = new Map(data.installations.map(item => [item.installationId, item]))
  const offers = new Map(data.updates.map(item => [item.id, item]))
  const rows: EventRow[] = data.downloads.map(item => ({
    ...item, id: `download-${item.id}`,
    status: `${status[item.status] ?? item.status} · ${item.version}`,
    at: item.finishedAt ?? item.startedAt,
    platform: (item.offerId ? offers.get(item.offerId)?.policy.package?.platform : undefined)
      ?? item.fileName.match(/win-x64|osx-x64|osx-arm64/)?.[0]
      ?? (item.installationId ? installations.get(item.installationId)?.platform : undefined) ?? '',
    detail: item.fileName,
  }))
  for (const item of data.updates) {
    const platform = item.policy.package?.platform ?? installations.get(item.installationId)?.platform ?? ''
    const transition = `${item.fromVersion} → ${item.policy.targetVersion}`
    rows.push({ ...item, id: `offer-${item.id}`, platform, at: item.offeredAt,
      status: `${item.policy.isDowngrade ? 'Предложен откат' : 'Предложено обновление'} · ${transition}` })
    if (item.confirmedAt) rows.push({ ...item, id: `confirmed-${item.id}`, platform, at: item.confirmedAt,
      status: `${item.policy.isDowngrade ? 'Откат выполнен' : 'Обновление установлено'} · ${transition}` })
    else if (item.failure) rows.push({ ...item, id: `failure-${item.id}`, platform, at: undefined,
      status: `Ошибка обновления · ${transition}`, detail: item.failure })
  }
  return rows.sort((a, b) => (b.at ? Date.parse(b.at) : 0) - (a.at ? Date.parse(a.at) : 0))
}

export function DesktopUpdateHistory() {
  const [data, setData] = useState<History | null>(null)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const [search, setSearch] = useState('')
  const load = async () => {
    setBusy(true); setError('')
    try { setData(await desktopReleases.history()) }
    catch (error) { setError(error instanceof Error ? error.message : 'Не удалось загрузить историю.') }
    finally { setBusy(false) }
  }
  useEffect(() => { void load() }, [])
  const query = search.trim().toLowerCase()
  const rows = data ? historyRows(data).filter(item =>
    `${item.userName ?? ''} ${item.userId ?? ''} ${item.installationId ?? ''} ${item.status} ${platforms[item.platform] ?? item.platform}`.toLowerCase().includes(query)) : []
  return <section className="release-manager release-history">
    <div className="release-toolbar"><label className="release-search"><Search size={18}/><input aria-label="Поиск событий" placeholder="Пользователь, статус или архитектура" value={search} onChange={event => setSearch(event.target.value)}/></label><button className="release-button" disabled={busy} onClick={() => void load()}>Обновить</button></div>
    <p className="release-help">Скачивания, обновления и откаты в общей ленте, новые события сверху. Загружаются последние 500 скачиваний и назначений обновлений. Передача пакета не означает установку: её подтверждает десктоп после перезапуска.</p>
    {busy && <p role="status">Загружаем историю…</p>}
    {error && <p role="alert" className="release-feedback error">{error}</p>}
    {data && <section className="release-history-card"><h2>Учёт загрузок</h2><div className="release-table-wrap"><table className="release-table"><thead><tr><th>Пользователь</th><th>Статус события</th><th>Дата события</th><th>Архитектура</th></tr></thead><tbody>
      {rows.map(item => <tr key={item.id}><td title={item.installationId ?? undefined}>{item.userName ?? item.userId ?? 'Не определён'}</td><td title={item.detail}>{item.status}</td><td title={item.at ? undefined : 'Время ошибки не сохранено в истории'}>{date(item.at)}</td><td>{platforms[item.platform] ?? (item.platform || 'Не определена')}</td></tr>)}
      {!rows.length && <tr><td colSpan={4}>{query ? 'События не найдены.' : 'Событий пока нет.'}</td></tr>}
    </tbody></table></div></section>}
  </section>
}
