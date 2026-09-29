import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router'
import { documentKeys, getDocuments, getDocumentStats } from '../api/documents'
import { DocumentTable } from '../components/DocumentTable'
import { ErrorAlert } from '../components/ErrorAlert'
import { Spinner } from '../components/Spinner'

const recentParams = { page: 1, pageSize: 5 }

export function DashboardPage() {
  const stats = useQuery({ queryKey: documentKeys.stats, queryFn: getDocumentStats })
  const recent = useQuery({ queryKey: documentKeys.list(recentParams), queryFn: () => getDocuments(recentParams) })

  const tiles = stats.data && [
    { label: 'Total', value: stats.data.total, className: 'text-slate-900' },
    { label: 'In progress', value: stats.data.pending + stats.data.processing, className: 'text-amber-700' },
    { label: 'Completed', value: stats.data.completed, className: 'text-emerald-700' },
    { label: 'Failed', value: stats.data.failed, className: 'text-red-700' },
  ]

  return (
    <div className="space-y-8">
      <div className="flex items-center justify-between gap-4">
        <h1 className="text-2xl font-bold">Dashboard</h1>
        <Link
          to="/upload"
          className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-semibold text-white hover:bg-indigo-500"
        >
          Upload document
        </Link>
      </div>

      <section aria-label="Statistics">
        <ErrorAlert error={stats.error} />
        {stats.isPending && <Spinner />}
        {tiles && (
          <dl className="grid grid-cols-2 gap-4 md:grid-cols-4">
            {tiles.map((tile) => (
              <div key={tile.label} className="rounded-lg border border-slate-200 bg-white p-4">
                <dt className="text-sm text-slate-500">{tile.label}</dt>
                <dd className={`mt-1 text-3xl font-semibold ${tile.className}`}>{tile.value}</dd>
              </div>
            ))}
          </dl>
        )}
      </section>

      <section className="space-y-3">
        <div className="flex items-center justify-between">
          <h2 className="text-lg font-semibold">Recent documents</h2>
          <Link to="/documents" className="text-sm font-medium text-indigo-700 hover:underline">
            View all
          </Link>
        </div>
        <ErrorAlert error={recent.error} />
        {recent.isPending && <Spinner />}
        {recent.data &&
          (recent.data.items.length > 0 ? (
            <DocumentTable documents={recent.data.items} />
          ) : (
            <p className="rounded-lg border border-dashed border-slate-300 bg-white p-8 text-center text-sm text-slate-500">
              No documents yet.{' '}
              <Link to="/upload" className="font-medium text-indigo-700 hover:underline">
                Upload your first one
              </Link>
              .
            </p>
          ))}
      </section>
    </div>
  )
}
