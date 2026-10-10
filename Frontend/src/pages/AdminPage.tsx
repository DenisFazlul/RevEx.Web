import { ExternalLink, Monitor, Shield } from 'lucide-react'
import { DesktopReleaseAdmin } from '../components/DesktopReleaseAdmin'
import { DesktopUpdateHistory } from '../components/DesktopUpdateHistory'
import { useState } from 'react'

const desktopApiUrl = import.meta.env.VITE_DESKTOP_API_URL || import.meta.env.VITE_API_URL || 'http://localhost:8080/'
const keycloakUrl = import.meta.env.VITE_KEYCLOAK_URL || 'http://localhost:8081/admin/master/console/'

export function AdminPage() {
  const [section, setSection] = useState('releases')
  return <>
    <section className="page-intro admin-intro">
      <p className="eyebrow">Подключение и управление</p>
      <h1>Администрирование</h1>
      <p>Управляйте сборками десктопа, включайте поддержку версий и следите за обновлениями пользователей.</p>
    </section>
    <nav className="admin-sections" aria-label="Разделы администрирования"><button aria-pressed={section === 'releases'} onClick={() => setSection('releases')}>Сборки десктопа</button><button aria-pressed={section === 'history'} onClick={() => setSection('history')}>Установки и обновления</button><button aria-pressed={section === 'connection'} onClick={() => setSection('connection')}>Подключение и пользователи</button></nav>
    {section === 'connection' ? <div className="connection-grid">
      <section className="connection-card">
        <span className="connection-icon"><Monitor size={24}/></span>
        <h2>Подключение десктопа</h2>
        <p>Укажите этот адрес API в настройках подключения десктопного приложения RevEx.</p>
        <a className="address-link" href={desktopApiUrl} target="_blank" rel="noopener noreferrer">{desktopApiUrl}<ExternalLink size={16}/></a>
        <p className="connection-note">Для подключения с другого компьютера используйте сетевой адрес сервера вместо localhost.</p>
      </section>
      <section className="connection-card">
        <span className="connection-icon"><Shield size={24}/></span>
        <h2>Keycloak</h2>
        <p>Консоль администрирования Keycloak: пользователи, роли и настройки авторизации.</p>
        <a className="address-link" href={keycloakUrl} target="_blank" rel="noopener noreferrer">{keycloakUrl}<ExternalLink size={16}/></a>
      </section>
    </div> : section === 'history' ? <DesktopUpdateHistory/> : <DesktopReleaseAdmin/>}
  </>
}
