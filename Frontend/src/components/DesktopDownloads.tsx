import { useEffect, useState } from 'react'
import { RevitReleases } from './RevitReleases'
import { Download } from 'lucide-react'
import { desktopFileUrl, desktopReleases, type DesktopRelease } from '../desktopReleases'

export const platformNames: Record<string, string> = {
  'win-x64': 'Windows x64', 'osx-x64': 'macOS Intel', 'osx-arm64': 'macOS Apple Silicon',
}

export function DesktopDownloads() {
  const [release, setRelease] = useState<DesktopRelease | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const load = async () => {
    setLoading(true); setError('')
    try { setRelease(await desktopReleases.active()) }
    catch (error) { setError(error instanceof Error ? error.message : 'Не удалось получить сборки.') }
    finally { setLoading(false) }
  }
  useEffect(() => { void load() }, [])
  return <><section className="connection-card desktop-downloads">
    <span className="connection-icon"><Download size={24}/></span>
    <h2>Скачать RevEx Desktop</h2>
    {loading ? <p role="status">Проверяем доступную версию…</p> : error ? <>
      <p role="alert" className="release-error">{error}</p>
      <button className="release-button secondary" onClick={() => void load()}>Повторить</button>
    </> : !release ? <p>Администратор ещё не опубликовал сборку десктопа.</p> : <>
      <p>Доступные версии для вашего сервера:</p>
      <div className="download-links">{Object.entries(platformNames).map(([platform, name]) =>
        release.installers[platform] && <a className="release-button" key={platform} href={desktopFileUrl(release.installers[platform])}><Download size={16}/>{name} · {release.targets?.[platform]?.version ?? release.recommendedDesktopVersion}</a>)}</div>

      <p className="connection-note">Скачайте установщик для своей платформы, установите приложение и укажите адрес API вашего сервера. Пакеты выдаются из внутреннего контура.</p>
    </>}
  </section><RevitReleases/></>
}
