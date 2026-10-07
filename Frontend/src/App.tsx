import { useEffect, useState } from 'react'
import { AppNavigation, type AppTab, Brand } from './components/ui'
import { HomePage } from './pages/HomePage'
import { AdminAccess } from './components/AdminAccess'

function currentTab(): AppTab {
  return window.location.pathname.replace(/\/$/, '') === '/admin' ? 'admin' : 'home'
}

export default function App() {
  const [tab, setTab] = useState<AppTab>(currentTab)

  useEffect(() => {
    const onPopState = () => setTab(currentTab())
    window.addEventListener('popstate', onPopState)
    return () => window.removeEventListener('popstate', onPopState)
  }, [])

  const navigate = (nextTab: AppTab) => {
    window.history.pushState(null, '', nextTab === 'admin' ? '/admin' : '/')
    setTab(nextTab)
  }

  return <div className="app-shell">
    <header className="topbar">
      <button className="brand-button" onClick={() => navigate('home')} aria-label="На главную"><Brand/></button>
      <AppNavigation active={tab} onChange={navigate}/>
    </header>
    <main>{tab === 'admin' ? <AdminAccess/> : <HomePage/>}</main>
  </div>
}
