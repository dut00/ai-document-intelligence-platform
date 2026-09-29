import { Link, useLocation } from 'react-router'
import { useAuth } from '../auth/useAuth'
import { AuthForm } from '../components/AuthForm'

export function LoginPage() {
  const { login } = useAuth()
  const location = useLocation()
  const notice = (location.state as { notice?: string } | null)?.notice

  return (
    <AuthForm
      title="Sign in"
      submitLabel="Sign in"
      passwordAutoComplete="current-password"
      notice={notice}
      // Once signed in, AnonymousRoute sends the user back to where they came from.
      onSubmit={login}
      footer={
        <>
          No account yet?{' '}
          <Link to="/register" state={location.state} className="font-medium text-indigo-700 hover:underline">
            Create one
          </Link>
        </>
      }
    />
  )
}
