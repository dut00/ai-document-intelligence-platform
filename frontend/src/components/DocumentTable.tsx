import { Link } from 'react-router'
import type { DocumentSummary } from '../api/types'
import { formatDateTime, formatFileSize } from '../lib/format'
import { StatusBadge } from './StatusBadge'

export function DocumentTable({ documents }: { documents: DocumentSummary[] }) {
  return (
    <div className="overflow-x-auto rounded-lg border border-slate-200 bg-white">
      <table className="min-w-full divide-y divide-slate-200 text-sm">
        <thead className="bg-slate-50 text-left text-xs font-semibold tracking-wide text-slate-600 uppercase">
          <tr>
            <th scope="col" className="px-4 py-3">
              Name
            </th>
            <th scope="col" className="px-4 py-3">
              Status
            </th>
            <th scope="col" className="hidden px-4 py-3 sm:table-cell">
              Size
            </th>
            <th scope="col" className="hidden px-4 py-3 md:table-cell">
              Uploaded
            </th>
          </tr>
        </thead>
        <tbody className="divide-y divide-slate-100">
          {documents.map((document) => (
            <tr key={document.id} className="hover:bg-slate-50">
              <td className="max-w-xs truncate px-4 py-3 font-medium">
                <Link to={`/documents/${document.id}`} className="text-indigo-700 hover:underline">
                  {document.fileName}
                </Link>
              </td>
              <td className="px-4 py-3">
                <StatusBadge status={document.status} />
              </td>
              <td className="hidden px-4 py-3 text-slate-600 sm:table-cell">{formatFileSize(document.sizeBytes)}</td>
              <td className="hidden px-4 py-3 text-slate-600 md:table-cell">{formatDateTime(document.uploadedAt)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
