export type Category = { id: number; name: string }
export type TagGroup = { id: number; name: string }
export type Tag = { id: number; name: string; tagGroupId: number; tagGroupName: string }
export type Content = { id: number; name: string; description: string; categoryId: number; previewFileId: number | null; tagIds: number[] }
export type ContentInput = Pick<Content, 'name' | 'description' | 'categoryId'> & { tagIds?: number[] }
export type VersionStatus = 'draft' | 'published' | 'archived'
export type ContentVersion = { id: number; contentId: number; fileId: number | null; name: string; date: string; status: VersionStatus }
export type ContentHistoryEvent = { id: number; contentId: number; occurredAt: string; description: string; author: string }

const baseUrl = (import.meta.env.VITE_API_URL as string | undefined)?.replace(/\/$/, '') ?? ''

async function request<T>(path: string, options?: RequestInit): Promise<T> {
  const response = await fetch(`${baseUrl}${path}`, options)
  if (!response.ok) {
    const body = await response.json().catch(() => null)
    throw new Error(body?.message ?? body?.title ?? `Ошибка API: ${response.status}`)
  }
  if (response.status === 204) return undefined as T
  return response.json() as Promise<T>
}

const json = (method: string, body: unknown): RequestInit => ({
  method,
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify(body),
})

export const api = {
  categories: () => request<Category[]>('/api/categories'),
  createCategory: (name: string) => request<Category>('/api/categories', json('POST', { name })),
  updateCategory: (id: number, name: string) => request<void>(`/api/categories/${id}`, json('PUT', { name })),
  deleteCategory: (id: number) => request<void>(`/api/categories/${id}`, { method: 'DELETE' }),
  tagGroups: () => request<TagGroup[]>('/api/tag-groups'),
  tags: () => request<Tag[]>('/api/tags'),
  createTag: (name: string, tagGroupId: number) => request<Tag>('/api/tags', json('POST', { name, tagGroupId })),
  updateTag: (id: number, name: string, tagGroupId: number) => request<void>(`/api/tags/${id}`, json('PUT', { name, tagGroupId })),
  deleteTag: (id: number) => request<void>(`/api/tags/${id}`, { method: 'DELETE' }),
  content: () => request<Content[]>('/api/content'),
  createContent: (data: ContentInput) => request<Content>('/api/content', json('POST', data)),
  updateContent: (id: number, data: ContentInput) => request<void>(`/api/content/${id}`, json('PUT', data)),
  deleteContent: (id: number) => request<void>(`/api/content/${id}`, { method: 'DELETE' }),
  uploadPreview: (contentId: number, file: File) => {
    const data = new FormData(); data.append('file', file)
    return request<Content>(`/api/content/${contentId}/preview`, { method: 'POST', body: data })
  },
  deletePreview: (contentId: number) => request<void>(`/api/content/${contentId}/preview`, { method: 'DELETE' }),
  contentHistory: (contentId: number) => request<ContentHistoryEvent[]>(`/api/content/${contentId}/history`),
  addContentHistoryEvent: (contentId: number, description: string) => request<ContentHistoryEvent>(`/api/content/${contentId}/history`, json('POST', { description })),
  versions: () => request<ContentVersion[]>('/api/content-versions'),
  createVersion: (contentId: number) => request<ContentVersion>('/api/content-versions', json('POST', { contentId })),
  updateVersion: (id: number, data: Pick<ContentVersion, 'name' | 'date' | 'status'>) => request<void>(`/api/content-versions/${id}`, json('PUT', data)),
  deleteVersion: (id: number) => request<void>(`/api/content-versions/${id}`, { method: 'DELETE' }),
  uploadFile: (versionId: number, file: File) => {
    const data = new FormData(); data.append('file', file)
    return request<ContentVersion>(`/api/content-versions/${versionId}/file`, { method: 'POST', body: data })
  },
  fileUrl: (fileId: number) => `${baseUrl}/api/content-files/${fileId}/content`,
}
