import { useEffect, useRef, useState, type ReactNode } from 'react'
import { Plus, RefreshCw, Search, Trash2, X, RotateCcw, CheckCircle2, Package } from 'lucide-react'
import { desktopReleases, type DesktopRelease, type ReleaseRecord } from '../desktopReleases'

const platforms: Record<string, string> = { 'win-x64': 'Windows · x64', 'osx-x64': 'macOS · Intel', 'osx-arm64': 'macOS · Apple Silicon' }
const date = (value: string) => new Date(value).toLocaleString('ru-RU')
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
  const [search, setSearch] = useState('')
  const [filter, setFilter] = useState('available')
  const [file, setFile] = useState<File | null>(null)
  const [dialog, setDialog] = useState<Dialog>(null)
  const [busy, setBusy] = useState(false)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [message, setMessage] = useState('')
  const selected = records.find(record => record.manifest.version === version)
  const isActive = Boolean(selected?.isActive)
  const available = records.filter(record => !record.deletedAt)
  const deleted = records.filter(record => record.deletedAt)
  const visible = records.filter(record => {
    const matches = record.manifest.version.includes(search.trim()) || record.manifest.notes.toLowerCase().includes(search.trim().toLowerCase())
    return matches && (filter === 'trash' ? Boolean(record.deletedAt) : !record.deletedAt &&
      (filter === 'active' ? record.isActive :
        filter === 'staged' ? !record.publishedAt : true))
  })
  const select = (record: ReleaseRecord | undefined, _current = active) => {
    setVersion(record?.manifest.version ?? ''); setNotes(record?.manifest.notes ?? '')
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
    if (!file || !file.name.toLowerCase().endsWith('.zip')) throw new Error('Выберите ZIP-комплект релиза из CI.')
    if (file.size > 1024 ** 3) throw new Error('Максимальный размер — 1 ГБ.')
    const record = await desktopReleases.upload(file)
    const { current } = await load(); setFilter('available'); setSearch(''); select(record, current); setFile(null); setDialog(null)
    setMessage(`Сборка ${record.manifest.version} добавлена и проверена. Она ещё не опубликована.`)
  })
  const save = () => perform(async () => {
    if (!selected || selected.deletedAt) return
    const updated = await desktopReleases.edit(version, notes)
    await load(); setNotes(updated.manifest.notes); setMessage(`Описание версии ${version} сохранено.`)
  })
  const activate = () => perform(async () => {
    await desktopReleases.activate(version, !isActive)
    await load(); setDialog(null); setMessage(`Поддержка версии ${version} ${isActive ? 'отключена' : 'включена'}.`)
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
      <div><span>Целевая версия</span><strong>{active?.recommendedDesktopVersion ?? 'Не выбрана'}</strong><small>{active ? 'Последняя из активных версий' : 'Опубликуйте первую сборку'}</small></div>
      <div><span>Доступные сборки</span><strong>{available.length}</strong><small>Windows и две архитектуры macOS</small></div>
      <div><span>Корзина</span><strong>{deleted.length}</strong><small>Удалённые сборки можно восстановить</small></div>
    </div>
    <div className="release-toolbar">
      <label className="release-search"><Search size={18}/><input aria-label="Поиск сборок" placeholder="Поиск по версии или описанию" value={search} disabled={busy} onChange={event => setSearch(event.target.value)}/></label>
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
              {record.deletedAt ? 'В корзине' : record.isActive ? 'Поддерживается' : record.publishedAt ? 'Отключена' : 'Черновик'}</span></td>
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
              <h3>Поддержка версии</h3>
              <p className="release-help">{selected.publishedAt ? (isActive ? 'Пользователи этой версии могут продолжать работу.' : 'Пользователям этой версии требуется переход на целевой релиз.') : 'Проверенный комплект ещё не опубликован.'}</p>
              <p className="release-help">Целевая версия определяется автоматически: самая новая из активных. Отключение новой версии позволяет откатить пользователей на предыдущую активную.</p>
              <button className="release-button" disabled={busy || (isActive && records.filter(record => record.isActive && !record.deletedAt).length === 1)} onClick={() => open('activate')}>
                {!selected.publishedAt ? 'Опубликовать и включить' : isActive ? 'Отключить поддержку' : 'Включить поддержку'}
              </button>
              {isActive && records.filter(record => record.isActive && !record.deletedAt).length === 1 && <p className="release-help">Сначала включите другую версию, чтобы отключить последнюю активную.</p>}
            </div>
            <div className="release-delete-zone"><p>{selected.publishedAt ? 'Опубликованные файлы сохраняются для истории и ранее выданных обновлений.' : 'Черновик можно переместить в корзину и восстановить.'}</p><button className="release-button danger" disabled={busy || Boolean(selected.publishedAt)} onClick={() => open('delete')}><Trash2 size={17}/>Удалить черновик</button></div>
          </>}
        </> : <div className="release-empty"><Package size={36}/><strong>Выберите версию</strong><p>Нажмите на сборку в списке, чтобы изменить описание, переключить версию или удалить её.</p></div>}
      </section>
    </div>
    {busy && <p role="status" className="release-busy">Выполняем операцию… Не закрывайте страницу.</p>}
    {dialog && <ReleaseDialog title={dialog === 'upload' ? 'Добавить сборку десктопа' : dialog === 'delete' ? `Удалить версию ${version}?` : isActive ? 'Отключить поддержку версии?' : 'Опубликовать и включить версию?'} busy={busy} close={() => setDialog(null)}>
      {dialog === 'upload' ? <form onSubmit={event => { event.preventDefault(); void upload() }}><fieldset disabled={busy}>
        <p>Выберите внутренний ZIP-комплект из GitHub Actions: Windows, macOS Intel и Apple Silicon. Максимум 1 ГБ.</p>
        <label>ZIP-архив релиза<input type="file" accept=".zip,application/zip" onChange={event => setFile(event.target.files?.[0] ?? null)}/></label>
        {file && <p>{file.name} · {(file.size / 1024 ** 2).toFixed(1)} МБ</p>}
        <p className="release-help">После проверки сборка появится в списке. Пользователи увидят её после публикации и включения поддержки.</p>
        <button className="release-button" type="submit" disabled={!file}>Загрузить и проверить</button>
      </fieldset></form> : dialog === 'delete' ? <><p>Сборка {version} будет перемещена в корзину. Файлы сохранятся, восстановление доступно в разделе «Корзина».</p><button className="release-button danger" disabled={busy} onClick={() => void remove()}>Переместить в корзину</button></> : <>
        <p>Поддержка версии <strong>{version}</strong> будет {isActive ? 'отключена' : 'включена'}.</p>
        <p>{isActive ? 'Пользователи этой версии должны будут установить целевой релиз. Если он старше, приложение выполнит откат.' : 'Пользователи этой версии смогут продолжить работу. Самая новая активная версия станет целевой.'}</p>
        <button className="release-button" disabled={busy} onClick={() => void activate()}>Подтвердить</button>
      </>}
      {error && <p role="alert" className="release-feedback error">{error}</p>}
      {busy && <p role="status">Загрузка или обработка… Не закрывайте окно.</p>}
    </ReleaseDialog>}
  </section>
}
