import { CirclePlus, ExternalLink, Search, Sparkles, Tag as TagIcon, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { api, type Category, type Content, type ContentVersion, type Tag } from '../api'
import { EmptyState, PageIntro } from '../components/ui'

type Props = { categories: Category[]; tags: Tag[]; content: Content[]; versionByContent: Map<number, ContentVersion>; onAdd: () => void; onDelete: (id: number) => Promise<void> }

export function CatalogPage({ categories, tags, content, versionByContent, onAdd, onDelete }: Props) {
  const [query, setQuery] = useState('')
  const [category, setCategory] = useState<number | 'all'>('all')
  const [selectedTags, setSelectedTags] = useState<number[]>([])
  const categoryFiltered = content.filter(item => category === 'all' || item.categoryId === category)
  const tagFiltered = selectedTags.length
    ? categoryFiltered.filter(item => selectedTags.every(tagId => item.tagIds.includes(tagId)))
    : categoryFiltered
  const filtered = tagFiltered.filter(item => `${item.name} ${item.description}`.toLowerCase().includes(query.toLowerCase()))
  const toggleTag = (tagId: number) => setSelectedTags(current => current.includes(tagId) ? current.filter(id => id !== tagId) : [...current, tagId])
  return <>
    <PageIntro eyebrow="Цифровая библиотека" title="Контент, который стоит сохранить." copy="Управляйте материалами, версиями и файлами в одном аккуратном каталоге." action={<button className="primary uk-button uk-button-primary" onClick={onAdd}><CirclePlus size={18}/> Новый материал</button>}/>
    <section className="catalog-toolbar"><label className="search"><Search size={18}/><input className="uk-input uk-form-blank" value={query} onChange={event => setQuery(event.target.value)} placeholder="Найти по названию или описанию"/></label><div className="chips"><button className={category === 'all' ? 'chip active' : 'chip'} onClick={() => setCategory('all')}>Все <span>{content.length}</span></button>{categories.map(item => <button key={item.id} className={category === item.id ? 'chip active' : 'chip'} onClick={() => setCategory(item.id)}>{item.name}</button>)}</div></section>
    {tags.length > 0 && <section className="tag-filter" aria-label="Фильтр по тегам"><span><TagIcon size={15}/> Теги</span><div className="chips">{tags.map(tag => <button key={tag.id} className={selectedTags.includes(tag.id) ? 'chip tag-chip active' : 'chip tag-chip'} aria-pressed={selectedTags.includes(tag.id)} onClick={() => toggleTag(tag.id)}>#{tag.name}</button>)}{selectedTags.length > 0 && <button className="clear-tags" onClick={() => setSelectedTags([])}>Сбросить</button>}</div></section>}
    {filtered.length ? <section className="content-grid">{filtered.map((item, index) => { const currentCategory = categories.find(categoryItem => categoryItem.id === item.categoryId); const version = versionByContent.get(item.id); const itemTags = tags.filter(tag => item.tagIds.includes(tag.id)); return <article className={`content-card tone-${index % 4}`} key={item.id}><div className={item.previewFileId ? 'card-art has-preview' : 'card-art'}>{item.previewFileId ? <img src={api.fileUrl(item.previewFileId)} alt={`Превью ${item.name}`}/> : <><span className="number">{String(index + 1).padStart(2, '0')}</span><Sparkles size={22}/></>}</div><div className="card-body"><div className="card-meta"><span>{currentCategory?.name ?? 'Без категории'}</span><span>{version ? `v${version.id}` : 'без файла'}</span></div><h2>{item.name}</h2><p>{item.description}</p>{itemTags.length > 0 && <div className="card-tags">{itemTags.map(tag => <span key={tag.id}>#{tag.name}</span>)}</div>}<div className="card-actions"><a className="open-link" href={`/content/${item.id}`} target="_blank" rel="noreferrer"><ExternalLink size={16}/> Открыть</a><button className="icon-button danger" onClick={() => { if (confirm(`Удалить «${item.name}»?`)) void onDelete(item.id) }} aria-label="Удалить"><Trash2 size={17}/></button></div></div></article> })}</section> : <EmptyState title={content.length ? 'Ничего не найдено' : 'Каталог пока пуст'} copy={content.length ? 'Попробуйте изменить поиск, категорию или теги.' : 'Добавьте первый материал — он появится здесь.'} action={!content.length ? <button className="primary uk-button uk-button-primary" onClick={onAdd}>Добавить материал</button> : undefined}/>} 
  </>
}
