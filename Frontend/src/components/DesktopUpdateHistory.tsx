import { useEffect, useState } from 'react'
import { Search } from 'lucide-react'
import { desktopReleases, type DesktopUpdateHistory as History } from '../desktopReleases'

const date = (value?: string | null) => value ? new Date(value).toLocaleString('ru-RU') : '—'
const status: Record<string, string> = { started: 'Передача начата', transferred: 'Пакет передан', partial: 'Часть пакета передана', cancelled: 'Отменено', failed: 'Ошибка', rejected: 'Запрос отклонён' }
const platforms: Record<string, string> = { 'win-x64': 'Windows · x64', 'osx-x64': 'macOS · Intel', 'osx-arm64': 'macOS · Apple Silicon' }
const installation = (id?: string) => id ? id.slice(0, 8) : 'Первая установка'

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
  const matches = (item: { userName?: string; userId?: string; installationId?: string | null }) =>
    `${item.userName ?? ''} ${item.userId ?? ''} ${item.installationId ?? ''}`.toLowerCase().includes(search.trim().toLowerCase())
  return <section className="release-manager release-history">
    <div className="release-toolbar"><label className="release-search"><Search size={18}/><input aria-label="Поиск пользователя или установки" placeholder="Пользователь или идентификатор установки" value={search} onChange={event => setSearch(event.target.value)}/></label><button className="release-button" disabled={busy} onClick={() => void load()}>Обновить</button></div>
    <p className="release-help">Последние 500 записей в каждом списке. Версия — последняя сообщённая клиентом; время последнего обращения показывает, насколько сведения актуальны. Без входа пользователь ещё не определён.</p>
    {busy && <p role="status">Загружаем историю…</p>}
    {error && <p role="alert" className="release-feedback error">{error}</p>}
    {data && <>
      <section className="release-history-card"><h2>Установки пользователей</h2><div className="release-table-wrap"><table className="release-table"><thead><tr><th>Пользователь</th><th>Установка</th><th>ОС / архитектура</th><th>Версия</th><th>Последнее обращение</th></tr></thead><tbody>
        {data.installations.filter(matches).map(item => <tr key={item.installationId}><td>{item.userName ?? 'Вход ещё не выполнен'}</td><td title={item.installationId}>{installation(item.installationId)}</td><td>{platforms[item.platform] ?? item.platform}</td><td>{item.version}</td><td>{date(item.lastSeenAt)}</td></tr>)}
      {!data.installations.filter(matches).length && <tr><td colSpan={5}>Записей нет.</td></tr>}
      </tbody></table></div></section>
      <section className="release-history-card"><h2>Обновления и откаты</h2><div className="release-table-wrap"><table className="release-table"><thead><tr><th>Пользователь / установка</th><th>Переход</th><th>Назначен</th><th>Результат</th><th>Подтверждён</th></tr></thead><tbody>
        {data.updates.filter(matches).map(item => <tr key={item.id}><td>{item.userName ?? 'Не определён'} / {installation(item.installationId)}</td><td>{item.fromVersion} → {item.policy.targetVersion} {item.policy.isDowngrade ? '(откат)' : ''}</td><td>{date(item.offeredAt)}</td><td>{item.confirmedAt ? 'Установлено' : item.failure ? `Ошибка: ${item.failure}` : 'Ожидает подтверждения'}</td><td>{date(item.confirmedAt)}</td></tr>)}
      {!data.updates.filter(matches).length && <tr><td colSpan={5}>Записей нет.</td></tr>}
      </tbody></table></div></section>
      <section className="release-history-card"><h2>Скачивания</h2><p className="release-help">Передача пакета не означает установку. Завершение обновления подтверждает десктоп после перезапуска.</p><div className="release-table-wrap"><table className="release-table"><thead><tr><th>Пользователь / установка</th><th>Версия / файл</th><th>Начало</th><th>Результат</th><th>Завершение</th></tr></thead><tbody>
        {data.downloads.filter(matches).map(item => <tr key={item.id}><td>{item.userName ?? 'Не определён'} / {installation(item.installationId)}</td><td>{item.version}<br/>{item.fileName}</td><td>{date(item.startedAt)}</td><td>{status[item.status] ?? item.status}</td><td>{date(item.finishedAt)}</td></tr>)}
      {!data.downloads.filter(matches).length && <tr><td colSpan={5}>Записей нет.</td></tr>}
      </tbody></table></div></section>
    </>}
  </section>
}
