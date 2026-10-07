import { useEffect, useRef, useState } from 'react'
import { Upload } from 'lucide-react'
import { desktopReleases, type DesktopRelease, type ReleaseRecord } from '../desktopReleases'

export function DesktopReleaseAdmin() {
  const [records, setRecords] = useState<ReleaseRecord[]>([])
  const [active, setActive] = useState<DesktopRelease | null>(null)
  const [version, setVersion] = useState('')
  const [minimum, setMinimum] = useState('')
  const [maximum, setMaximum] = useState('')
  const [file, setFile] = useState<File | null>(null)
  const [busy, setBusy] = useState(false)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [message, setMessage] = useState('')
  const input = useRef<HTMLInputElement>(null)
  const choose = (version: string) => { setVersion(version); setMinimum(version); setMaximum(version) }
  const load = async () => {
    const [records, current] = await Promise.all([desktopReleases.list(), desktopReleases.active()])
    setRecords(records); setActive(current)
    return records
  }
  useEffect(() => {
    void load().then(records => { if (records[0]) choose(records[0].manifest.version) })
      .catch(error => setError(error instanceof Error ? error.message : 'Ошибка загрузки списка.'))
      .finally(() => setLoading(false))
  }, [])
  const perform = async (operation: () => Promise<void>) => {
    setBusy(true); setError(''); setMessage('')
    try { await operation() }
    catch (error) { setError(error instanceof Error ? error.message : 'Операция не выполнена.') }
    finally { setBusy(false) }
  }
  const upload = () => perform(async () => {
    if (!file) throw new Error('Выберите ZIP-архив релиза.')
    if (!file.name.toLowerCase().endsWith('.zip')) throw new Error('Нужен ZIP-архив релиза.')
    if (file.size > 1024 ** 3) throw new Error('Максимальный размер архива — 1 ГБ.')
    const record = await desktopReleases.upload(file)
    await load(); choose(record.manifest.version); setFile(null)
    if (input.current) input.current.value = ''
    setMessage(`Сборка ${record.manifest.version} загружена и проверена. Для публикации активируйте её ниже.`)
  })
  const activate = () => perform(async () => {
    await desktopReleases.activate(version, minimum, maximum)
    await load()
    setMessage(`Сборка ${version} активирована. Установщики доступны пользователям; десктоп проверит обновление при следующем запуске.`)
  })
  return <section className="connection-card release-admin">
    <span className="connection-icon"><Upload size={24}/></span>
    <h2>Сборки десктопа</h2>
    <p>Загрузите комплект Windows и macOS. Загрузка не публикует обновление: сначала проверьте сборку, затем активируйте её.</p>
    {active ? <div className="release-active"><strong>Активная версия: {active.recommendedDesktopVersion}</strong><span>Совместимые клиенты: {active.minimumDesktopVersion}–{active.maximumDesktopVersion}</span></div>
      : !loading && <p>Активного релиза пока нет.</p>}
    <form onSubmit={event => { event.preventDefault(); void upload() }}>
      <fieldset disabled={busy || loading}>
        <legend>1. Загрузить новую сборку</legend>
        <label htmlFor="release-archive">ZIP-архив (до 1 ГБ)</label>
        <input ref={input} id="release-archive" type="file" accept=".zip,application/zip" onChange={event => setFile(event.target.files?.[0] ?? null)}/>
        <p className="connection-note">Архив из CI содержит release.json, три установщика, три полных пакета .nupkg и три списка releases.*.json.</p>
        <button className="release-button" disabled={!file} type="submit">Загрузить и проверить</button>
      </fieldset>
    </form>
    <form onSubmit={event => { event.preventDefault(); void activate() }}>
      <fieldset disabled={busy || loading || records.length === 0}>
        <legend>2. Активировать сборку</legend>
        <label htmlFor="release-version">Загруженная версия</label>
        <select id="release-version" value={version} onChange={event => choose(event.target.value)}>
          <option value="" disabled>Выберите версию</option>
          {records.map(record => <option key={record.manifest.version} value={record.manifest.version}>{record.manifest.version}</option>)}
        </select>
        <div className="release-range">
          <label>Минимальная версия клиента<input required pattern="[0-9]+\.[0-9]+\.[0-9]+" value={minimum} onChange={event => setMinimum(event.target.value)}/></label>
          <label>Максимальная версия клиента<input required pattern="[0-9]+\.[0-9]+\.[0-9]+" value={maximum} onChange={event => setMaximum(event.target.value)}/></label>
        </div>
        <p className="connection-note">Клиенты вне диапазона должны обновиться. Для необязательного исправления оставьте прежнюю минимальную версию. Для обязательного укажите новую. Задавайте только протестированный диапазон.</p>
        <button className="release-button" disabled={!version} type="submit">Активировать {version}</button>
      </fieldset>
    </form>
    {busy && <p role="status">Выполняем операцию… Не закрывайте страницу.</p>}
    {loading && <p role="status">Загружаем список сборок…</p>}
    {error && <p role="alert" className="release-error">{error}</p>}
    {message && <p role="status" className="release-success">{message}</p>}
    {!loading && !busy && <button className="release-button secondary" onClick={() => void perform(async () => { await load() })}>Обновить список</button>}
    {records.length > 0 && <ul className="release-list">{records.map(record => <li key={record.manifest.version}>
      <strong>{record.manifest.version}{active?.recommendedDesktopVersion === record.manifest.version ? ' · активна' : ' · загружена'}</strong>
      <span>{new Date(record.uploadedAt).toLocaleString('ru-RU')}</span>
      {record.manifest.notes && <p className="release-notes">{record.manifest.notes}</p>}
    </li>)}</ul>}
  </section>
}
