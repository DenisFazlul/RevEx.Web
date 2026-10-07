import { ArrowDownToLine, CirclePlus, FilePlus2, FileUp, Hash, ImagePlus, LoaderCircle, Pencil, Save, Trash2, X } from 'lucide-react'
import { FormEvent, useEffect, useMemo, useState } from 'react'
import { api, type Category, type Content, type ContentHistoryEvent, type ContentVersion, type Tag, type TagGroup, type VersionStatus } from '../api'
import { EmptyState, PageIntro } from '../components/ui'

type Props = { item: Content; categories: Category[]; tagGroups: TagGroup[]; tags: Tag[]; versions: ContentVersion[]; onChanged: () => Promise<void>; flash: (message: string) => void }
type DetailTab = 'content' | 'tags' | 'versions' | 'history'

export function ContentDetailsPage({ item, categories, tagGroups, tags, versions, onChanged, flash }: Props) {
  const [tab, setTab] = useState<DetailTab>('content')
  const [file, setFile] = useState<File>()
  const [selectedId, setSelectedId] = useState<number>()
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [history, setHistory] = useState<ContentHistoryEvent[]>([])
  const [historyLoading, setHistoryLoading] = useState(false)
  const selected = versions.find(version => version.id === selectedId)
  useEffect(() => { if (selectedId && !versions.some(version => version.id === selectedId)) setSelectedId(undefined) }, [versions, selectedId])
  useEffect(() => {
    if (tab !== 'history') return
    setHistoryLoading(true)
    setError('')
    api.contentHistory(item.id)
      .then(setHistory)
      .catch(loadError => setError(loadError instanceof Error ? loadError.message : 'Не удалось загрузить историю'))
      .finally(() => setHistoryLoading(false))
  }, [tab, item.id])

  const addEmptyVersion = async () => run(async () => { const version = await api.createVersion(item.id); await onChanged(); setSelectedId(version.id); flash('Пустая версия добавлена') })
  const addFileVersion = async () => { if (!file) return; await run(async () => { const version = await api.createVersion(item.id); await api.uploadFile(version.id, file); setFile(undefined); setSelectedId(version.id); await onChanged(); flash('Версия с файлом добавлена') }) }
  const run = async (action: () => Promise<void>) => { setBusy(true); setError(''); try { await action() } catch (actionError) { setError(actionError instanceof Error ? actionError.message : 'Операция не выполнена') } finally { setBusy(false) } }

  return <><PageIntro eyebrow={`Материал #${item.id}`} title={item.name} copy="Редактируйте карточку и управляйте метаданными всех версий материала."/>
    {error && <div className="uk-alert uk-alert-danger detail-alert" role="alert"><X size={17}/>{error}</div>}
    <ul className="detail-tabs uk-tab" role="tablist"><li className={tab === 'content' ? 'uk-active' : ''}><button className="detail-tab-button" onClick={() => setTab('content')}>Карточка контента</button></li><li className={tab === 'tags' ? 'uk-active' : ''}><button className="detail-tab-button" onClick={() => setTab('tags')}>Теги <span className="uk-badge">{item.tagIds.length}</span></button></li><li className={tab === 'versions' ? 'uk-active' : ''}><button className="detail-tab-button" onClick={() => setTab('versions')}>Версии <span className="uk-badge">{versions.length}</span></button></li><li className={tab === 'history' ? 'uk-active' : ''}><button className="detail-tab-button" onClick={() => setTab('history')}>История изменений</button></li></ul>
    {tab === 'content' ? <ContentEditor item={item} categories={categories} onChanged={onChanged} flash={flash} setError={setError}/> : tab === 'tags' ? <TagEditor item={item} tagGroups={tagGroups} tags={tags} onChanged={onChanged} flash={flash} setError={setError}/> : tab === 'history' ? <HistoryGrid history={history} loading={historyLoading}/> : <section className="versions-tab">
      <div className="versions-toolbar"><div className="new-version-box"><label className={file ? 'version-file selected' : 'version-file'}><input type="file" onChange={event => setFile(event.target.files?.[0])}/><FilePlus2 size={22}/><span><strong>{file?.name ?? 'Файл для новой версии'}</strong><small>{file ? `${(file.size / 1024 / 1024).toFixed(2)} МБ` : 'Можно создать и пустую версию'}</small></span></label><button className="primary uk-button uk-button-primary" disabled={!file || busy} onClick={() => void addFileVersion()}>{busy ? <LoaderCircle className="spin" size={18}/> : <FileUp size={18}/>} Загрузить</button></div><button className="secondary uk-button uk-button-default" disabled={busy} onClick={() => void addEmptyVersion()}><CirclePlus size={17}/> Пустая версия</button></div>
      <div className="version-workspace"><div className="version-selector"><p className="eyebrow">Все версии</p>{versions.length ? [...versions].sort((a,b) => b.id - a.id).map(version => <button className={`version-select ${selectedId === version.id ? 'active' : ''}`} key={version.id} onClick={() => setSelectedId(version.id)}><span className={`status-mark ${version.status}`}/><span><strong>{version.name}</strong><small>{formatDate(version.date)} · {statusLabel(version.status)}</small></span></button>) : <EmptyState title="Версий пока нет" copy="Создайте первую версию сверху."/>}</div>
        <div className="version-editor-slot">{selected ? <VersionEditor key={`${selected.id}-${selected.name}-${selected.date}-${selected.status}`} version={selected} busy={busy} setBusy={setBusy} onChanged={onChanged} flash={flash} setError={setError}/> : <div className="editor-placeholder"><Pencil size={28}/><h2>Выберите версию</h2><p>Здесь можно изменить её название, дату и статус.</p></div>}</div>
      </div>
    </section>}
  </>
}

