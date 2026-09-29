import { useState, type FormEvent, type ReactNode } from 'react'
import type { Credentials } from '../api/auth'
import { ErrorAlert } from './ErrorAlert'
import { Spinner } from './Spinner'

interface AuthFormProps {
  title: string
  submitLabel: string
  passwordHint?: string
  notice?: string
  passwordAutoComplete: 'current-password' | 'new-password'
  onSubmit: (credentials: Credentials) => Promise<void>
  footer: ReactNode
}

/** The shared email + password form of the login and register pages. */
export function AuthForm({ title, submitLabel, passwordHint, notice, passwordAutoComplete, onSubmit, footer }: AuthFormProps) {
  const [error, setError] = useState<unknown>(null)
  const [submitting, setSubmitting] = useState(false)

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const form = new FormData(event.currentTarget)

    setError(null)
    setSubmitting(true)
    try {
      await onSubmit({ email: String(form.get('email')), password: String(form.get('password')) })
    } catch (caught) {
      setError(caught)
      setSubmitting(false)
    }
  }

  const inputClass =
    'mt-1 block w-full rounded-md border border-slate-300 px-3 py-2 text-sm shadow-sm focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500 focus:outline-none'

  return (
    <div className="flex min-h-screen items-center justify-center bg-slate-50 px-4">
      <div className="w-full max-w-sm">
        <p className="mb-2 text-center text-sm font-semibold text-indigo-700">Document Intelligence</p>
        <h1 className="mb-6 text-center text-2xl font-bold text-slate-900">{title}</h1>
        <form
          onSubmit={(event) => void handleSubmit(event)}
          className="space-y-4 rounded-lg border border-slate-200 bg-white p-6 shadow-sm"
        >
          {notice && !error && (
            <div role="status" className="rounded-md border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm text-emerald-800">
              {notice}
            </div>
          )}
          <ErrorAlert error={error} />
          <label className="block text-sm font-medium text-slate-700">
            Email
            <input name="email" type="email" required autoComplete="email" className={inputClass} />
          </label>
          <label className="block text-sm font-medium text-slate-700">
            Password
            <input
              name="password"
              type="password"
              required
              minLength={passwordAutoComplete === 'new-password' ? 8 : undefined}
              autoComplete={passwordAutoComplete}
              className={inputClass}
            />
            {passwordHint && <span className="mt-1 block text-xs font-normal text-slate-500">{passwordHint}</span>}
          </label>
          <button
            type="submit"
            disabled={submitting}
            className="flex w-full items-center justify-center gap-2 rounded-md bg-indigo-600 px-4 py-2 text-sm font-semibold text-white hover:bg-indigo-500 disabled:opacity-60"
          >
            {submitting && <Spinner />}
            {submitLabel}
          </button>
        </form>
        <p className="mt-4 text-center text-sm text-slate-600">{footer}</p>
      </div>
    </div>
  )
}
