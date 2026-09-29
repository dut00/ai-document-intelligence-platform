import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useSearchParams } from 'react-router'
import { documentKeys, getDocuments, type DocumentListParams } from '../api/documents'
import type { DocumentStatus } from '../api/types'
import { DocumentTable } from '../components/DocumentTable'
import { ErrorAlert } from '../components/ErrorAlert'
import { Pagination } from '../components/Pagination'
import { Spinner } from '../components/Spinner'

const pageSize = 20

const statusFilters: { value: DocumentStatus | ''; label: string }[] = [
  { value: '', label: 'All statuses' },
  // Processing is never committed on its own, so in-progress documents are the pending ones.
  { value: 'Pending', label: 'In progress' },
  { value: 'Completed', label: 'Completed' },
  { value: 'Failed', label: 'Failed' },
]

export function DocumentsPage() {
  // Page and filter live in the URL, so they survive a reload and the back button.
  const [searchParams, setSearchParams] = useSearchParams()
  const page = Math.max(1, Number(searchParams.get('page')) || 1)
  const status = parseStatus(searchParams.get('status'))

  const params: DocumentListParams = { page, pageSize, status }
  const documents = useQuery({
    queryKey: documentKeys.list(params),
    queryFn: () => getDocuments(params),
    placeholderData: keepPreviousData,
  })

  function show(nextPage: number, nextStatus: DocumentStatus | undefined) {
    const query = new URLSearchParams()
    if (nextStatus) {
      query.set('status', nextStatus)
    }
    if (nextPage > 1) {
      query.set('page', String(nextPage))
    }

    setSearchParams(query)
  }

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-4">
        <h1 className="text-2xl font-bold">Documents</h1>
        <label className="flex items-center gap-2 text-sm text-slate-600">
          Status
          <select
            value={status ?? ''}
            onChange={(event) => show(1, parseStatus(event.target.value))}
            className="rounded-md border border-slate-300 bg-white px-3 py-1.5 text-sm"
          >
            {statusFilters.map((filter) => (
              <option key={filter.label} value={filter.value}>
                {filter.label}
              </option>
            ))}
          </select>
        </label>
      </div>

      <ErrorAlert error={documents.error} />
      {documents.isPending && <Spinner />}
      {documents.data &&
        (documents.data.items.length > 0 ? (
          <>
            <DocumentTable documents={documents.data.items} />
            <Pagination
              page={documents.data.page}
              totalPages={documents.data.totalPages}
              onPageChange={(next) => show(next, status)}
            />
          </>
        ) : (
          <p className="rounded-lg border border-dashed border-slate-300 bg-white p-8 text-center text-sm text-slate-500">
            {page > 1 ? (
              // A stale link or deletions left this page empty.
              <>
                This page is empty.{' '}
                <button type="button" onClick={() => show(1, status)} className="font-medium text-indigo-700 hover:underline">
                  Go to the first page
                </button>
              </>
            ) : (
              'No documents match.'
            )}
          </p>
        ))}
    </div>
  )
}

function parseStatus(value: string | null): DocumentStatus | undefined {
  return statusFilters.find((filter) => filter.value && filter.value === value)?.value || undefined
}
