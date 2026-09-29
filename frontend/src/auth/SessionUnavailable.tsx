import { ErrorAlert } from '../components/ErrorAlert'
import { useAuth } from './useAuth'

/** Shown when the session could not be checked; signing in again would only open a second session. */
export function SessionUnavailable() {
  const { state, retry } = useAuth()

  return (
    <div className="flex min-h-screen items-center justify-center bg-slate-50 px-4">
      <div className="w-full max-w-sm space-y-4 text-center">
        <h1 className="text-xl font-semibold text-slate-900">Cannot reach the server</h1>
        <ErrorAlert error={state.status === 'unavailable' ? state.error : null} />
        <button
          type="button"
          onClick={retry}
          className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-semibold text-white hover:bg-indigo-500"
        >
          Try again
        </button>
      </div>
    </div>
  )
}
