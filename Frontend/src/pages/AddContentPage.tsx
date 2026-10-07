import { Check, ChevronRight, Clock3, FileUp, LoaderCircle, X } from 'lucide-react'
import { FormEvent, useState } from 'react'
import type { Category, ContentInput } from '../api'
import { PageIntro } from '../components/ui'

export type UploadStatus = 'waiting' | 'uploading' | 'done' | 'error'
type SelectedFile = { file: File; status: UploadStatus }
type Props = { categories: Category[]; onCategory: () => void; onSubmit: (data: ContentInput, files: File[], onProgress: (index: number, status: UploadStatus) => void) => Promise<void> }

export function AddContentPage({ categories, onCategory, onSubmit }: Props) {
  const [name, setName] = useState(''); const [description, setDescription] = useState(''); const [categoryId, setCategoryId] = useState(''); const [files, setFiles] = useState<SelectedFile[]>([]); const [busy, setBusy] = useState(false); const [error, setError] = useState('')
  const updateStatus = (index: number, status: UploadStatus) => setFiles(current => current.map((item, itemIndex) => itemIndex === index ? { ...item, status } : item))
  const submit = async (event: FormEvent) => { event.preventDefault(); setError(''); setBusy(true); try { await onSubmit({ name, description, categoryId: Number(categoryId) }, files.map(item => item.file), updateStatus) } catch (submitError) { setError(submitError instanceof Error ? submitError.message : 'Не удалось сохранить материал') } finally { setBusy(false) } }
  return <>
    <PageIntro eyebrow="Новая публикация" title="Добавьте материал в коллекцию." copy="Основные данные создаются сразу. Каждый файл сохраняется как отдельная версия контента."/>
    <form className="editor-layout" onSubmit={submit}><section className="form-panel"><div className="section-title"><span>01</span><div><h2>Описание</h2><p>Как материал будет выглядеть в каталоге</p></div></div>
      <label className="field"><span>Название</span><input className="uk-input" required maxLength={200} value={name} onChange={event => setName(event.target.value)} placeholder="Например, Brand Guidelines 2026"/></label>
      <label className="field"><span>Описание</span><textarea className="uk-input" required maxLength={2000} value={description} onChange={event => setDescription(event.target.value)} rows={7} placeholder="Коротко расскажите, что находится внутри…"/><small>{description.length} / 2000</small></label>
      <label className="field"><span>Категория</span>{categories.length ? <select className="uk-select" required value={categoryId} onChange={event => setCategoryId(event.target.value)}><option value="">Выберите категорию</option>{categories.map(category => <option value={category.id} key={category.id}>{category.name}</option>)}</select> : <button type="button" className="empty-category" onClick={onCategory}>Сначала создайте категорию <ChevronRight size={17}/></button>}</label>
    </section><aside className="upload-panel"><div className="section-title"><span>02</span><div><h2>Файлы</h2><p>Загружаются по очереди</p></div></div>
      <label className={files.length ? 'dropzone compact has-file' : 'dropzone'}><input type="file" multiple onChange={event => setFiles(Array.from(event.target.files ?? []).map(file => ({ file, status: 'waiting' })))}/><FileUp size={30}/><strong>{files.length ? `Выбрано файлов: ${files.length}` : 'Выберите несколько файлов'}</strong><span>{files.length ? 'Нажмите, чтобы изменить выбор' : 'или перетащите их сюда'}</span></label>
      {files.length > 0 && <div className="upload-queue" aria-live="polite">{files.map((item, index) => <div className={`upload-item ${item.status}`} key={`${item.file.name}-${item.file.lastModified}`}><span className="upload-state">{item.status === 'uploading' ? <LoaderCircle className="spin" size={17}/> : item.status === 'done' ? <Check size={17}/> : item.status === 'error' ? <X size={17}/> : <Clock3 size={17}/>}</span><span className="upload-name"><strong>{item.file.name}</strong><small>{(item.file.size / 1024 / 1024).toFixed(2)} МБ</small></span><span className="upload-label">{item.status === 'uploading' ? 'Загрузка…' : item.status === 'done' ? 'Готово' : item.status === 'error' ? 'Ошибка' : 'В очереди'}</span></div>)}</div>}
      {error && <div className="uk-alert uk-alert-danger uk-padding-small" role="alert">{error}</div>}<button className="primary wide uk-button uk-button-primary uk-width-1-1" disabled={busy || !categories.length}>{busy ? <><LoaderCircle className="spin" size={18}/> Сохраняем…</> : <>Опубликовать <ChevronRight size={18}/></>}</button><p className="form-note">Без файлов материал тоже будет опубликован.</p>
    </aside></form>
  </>
}