function HistoryGrid({ history, loading }: { history: ContentHistoryEvent[]; loading: boolean }) {
  if (loading) return <section className="history-panel"><div className="history-loading"><LoaderCircle className="spin" size={22}/> Загружаем историю…</div></section>
  return <section className="history-panel"><div className="section-title"><span>↺</span><div><h2>История изменений</h2><p>События контента и его версий</p></div></div>{history.length ? <div className="history-grid" role="table"><div className="history-grid-head" role="row"><span>Время</span><span>Описание</span><span>Автор</span></div>{history.map(event => <div className="history-grid-row" role="row" key={event.id}><time dateTime={event.occurredAt}>{formatDateTime(event.occurredAt)}</time><span>{event.description}</span><strong>{event.author}</strong></div>)}</div> : <EmptyState title="История пока пуста" copy="Изменения контента и версий появятся здесь."/>}</section>
}

function TagEditor({ item, tagGroups, tags, onChanged, flash, setError }: { item: Content; tagGroups: TagGroup[]; tags: Tag[]; onChanged: () => Promise<void>; flash: (message: string) => void; setError: (message: string) => void }) {
  const [selectedIds, setSelectedIds] = useState<number[]>(item.tagIds)
  const [newName, setNewName] = useState('')
  const [newGroupId, setNewGroupId] = useState<number>(tagGroups[0]?.id ?? 0)
  const [editingId, setEditingId] = useState<number>()
  const [editingName, setEditingName] = useState('')
  const [busy, setBusy] = useState(false)
  useEffect(() => setSelectedIds(item.tagIds), [item.tagIds])
  useEffect(() => { if (!tagGroups.some(group => group.id === newGroupId)) setNewGroupId(tagGroups[0]?.id ?? 0) }, [tagGroups, newGroupId])

  const run = async (action: () => Promise<void>) => {
    setBusy(true); setError('')
    try { await action() }
    catch (actionError) { setError(actionError instanceof Error ? actionError.message : 'Операция с тегами не выполнена') }
    finally { setBusy(false) }
  }
  const updateContentTags = (tagIds: number[]) => api.updateContent(item.id, { name: item.name, description: item.description, categoryId: item.categoryId, tagIds })
  const save = async () => run(async () => { await updateContentTags(selectedIds); await onChanged(); flash('Теги контента сохранены') })
  const create = async (event: FormEvent) => { event.preventDefault(); const name = newName.trim(); if (!name || !newGroupId) return; await run(async () => { const created = await api.createTag(name, newGroupId); const nextIds = [...selectedIds, created.id]; await updateContentTags(nextIds); setSelectedIds(nextIds); setNewName(''); await onChanged(); flash('Тег создан и назначен') }) }
  const rename = async (tag: Tag) => { const name = editingName.trim(); if (!name || name === tag.name) { setEditingId(undefined); return } await run(async () => { await api.updateTag(tag.id, name, tag.tagGroupId); setEditingId(undefined); await onChanged(); flash('Тег переименован') }) }
  const remove = async (tag: Tag) => { if (!confirm(`Удалить тег «${tag.name}» из всего каталога?`)) return; await run(async () => { await api.deleteTag(tag.id); setSelectedIds(current => current.filter(id => id !== tag.id)); await onChanged(); flash('Тег удалён') }) }
  const toggle = (tagId: number) => setSelectedIds(current => current.includes(tagId) ? current.filter(id => id !== tagId) : [...current, tagId])

  return <section className="tag-editor"><div className="section-title"><span><Hash size={15}/></span><div><h2>Теги контента</h2><p>Выберите метки, по которым материал можно будет найти</p></div></div>
    <form className="new-tag-form" onSubmit={create}><label className="field"><span>Новый тег</span><input className="uk-input" maxLength={100} value={newName} onChange={event => setNewName(event.target.value)} placeholder="Например, Revit"/></label><label className="field"><span>Группа</span><select className="uk-select" value={newGroupId} onChange={event => setNewGroupId(Number(event.target.value))}>{tagGroups.map(group => <option key={group.id} value={group.id}>{group.name}</option>)}</select></label><button className="secondary uk-button uk-button-default" disabled={busy || !newName.trim() || !newGroupId}><CirclePlus size={17}/> Создать</button></form>
    {tags.length ? <div className="tag-list">{[...tags].sort((a,b) => a.tagGroupName.localeCompare(b.tagGroupName) || a.name.localeCompare(b.name)).map(tag => <div className={selectedIds.includes(tag.id) ? 'tag-row selected' : 'tag-row'} key={tag.id}><div className="tag-choice"><label className="tag-checkbox"><input type="checkbox" checked={selectedIds.includes(tag.id)} onChange={() => toggle(tag.id)}/><span>#</span><span className="sr-only">Назначить тег {tag.name}</span></label>{editingId === tag.id ? <input className="uk-input tag-name-input" autoFocus maxLength={100} value={editingName} onChange={event => setEditingName(event.target.value)} onKeyDown={event => { if (event.key === 'Enter') { event.preventDefault(); void rename(tag) } if (event.key === 'Escape') setEditingId(undefined) }}/>: <strong>{tag.name}</strong>}<small>{tag.tagGroupName}</small></div><div className="row-actions">{editingId === tag.id ? <><button className="icon-button" disabled={busy} onClick={() => void rename(tag)} aria-label="Сохранить название"><Save size={16}/></button><button className="icon-button" disabled={busy} onClick={() => setEditingId(undefined)} aria-label="Отменить"><X size={16}/></button></> : <button className="icon-button" disabled={busy} onClick={() => { setEditingId(tag.id); setEditingName(tag.name) }} aria-label={`Переименовать ${tag.name}`}><Pencil size={16}/></button>}<button className="icon-button danger" disabled={busy} onClick={() => void remove(tag)} aria-label={`Удалить ${tag.name}`}><Trash2 size={16}/></button></div></div>)}</div> : <EmptyState title="Тегов пока нет" copy="Создайте первую метку для этого материала."/>}
    <div className="tag-editor-footer"><span>Выбрано: {selectedIds.length}</span><button className="primary uk-button uk-button-primary" disabled={busy} onClick={() => void save()}>{busy ? <LoaderCircle className="spin" size={17}/> : <Save size={17}/>} Сохранить теги</button></div>
  </section>
}

function ContentEditor({ item, categories, onChanged, flash, setError }: { item: Content; categories: Category[]; onChanged: () => Promise<void>; flash: (message: string) => void; setError: (message: string) => void }) {
  const [name, setName] = useState(item.name); const [description, setDescription] = useState(item.description); const [categoryId, setCategoryId] = useState(String(item.categoryId)); const [preview, setPreview] = useState<File>(); const [saving, setSaving] = useState(false); const [previewBusy, setPreviewBusy] = useState(false)
  const localPreviewUrl = useMemo(() => preview ? URL.createObjectURL(preview) : undefined, [preview])
  useEffect(() => () => { if (localPreviewUrl) URL.revokeObjectURL(localPreviewUrl) }, [localPreviewUrl])
  const save = async (event: FormEvent) => { event.preventDefault(); setSaving(true); setError(''); try { await api.updateContent(item.id, { name, description, categoryId: Number(categoryId) }); await onChanged(); flash('Изменения сохранены') } catch (saveError) { setError(saveError instanceof Error ? saveError.message : 'Не удалось сохранить') } finally { setSaving(false) } }
  const uploadPreview = async () => { if (!preview) return; setPreviewBusy(true); setError(''); try { await api.uploadPreview(item.id, preview); setPreview(undefined); await onChanged(); flash('Превью обновлено') } catch (uploadError) { setError(uploadError instanceof Error ? uploadError.message : 'Не удалось загрузить превью') } finally { setPreviewBusy(false) } }
  const deletePreview = async () => { setPreviewBusy(true); setError(''); try { await api.deletePreview(item.id); await onChanged(); flash('Превью удалено') } catch (deleteError) { setError(deleteError instanceof Error ? deleteError.message : 'Не удалось удалить превью') } finally { setPreviewBusy(false) } }
  const displayedPreview = localPreviewUrl ?? (item.previewFileId ? api.fileUrl(item.previewFileId) : undefined)
  return <form className="form-panel content-editor" onSubmit={save}><div className="section-title"><span><Pencil size={14}/></span><div><h2>Карточка контента</h2><p>Название, описание, изображение и размещение</p></div></div>
    <div className="preview-editor"><div className={displayedPreview ? 'preview-canvas has-image' : 'preview-canvas'}>{displayedPreview ? <img src={displayedPreview} alt="Превью контента"/> : <><ImagePlus size={30}/><span>Превью не добавлено</span></>}</div><div className="preview-controls"><label className="secondary uk-button uk-button-default"><ImagePlus size={17}/> {item.previewFileId ? 'Заменить изображение' : 'Выбрать изображение'}<input type="file" accept="image/png,image/jpeg,image/webp,image/gif" onChange={event => setPreview(event.target.files?.[0])}/></label>{preview && <button type="button" className="primary uk-button uk-button-primary" disabled={previewBusy} onClick={() => void uploadPreview()}>{previewBusy ? <LoaderCircle className="spin" size={17}/> : <FileUp size={17}/>} Загрузить</button>}{item.previewFileId && !preview && <button type="button" className="uk-button uk-button-danger" disabled={previewBusy} onClick={() => void deletePreview()}><Trash2 size={16}/> Удалить</button>}<small>JPEG, PNG, WebP или GIF</small></div></div>
    <label className="field"><span>Название</span><input className="uk-input" required maxLength={200} value={name} onChange={event => setName(event.target.value)}/></label><label className="field"><span>Описание</span><textarea className="uk-input" required maxLength={2000} rows={9} value={description} onChange={event => setDescription(event.target.value)}/><small>{description.length} / 2000</small></label><label className="field"><span>Категория</span><select className="uk-select" required value={categoryId} onChange={event => setCategoryId(event.target.value)}>{categories.map(category => <option key={category.id} value={category.id}>{category.name}</option>)}</select></label><button className="primary uk-button uk-button-primary" disabled={saving}>{saving ? <><LoaderCircle className="spin" size={18}/> Сохраняем…</> : <><Save size={17}/> Сохранить изменения</>}</button></form>
}

function VersionEditor({ version, busy, setBusy, onChanged, flash, setError }: { version: ContentVersion; busy: boolean; setBusy: (busy: boolean) => void; onChanged: () => Promise<void>; flash: (message: string) => void; setError: (message: string) => void }) {
  const [name, setName] = useState(version.name); const [date, setDate] = useState(toDateInput(version.date)); const [status, setStatus] = useState<VersionStatus>(version.status)
  const save = async (event: FormEvent) => { event.preventDefault(); setBusy(true); setError(''); try { await api.updateVersion(version.id, { name, date: new Date(`${date}T12:00:00`).toISOString(), status }); await onChanged(); flash('Версия обновлена') } catch (saveError) { setError(saveError instanceof Error ? saveError.message : 'Не удалось обновить версию') } finally { setBusy(false) } }
  return <form className="version-editor" onSubmit={save}><div className="section-title"><span><Pencil size={14}/></span><div><h2>Редактор версии</h2><p>Версия #{version.id}</p></div></div><label className="field"><span>Название версии</span><input className="uk-input" required maxLength={200} value={name} onChange={event => setName(event.target.value)}/></label><label className="field"><span>Дата</span><input className="uk-input" type="date" required value={date} onChange={event => setDate(event.target.value)}/></label><label className="field"><span>Статус</span><select className="uk-select" value={status} onChange={event => setStatus(event.target.value as VersionStatus)}><option value="draft">Черновик</option><option value="published">Опубликована</option><option value="archived">В архиве</option></select></label><div className="version-editor-actions"><button className="primary uk-button uk-button-primary" disabled={busy}><Save size={17}/> Сохранить</button>{version.fileId && <a className="secondary uk-button uk-button-default" href={api.fileUrl(version.fileId)}><ArrowDownToLine size={16}/> Скачать файл</a>}<button type="button" className="uk-button uk-button-danger version-delete" disabled={busy} onClick={async () => { if (!confirm(`Удалить «${version.name}»?`)) return; setBusy(true); try { await api.deleteVersion(version.id); await onChanged(); flash('Версия удалена') } catch (deleteError) { setError(deleteError instanceof Error ? deleteError.message : 'Не удалось удалить версию') } finally { setBusy(false) } }}><Trash2 size={16}/> Удалить</button></div></form>
}

const toDateInput = (value?: string) => value && !Number.isNaN(new Date(value).getTime()) ? value.slice(0, 10) : new Date().toISOString().slice(0, 10)
const formatDate = (value?: string) => value && !Number.isNaN(new Date(value).getTime()) ? new Intl.DateTimeFormat('ru-RU', { day: '2-digit', month: 'short', year: 'numeric' }).format(new Date(value)) : 'Дата не указана'
const formatDateTime = (value: string) => new Intl.DateTimeFormat('ru-RU', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit', second: '2-digit' }).format(new Date(value))
const statusLabel = (status: VersionStatus) => ({ draft: 'Черновик', published: 'Опубликована', archived: 'В архиве' })[status]
