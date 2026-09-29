import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { ApiError } from '../api/client'
import { deleteDocument, documentKeys, downloadDocument, getDocument } from '../api/documents'
import type { DocumentDetails } from '../api/types'
import { AnalysisSections } from '../components/AnalysisSections'
import { ErrorAlert } from '../components/ErrorAlert'
import { Spinner } from '../components/Spinner'
import { StatusBadge } from '../components/StatusBadge'
import { isInProgress } from '../lib/documentStatus'
import { formatDateTime, formatFileSize } from '../lib/format'

export function DocumentDetailsPage() {
  const { id = '' } = useParams()
  const details = useQuery({ queryKey: documentKeys.details(id), queryFn: () => getDocument(id) })

  if (details.isPending) {
    return <Spinner />
  }

  // Another user's document is a 404 too: the API does not reveal that it exists.
  if (details.error instanceof ApiError && details.error.status === 404) {
    return (
      <div className="space-y-3">
        <h1 className="text-2xl font-bold">Document not found</h1>
        <Link to="/documents" className="text-sm font-medium text-indigo-700 hover:underline">
          Back to documents
        </Link>
      </div>
    )
  }

  if (details.error) {
    return <ErrorAlert error={details.error} />
  }

  return <DocumentView document={details.data} />
}

function DocumentView({ document }: { document: DocumentDetails }) {
  const queryClient = useQueryClient()
  const navigate = useNavigate()

  const download = useMutation({ mutationFn: () => downloadDocument(document.id, document.fileName) })
  const remove = useMutation({
    mutationFn: () => deleteDocument(document.id),
    onSuccess: async () => {
      queryClient.removeQueries({ queryKey: documentKeys.details(document.id) })
      await queryClient.invalidateQueries({ queryKey: documentKeys.all })
      await navigate('/documents', { replace: true })
    },
  })

  function confirmDelete() {
    if (window.confirm(`Delete "${document.fileName}"? This cannot be undone.`)) {
      remove.mutate()
    }
  }

  const buttonClass =
    'flex items-center gap-2 rounded-md border px-3 py-1.5 text-sm font-medium disabled:cursor-not-allowed disabled:opacity-50'

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div className="min-w-0">
          <Link to="/documents" className="text-sm text-slate-500 hover:underline">
            ← Documents
          </Link>
          <h1 className="mt-1 truncate text-2xl font-bold">{document.fileName}</h1>
        </div>
        <div className="flex gap-2">
          <button
            type="button"
            onClick={() => download.mutate()}
            disabled={download.isPending}
            className={`${buttonClass} border-slate-300 bg-white text-slate-700 hover:bg-slate-50`}
          >
            {download.isPending && <Spinner />}
            Download
          </button>
          <button
            type="button"
            onClick={confirmDelete}
            disabled={remove.isPending}
            className={`${buttonClass} border-red-200 bg-white text-red-700 hover:bg-red-50`}
          >
            {remove.isPending && <Spinner />}
            Delete
          </button>
        </div>
      </div>

      <ErrorAlert error={download.error ?? remove.error} />

      <dl className="grid grid-cols-2 gap-4 rounded-lg border border-slate-200 bg-white p-5 text-sm md:grid-cols-4">
        <Field label="Status">
          <StatusBadge status={document.status} />
        </Field>
        <Field label="Type">{document.analysis?.documentType ?? '—'}</Field>
        <Field label="Size">{formatFileSize(document.sizeBytes)}</Field>
        <Field label="Uploaded">{formatDateTime(document.uploadedAt)}</Field>
        {document.processedAt && <Field label="Processed">{formatDateTime(document.processedAt)}</Field>}
        {document.analysis && <Field label="Model">{document.analysis.model}</Field>}
      </dl>

      {isInProgress(document.status) && (
        <div className="flex items-center gap-3 rounded-lg border border-amber-200 bg-amber-50 p-5 text-sm text-amber-900">
          <Spinner />
          The document is being analyzed. This page updates as soon as the analysis is ready.
        </div>
      )}

      {document.status === 'Failed' && (
        <div role="alert" className="rounded-lg border border-red-200 bg-red-50 p-5 text-sm text-red-800">
          <p className="font-semibold">Processing failed</p>
          <p className="mt-1">{document.failureReason ?? 'Unknown reason.'}</p>
        </div>
      )}

      {document.analysis && <AnalysisSections analysis={document.analysis} />}
    </div>
  )
}

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="min-w-0">
      <dt className="text-xs text-slate-500">{label}</dt>
      <dd className="mt-1 truncate font-medium">{children}</dd>
    </div>
  )
}
