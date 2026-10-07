import { BookOpen, Home, Settings } from 'lucide-react'

export type AppTab = 'home' | 'admin'

export function Brand() {
  return <span className="brand"><span className="brand-mark"><BookOpen size={19}/></span><span>REVEX</span></span>
}

export function AppNavigation({ active, onChange }: { active: AppTab; onChange: (tab: AppTab) => void }) {
  const items = [
    { id: 'home' as const, label: 'Home', icon: <Home size={17}/> },
    { id: 'admin' as const, label: 'Admin', icon: <Settings size={17}/> },
  ]
  return <nav aria-label="Основная навигация">{items.map(item => <button key={item.id} className={active === item.id ? 'nav-button active' : 'nav-button'} aria-current={active === item.id ? 'page' : undefined} onClick={() => onChange(item.id)}>{item.icon}<span>{item.label}</span></button>)}</nav>
}
