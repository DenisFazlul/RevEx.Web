import { useEffect, useState } from 'react'
import { desktopFileUrl, revitReleases, type RevitRelease } from '../desktopReleases'

export function RevitReleases({ admin = false }: { admin?: boolean }) {
  const [records, setRecords] = useState<RevitRelease[]>([])
  const [error, setError] = useState('')
  const [message, setMessage] = useState('')
  const [busy, setBusy] = useState(false)
  const [loading, setLoading] = useState(true)
  const load = async () => setRecords(await (admin ? revitReleases.list() : revitReleases.installers()))
  useEffect(() => { void load().catch(e => setError(String(e.message))).finally(() => setLoading(false)) }, [admin])
  const perform = async (action: () => Promise<void>) => {
    setBusy(true); setError(''); setMessage('')
    try { await action(); await load() } catch (e) { setError(e instanceof Error ? e.message : 'Операция не выполнена.') }
    finally { setBusy(false) }
  }
  return <section className="connection-card desktop-downloads">
    <h2>{admin ? 'Сборки RevEx для Revit' : 'Скачать RevEx для Revit'}</h2>
    <p>Отдельная версия для Revit. Установка и обновление выполняются вручную: скачайте и запустите установщик.</p>
    {admin && <form onSubmit={event => {
      event.preventDefault()
      const form = event.currentTarget
      const data = new FormData(form)
      void perform(async () => { await revitReleases.upload(data); form.reset(); setMessage('Сборка добавлена как черновик. Опубликуйте её, чтобы открыть скачивание.') })
    }}><fieldset disabled={busy || loading}>
      <label>Версия RevEx<input name="version" placeholder="1.2.3" pattern="[0-9]+\.[0-9]+\.[0-9]+" required/></label>
      <label>Версия Revit<input name="revitVersion" placeholder="2025" pattern="20[0-9]{2}" required/></label>
      <label>Описание изменений<textarea name="notes" maxLength={12000}/></label>
      <label>Установщик (EXE, MSI или ZIP, до 1 ГБ)<input name="file" type="file" accept=".exe,.msi,.zip" required/></label>
      <button className="release-button" type="submit">Добавить сборку</button>
    </fieldset></form>}
    {loading && <p role="status">Загружаем сборки…</p>}
    {busy && <p role="status">Выполняем операцию…</p>}
    {error && <p role="alert">{error}</p>}
    {message && <p role="status">{message}</p>}
    {!loading && !error && !records.length && <p>{admin ? 'Сборки ещё не добавлены.' : 'Администратор ещё не опубликовал сборки для Revit.'}</p>}
    <ul className="release-files">{records.map(record => <li key={record.id}>
      <strong>Revit {record.revitVersion} · RevEx {record.version}</strong>
      <p>{record.notes}</p>
      {admin && <><span>{record.fileName} · {record.isPublished ? 'Опубликована' : 'Черновик'}</span>
        <button className="release-button secondary" disabled={busy} onClick={() => void perform(async () => { await revitReleases.publish(record.id, !record.isPublished) })}>{record.isPublished ? 'Снять с публикации' : 'Опубликовать'}</button></>}
      {record.isPublished && <a className="release-button" href={desktopFileUrl(`api/revit-releases/${record.id}/download`)}>Скачать для Revit {record.revitVersion}</a>}
    </li>)}</ul>
  </section>
}
