import { Link, useLocation, useNavigate } from 'react-router'
import type { Credentials } from '../api/auth'
import { useAuth } from '../auth/useAuth'
import { AuthForm } from '../components/AuthForm'

export function RegisterPage() {
  const { register } = useAuth()
  const location = useLocation()
  const navigate = useNavigate()

  async function handleSubmit(credentials: Credentials) {
    // Submitting again would only fail with "already registered", so hand over to the login page.
    if ((await register(credentials)) === 'signInFailed') {
      await navigate('/login', { state: { ...(location.state as object | null), notice: 'Account created. Please sign in.' } })
    }
  }

  return (
    <AuthForm
      title="Create an account"
      submitLabel="Create account"
      passwordHint="At least 8 characters."
      passwordAutoComplete="new-password"
      onSubmit={handleSubmit}
      footer={
        <>
          Already registered?{' '}
          <Link to="/login" state={location.state} className="font-medium text-indigo-700 hover:underline">
            Sign in
          </Link>
        </>
      }
    />
  )
}
