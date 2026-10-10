import { useEffect, useRef, useState, type ReactNode } from 'react'
import { Plus, RefreshCw, Search, Trash2, X, RotateCcw, CheckCircle2, Package } from 'lucide-react'
import { desktopReleases, type DesktopRelease, type ReleaseRecord } from '../desktopReleases'

const platforms: Record<string, string> = { 'win-x64': 'Windows · x64', 'osx-x64': 'macOS · Intel', 'osx-arm64': 'macOS · Apple Silicon' }
const date = (value: string) => new Date(value).toLocaleString('ru-RU')
const platformState = (record: ReleaseRecord, platform: string) => record.platforms?.[platform]
  ?? { publishedAt: record.publishedAt, isActive: record.isActive }
const hasDrafts = (record: ReleaseRecord) => Object.keys(record.manifest.installers).some(p => !platformState(record, p).publishedAt)
type Dialog = 'upload' | 'delete' | 'activate' | null

function ReleaseDialog({ title, busy, close, children }: { title: string; busy: boolean; close: () => void; children: ReactNode }) {
  const ref = useRef<HTMLDivElement>(null)
  useEffect(() => {
    const previous = document.activeElement as HTMLElement | null
    const overflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    ref.current?.querySelector<HTMLElement>('button, input, textarea')?.focus()
    return () => { document.body.style.overflow = overflow; if (previous?.isConnected) previous.focus() }
  }, [])
  return <div className="release-overlay" onClick={event => { if (event.target === event.currentTarget && !busy) close() }}>
    <div ref={ref} role="dialog" aria-modal="true" aria-labelledby="release-dialog-title" className="release-dialog" onKeyDown={event => {
      if (event.key === 'Escape' && !busy) close()
      if (event.key !== 'Tab') return
      const items = Array.from(ref.current?.querySelectorAll<HTMLElement>('button:not(:disabled), input:not(:disabled), textarea:not(:disabled), select:not(:disabled)') ?? [])
        .filter(element => element.getClientRects().length > 0)
      if (!items.length) { event.preventDefault(); return }
      if (event.shiftKey && document.activeElement === items[0]) { event.preventDefault(); items.at(-1)?.focus() }
      else if (!event.shiftKey && document.activeElement === items.at(-1)) { event.preventDefault(); items[0]?.focus() }
    }}>
      <div className="release-dialog-heading"><h2 id="release-dialog-title">{title}</h2><button disabled={busy} className="release-icon-button" aria-label="Закрыть окно" onClick={close}><X size={20}/></button></div>
      {children}
    </div>
  </div>
}

