import { withContentType } from '../lib/fileValidation'
import { apiFetch, apiJson } from './client'
import type { DocumentDetails, DocumentStats, DocumentStatus, DocumentSummary, PagedResponse } from './types'

export interface DocumentListParams {
  page: number
  pageSize: number
  status?: DocumentStatus
}

// Query keys: every key starts with 'documents', so one invalidation refreshes lists, stats and details.
export const documentKeys = {
  all: ['documents'] as const,
  list: (params: DocumentListParams) => ['documents', 'list', params] as const,
  stats: ['documents', 'stats'] as const,
  details: (id: string) => ['documents', 'details', id] as const,
}

export function getDocuments({ page, pageSize, status }: DocumentListParams): Promise<PagedResponse<DocumentSummary>> {
  const query = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
  if (status) {
    query.set('status', status)
  }

  return apiJson(`/api/documents?${query}`)
}

export function getDocumentStats(): Promise<DocumentStats> {
  return apiJson('/api/documents/stats')
}

export function getDocument(id: string): Promise<DocumentDetails> {
  return apiJson(`/api/documents/${encodeURIComponent(id)}`)
}

export function uploadDocument(file: File): Promise<DocumentSummary> {
  const form = new FormData()
  form.append('file', withContentType(file))

  return apiJson('/api/documents', { method: 'POST', body: form })
}

export function deleteDocument(id: string): Promise<void> {
  return apiJson(`/api/documents/${encodeURIComponent(id)}`, { method: 'DELETE' })
}

/**
 * Downloads the original file. The endpoint needs the bearer token, which a plain link cannot send,
 * so the file is fetched as a blob and saved through a temporary object URL.
 */
export async function downloadDocument(id: string, fileName: string): Promise<void> {
  const response = await apiFetch(`/api/documents/${encodeURIComponent(id)}/download`)
  const url = URL.createObjectURL(await response.blob())

  try {
    const link = document.createElement('a')
    link.href = url
    link.download = fileName
    link.click()
  } finally {
    // Revoke after the click has been handled, or some browsers cancel the download.
    setTimeout(() => URL.revokeObjectURL(url), 0)
  }
}
