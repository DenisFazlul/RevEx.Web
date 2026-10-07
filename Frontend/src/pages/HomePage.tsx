import { DesktopDownloads } from '../components/DesktopDownloads'

export function HomePage() {
  return <><section className="page-intro">
    <p className="eyebrow">RevEx</p>
    <h1>Home</h1>
    <p>Добро пожаловать в RevEx. Работайте с материалами в десктопном приложении. Адрес подключения и ссылка на Keycloak находятся на вкладке Admin.</p>
  </section><DesktopDownloads/></>
}
