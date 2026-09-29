import { ApiError } from '../api/client'

export function ErrorAlert({ error }: { error: unknown }) {
  if (!error) {
    return null
  }

  const details = error instanceof ApiError ? error.fieldErrors : []

  return (
    <div role="alert" className="rounded-md border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-800">
      {details.length > 1 ? (
        <ul className="list-inside list-disc space-y-1">
          {details.map((detail) => (
            <li key={detail}>{detail}</li>
          ))}
        </ul>
      ) : (
        errorMessage(error)
      )}
    </div>
  )
}

function errorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.status === 429) {
      return 'Too many requests. Please wait a moment and try again.'
    }

    return error.fieldErrors[0] ?? error.message
  }

  return error instanceof Error ? error.message : 'Something went wrong.'
}