export function DesktopReleaseAdmin() {
  const [records, setRecords] = useState<ReleaseRecord[]>([])
  const [active, setActive] = useState<DesktopRelease | null>(null)
  const [version, setVersion] = useState('')
  const [notes, setNotes] = useState('')
  const [platform, setPlatform] = useState('win-x64')
  const [platformFilter, setPlatformFilter] = useState('all')
  const [search, setSearch] = useState('')
  const [filter, setFilter] = useState('available')
  const [file, setFile] = useState<File | null>(null)
  const [dialog, setDialog] = useState<Dialog>(null)
  const [busy, setBusy] = useState(false)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [message, setMessage] = useState('')
  const selected = records.find(record => record.manifest.version === version)
  const state = selected ? platformState(selected, platform) : undefined
  const isActive = Boolean(state?.isActive)
  const isLastActive = isActive && !records.some(record => record.manifest.version !== version && !record.deletedAt && record.manifest.installers[platform] && platformState(record, platform).isActive)
  const available = records.filter(record => !record.deletedAt)
  const deleted = records.filter(record => record.deletedAt)
  const visible = records.filter(record => {
    const matches = record.manifest.version.includes(search.trim()) || record.manifest.notes.toLowerCase().includes(search.trim().toLowerCase())
    return matches && (platformFilter === 'all' || Boolean(record.manifest.installers[platformFilter])) && (filter === 'trash' ? Boolean(record.deletedAt) : !record.deletedAt &&
      (filter === 'active' ? (platformFilter === 'all' ? record.isActive : platformState(record, platformFilter).isActive) :
        filter === 'staged' ? (platformFilter === 'all' ? hasDrafts(record) : !platformState(record, platformFilter).publishedAt) : true))
  })
  const select = (record: ReleaseRecord | undefined, _current = active) => {
    setVersion(record?.manifest.version ?? ''); setNotes(record?.manifest.notes ?? '')
    setPlatform(Object.keys(record?.manifest.installers ?? {})[0] ?? 'win-x64')
  }
  const load = async () => {
    const [items, current] = await Promise.all([desktopReleases.list(), desktopReleases.active()])
    setRecords(items); setActive(current)
    return { items, current }
  }
  useEffect(() => {
    let mounted = true
    void load().then(({ items, current }) => { if (mounted) select(items.find(item => item.manifest.version === current?.recommendedDesktopVersion) ?? items.find(item => !item.deletedAt), current) })
      .catch(error => { if (mounted) setError(error instanceof Error ? error.message : 'Не удалось загрузить сборки.') })
      .finally(() => { if (mounted) setLoading(false) })
    return () => { mounted = false }
  }, [])
  const perform = async (operation: () => Promise<void>) => {
    if (busy) return
    setBusy(true); setError(''); setMessage('')
    try { await operation() }
    catch (error) { setError(error instanceof Error ? error.message : 'Операция не выполнена.') }
    finally { setBusy(false) }
  }
  const open = (value: Dialog) => { setError(''); setMessage(''); setDialog(value); if (value === 'upload') setFile(null) }
  const upload = () => perform(async () => {
    if (!file || !file.name.toLowerCase().endsWith('.zip')) throw new Error('Выберите ZIP-комплект для Windows или macOS из CI.')
    if (file.size > 1024 ** 3) throw new Error('Максимальный размер — 1 ГБ.')
    const record = await desktopReleases.upload(file)
    const { current } = await load(); setFilter('available'); setSearch(''); select(record, current); setFile(null); setDialog(null)
    setMessage(`Пакет версии ${record.manifest.version} добавлен и проверен. Новые платформы загружены как черновики; опубликуйте их отдельно.`)
  })
  const save = () => perform(async () => {
    if (!selected || selected.deletedAt) return
    const updated = await desktopReleases.edit(version, notes)
    await load(); setNotes(updated.manifest.notes); setMessage(`Описание версии ${version} сохранено.`)
  })
  const activate = () => perform(async () => {
    await desktopReleases.activate(version, !isActive, platform)
    await load(); setDialog(null); setMessage(`Поддержка ${platforms[platform]} ${version} ${isActive ? 'отключена' : 'включена'}.`)
  })
  const remove = () => perform(async () => {
    await desktopReleases.remove(version)
    const { items, current } = await load(); select(items.find(item => !item.deletedAt), current); setDialog(null)
    setMessage(`Версия ${version} перемещена в корзину. Файлы сохранены, сборку можно восстановить.`)
  })
  const restore = () => perform(async () => {
    await desktopReleases.restore(version)
    const { items, current } = await load(); setFilter('available'); setSearch(''); select(items.find(item => item.manifest.version === version), current)
    setMessage(`Версия ${version} восстановлена. Для публикации выберите «Опубликовать и включить».`)
  })
  return <section className="release-manager" aria-label="Управление сборками десктопа">
    <div className="release-summary">
      <div><span>Целевые версии</span>{active?.targets ? Object.entries(active.targets).map(([p, target]) => <small key={p}>{platforms[p]} · <b>{target.version}</b></small>) : <strong>Не выбраны</strong>}<small>Последняя активная для каждой платформы</small></div>
      <div><span>Доступные версии</span><strong>{available.length}</strong><small>Windows и macOS выпускаются отдельно</small></div>
      <div><span>Корзина</span><strong>{deleted.length}</strong><small>Удалённые сборки можно восстановить</small></div>
    </div>
    <div className="release-toolbar">
      <label className="release-search"><Search size={18}/><input aria-label="Поиск сборок" placeholder="Поиск по версии или описанию" value={search} disabled={busy} onChange={event => setSearch(event.target.value)}/></label>
      <select aria-label="Платформа сборок" value={platformFilter} disabled={busy} onChange={event => { setPlatformFilter(event.target.value); select(undefined) }}>
        <option value="all">Все платформы</option>{Object.entries(platforms).map(([p, name]) => <option key={p} value={p}>{name}</option>)}
      </select>
      <select aria-label="Статус сборок" value={filter} disabled={busy} onChange={event => { setFilter(event.target.value); select(undefined) }}>
        <option value="available">Все доступные</option><option value="active">Поддерживаются</option><option value="staged">Черновики</option><option value="trash">Корзина</option>
      </select>
      <button className="release-icon-button" aria-label="Обновить список сборок" disabled={busy || loading} onClick={() => void perform(async () => { const { items, current } = await load(); select(items.find(item => item.manifest.version === version), current) })}><RefreshCw size={19}/></button>
      <button className="release-button" disabled={busy || loading} onClick={() => open('upload')}><Plus size={18}/>Добавить сборку</button>
    </div>
    {loading && <p role="status">Загружаем сборки…</p>}
    {error && !dialog && <p role="alert" className="release-feedback error">{error}</p>}
    {message && <p role="status" className="release-feedback success">{message}</p>}
    <div className="release-workspace" aria-busy={busy || loading}>
      <section className="release-catalog"><h2>Версии <span>{visible.length}</span></h2>
        <div className="release-table-wrap"><table className="release-table"><thead><tr><th>Версия и описание</th><th>Статус</th></tr></thead><tbody>
          {visible.map(record => <tr key={record.manifest.version} className={version === record.manifest.version ? 'selected' : ''}>
            <td><button disabled={busy} className="release-row-select" aria-pressed={version === record.manifest.version} onClick={() => select(record)}>
              <strong><Package size={16}/>{record.manifest.version}</strong><span>{record.manifest.notes || 'Без описания'}</span><small>{date(record.uploadedAt)}</small>
            </button></td>
            <td><span className={`release-badge ${record.deletedAt ? 'trash' : record.isActive ? 'active' : ''}`}>
              {record.deletedAt ? 'В корзине' : record.isActive ? (hasDrafts(record) ? 'Поддерживается · есть черновик' : 'Поддерживается') : hasDrafts(record) ? 'Черновик' : 'Отключена'}</span>
              <div className="release-platform-statuses">{Object.keys(record.manifest.installers).map(p => <small key={p}>{platforms[p]} · {platformState(record, p).isActive ? 'активна' : platformState(record, p).publishedAt ? 'отключена' : 'черновик'}</small>)}</div></td>
          </tr>)}
        </tbody></table></div>
        {!loading && !visible.length && <div className="release-empty"><Package size={32}/><strong>{search ? 'Сборки не найдены' : filter === 'trash' ? 'Корзина пуста' : 'Нет сборок в этом разделе'}</strong><p>{search ? 'Измените поиск или фильтр.' : 'Добавьте ZIP-комплект или выберите другой статус.'}</p></div>}
      </section>
      <section className="release-editor" aria-label="Редактор выбранной версии">
        {selected ? <>
          <div className="release-editor-heading"><div><span className="eyebrow">Выбранная сборка</span><h2>{version}</h2></div>{isActive && <CheckCircle2 size={24} aria-label="Активная версия"/>}</div>
          <fieldset disabled={busy || Boolean(selected.deletedAt)}>
            <label>Номер версии<input value={version} readOnly/></label>
            <p className="release-help">Номер зашит в установщики. Для другого номера загрузите новую сборку; здесь можно изменить описание и используемую версию.</p>
            <form onSubmit={event => { event.preventDefault(); void save() }}>
              <label>Описание изменений<textarea rows={5} maxLength={12000} value={notes} onChange={event => setNotes(event.target.value)}/></label>
              <button className="release-button secondary" type="submit" disabled={notes === selected.manifest.notes}>Сохранить описание</button>
            </form>
          </fieldset>
          <h3>Установщики</h3><ul className="release-files">{Object.entries(selected.manifest.installers).map(([platform, name]) => <li key={platform}><strong>{platforms[platform] ?? platform}</strong><span>{name}</span></li>)}</ul>
          {selected.deletedAt ? <div className="release-trash-notice"><p>В корзине с {date(selected.deletedAt)}. Файлы сохранены, публикация отключена.</p><button disabled={busy} className="release-button" onClick={() => void restore()}><RotateCcw size={18}/>Восстановить сборку</button></div> : <>
            <div className="release-policy-form">
              <h3>Поддержка по платформам</h3>
              <label>Платформа<select aria-label="Платформа для публикации" value={platform} disabled={busy} onChange={event => setPlatform(event.target.value)}>{Object.keys(selected.manifest.installers).map(p => <option key={p} value={p}>{platforms[p]}</option>)}</select></label>
              <p className="release-help">{state?.publishedAt ? (isActive ? 'Пользователи этой версии могут продолжать работу.' : 'Пользователям этой версии требуется переход на целевой релиз.') : 'Сборка для выбранной платформы ещё не опубликована.'}</p>
              <p className="release-help">Цель определяется отдельно для каждой платформы: самая новая активная версия. Отключение Windows не влияет на macOS; откат тоже выполняется отдельно.</p>
              <button className="release-button" disabled={busy || isLastActive} onClick={() => open('activate')}>
                {!state?.publishedAt ? 'Опубликовать и включить' : isActive ? 'Отключить поддержку' : 'Включить поддержку'}
              </button>
              {isLastActive && <p className="release-help">Сначала включите другую версию для этой платформы.</p>}
            </div>
            <div className="release-delete-zone"><p>{selected.publishedAt ? 'Опубликованные файлы сохраняются для истории и ранее выданных обновлений.' : 'Черновик можно переместить в корзину и восстановить.'}</p><button className="release-button danger" disabled={busy || Boolean(selected.publishedAt)} onClick={() => open('delete')}><Trash2 size={17}/>Удалить черновик</button></div>
          </>}
        </> : <div className="release-empty"><Package size={36}/><strong>Выберите версию</strong><p>Нажмите на сборку в списке, чтобы изменить описание, переключить версию или удалить её.</p></div>}
      </section>
    </div>
    {busy && <p role="status" className="release-busy">Выполняем операцию… Не закрывайте страницу.</p>}
    {dialog && <ReleaseDialog title={dialog === 'upload' ? 'Добавить сборку десктопа' : dialog === 'delete' ? `Удалить версию ${version}?` : isActive ? 'Отключить поддержку версии?' : 'Опубликовать и включить версию?'} busy={busy} close={() => setDialog(null)}>
      {dialog === 'upload' ? <form onSubmit={event => { event.preventDefault(); void upload() }}><fieldset disabled={busy}>
        <p>Выберите внутренний ZIP-комплект для Windows или macOS. Можно загрузить одну архитектуру macOS или обе вместе. Максимум 1 ГБ.</p>
        <label>ZIP-архив релиза<input type="file" accept=".zip,application/zip" onChange={event => setFile(event.target.files?.[0] ?? null)}/></label>
        {file && <p>{file.name} · {(file.size / 1024 ** 2).toFixed(1)} МБ</p>}
        <p className="release-help">Платформы определяются из пакета. Сборку другой платформы можно добавить к той же версии позже. Уже загруженные пакеты не заменяются. Новые сборки публикуются отдельно.</p>
        <button className="release-button" type="submit" disabled={!file}>Загрузить и проверить</button>
      </fieldset></form> : dialog === 'delete' ? <><p>Сборка {version} будет перемещена в корзину. Файлы сохранятся, восстановление доступно в разделе «Корзина».</p><button className="release-button danger" disabled={busy} onClick={() => void remove()}>Переместить в корзину</button></> : <>
        <p>Поддержка <strong>{platforms[platform]} · {version}</strong> будет {isActive ? 'отключена' : 'включена'}.</p>
        <p>{isActive ? 'Пользователи этой платформы должны будут установить её целевую версию. Если она старше, приложение предложит откат.' : 'Пользователи этой платформы смогут продолжить работу. Самая новая активная версия для неё станет целевой.'}</p>
        <button className="release-button" disabled={busy} onClick={() => void activate()}>Подтвердить</button>
      </>}
      {error && <p role="alert" className="release-feedback error">{error}</p>}
      {busy && <p role="status">Загрузка или обработка… Не закрывайте окно.</p>}
    </ReleaseDialog>}
  </section>
}
