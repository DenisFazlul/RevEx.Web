import { ArrowLeft, Check, LoaderCircle, X } from 'lucide-react'
import { useEffect, useMemo, useState } from 'react'
import { api, type Category, type Content, type ContentInput, type ContentVersion, type Tag, type TagGroup } from './api'
import { ApiStatus, AppNavigation, type AppTab, Brand, EmptyState } from './components/ui'
import { AddContentPage, type UploadStatus } from './pages/AddContentPage'
import { CatalogPage } from './pages/CatalogPage'
import { CategoryAdminPage } from './pages/CategoryAdminPage'
import { ContentDetailsPage } from './pages/ContentDetailsPage'

export default function App() {
  const detailId = Number(window.location.pathname.match(/^\/content\/(\d+)\/?$/)?.[1] ?? 0)
  const [tab, setTab] = useState<AppTab>('catalog')
  const [categories, setCategories] = useState<Category[]>([])
  const [tags, setTags] = useState<Tag[]>([])
  const [tagGroups, setTagGroups] = useState<TagGroup[]>([])
  const [content, setContent] = useState<Content[]>([])
  const [versions, setVersions] = useState<ContentVersion[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')

  const refresh = async () => {
    setError('')
    try {
      const [nextCategories, nextTagGroups, nextTags, nextContent, nextVersions] = await Promise.all([api.categories(), api.tagGroups(), api.tags(), api.content(), api.versions()])
      setCategories(nextCategories); setTagGroups(nextTagGroups); setTags(nextTags); setContent(nextContent); setVersions(nextVersions)
    } catch (loadError) { setError(loadError instanceof Error ? loadError.message : 'Не удалось загрузить данные') }
    finally { setLoading(false) }
  }
  useEffect(() => { void refresh() }, [])
  const flash = (message: string) => { setNotice(message); window.setTimeout(() => setNotice(''), 2800) }
  const versionByContent = useMemo(() => { const result = new Map<number, ContentVersion>(); versions.forEach(version => { if (!result.has(version.contentId) || version.id > result.get(version.contentId)!.id) result.set(version.contentId, version) }); return result }, [versions])

  if (detailId) {
    const item = content.find(entry => entry.id === detailId)
    return <div className="app-shell"><header className="topbar detail-topbar"><Brand asLink/><a className="back-link" href="/"><ArrowLeft size={17}/> Вернуться в каталог</a><ApiStatus/></header><main>{notice && <div className="toast"><Check size={17}/>{notice}</div>}{loading ? <Loading label="Загружаем материал…"/> : item ? <ContentDetailsPage item={item} categories={categories} tagGroups={tagGroups} tags={tags} versions={versions.filter(version => version.contentId === detailId)} onChanged={refresh} flash={flash}/> : <EmptyState title="Материал не найден" copy="Возможно, он был удалён." action={<a className="primary uk-button uk-button-primary" href="/">Вернуться в каталог</a>}/>}</main></div>
  }

  const addContent = async (payload: ContentInput, files: File[], onProgress: (index: number, status: UploadStatus) => void) => {
    const created = await api.createContent(payload)
    for (let index = 0; index < files.length; index++) {
      onProgress(index, 'uploading')
      try { const version = await api.createVersion(created.id); await api.uploadFile(version.id, files[index]); onProgress(index, 'done') }
      catch (uploadError) { onProgress(index, 'error'); throw uploadError }
    }
    await refresh(); flash('Материал добавлен в каталог'); setTab('catalog')
  }

  return <div className="app-shell"><header className="topbar"><button className="brand-button" onClick={() => setTab('catalog')} aria-label="На главную"><Brand/></button><AppNavigation active={tab} onChange={setTab}/><ApiStatus/></header><main>
    {error && <div className="alert error"><X size={18}/>{error}<button onClick={() => setError('')}><X size={16}/></button></div>}{notice && <div className="toast"><Check size={17}/>{notice}</div>}
    {loading ? <Loading label="Загружаем библиотеку…"/> : tab === 'catalog' ? <CatalogPage categories={categories} tags={tags} content={content} versionByContent={versionByContent} onAdd={() => setTab('add')} onDelete={async id => { await api.deleteContent(id); await refresh(); flash('Материал удалён') }}/> : tab === 'add' ? <AddContentPage categories={categories} onCategory={() => setTab('categories')} onSubmit={addContent}/> : <CategoryAdminPage categories={categories} content={content} onChanged={refresh} flash={flash}/>} 
  </main></div>
}

function Loading({ label }: { label: string }) { return <div className="loading"><LoaderCircle className="spin"/> {label}</div> }
