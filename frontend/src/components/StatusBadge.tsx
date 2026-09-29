import type { DocumentStatus } from '../api/types'
import { isInProgress } from '../lib/documentStatus'
import { Spinner } from './Spinner'

// Processing is never committed on its own (the Worker saves the final status in the same
// transaction), so for the user a pending document is simply in progress.
const styles: Record<DocumentStatus, { label: string; className: string }> = {
  Pending: { label: 'In progress', className: 'bg-amber-50 text-amber-800 ring-amber-200' },
  Processing: { label: 'In progress', className: 'bg-amber-50 text-amber-800 ring-amber-200' },
  Completed: { label: 'Completed', className: 'bg-emerald-50 text-emerald-800 ring-emerald-200' },
  Failed: { label: 'Failed', className: 'bg-red-50 text-red-800 ring-red-200' },
}

export function StatusBadge({ status }: { status: DocumentStatus }) {
  const { label, className } = styles[status]

  return (
    <span
      className={`inline-flex items-center gap-1.5 rounded-full px-2.5 py-0.5 text-xs font-medium ring-1 ring-inset ${className}`}
    >
      {isInProgress(status) && <Spinner className="size-3" />}
      {label}
    </span>
  )
}
