import { BookOpen, CirclePlus, FolderCog, LayoutGrid } from 'lucide-react'
import type { ReactNode } from 'react'

export type AppTab = 'catalog' | 'add' | 'categories'

export function Brand({ asLink = false }: { asLink?: boolean }) {
  const content = <><span className="brand-mark"><BookOpen size={19}/></span><span>REVEX</span></>
  return asLink ? <a className="brand" href="/">{content}</a> : <span className="brand">{content}</span>
}

export function AppNavigation({ active, onChange }: { active: AppTab; onChange: (tab: AppTab) => void }) {
  const items = [
    { id: 'catalog' as const, label: 'Каталог', icon: <LayoutGrid size={17}/> },
    { id: 'add' as const, label: 'Добавить', icon: <CirclePlus size={17}/> },
    { id: 'categories' as const, label: 'Категории', icon: <FolderCog size={17}/> },
  ]
  return <nav aria-label="Основная навигация">{items.map(item => <button key={item.id} className={active === item.id ? 'nav-button active' : 'nav-button'} onClick={() => onChange(item.id)}>{item.icon}<span>{item.label}</span></button>)}</nav>
}

export function ApiStatus() { return <div className="status-dot"><span/> API</div> }

export function PageIntro({ eyebrow, title, copy, action }: { eyebrow: string; title: string; copy: string; action?: ReactNode }) {
  return <section className="page-intro"><div><p className="eyebrow">{eyebrow}</p><h1>{title}</h1><p>{copy}</p></div>{action}</section>
}

export function EmptyState({ title, copy, action }: { title: string; copy: string; action?: ReactNode }) {
  return <section className="empty"><BookOpen size={28}/><h2>{title}</h2><p>{copy}</p>{action}</section>
}
