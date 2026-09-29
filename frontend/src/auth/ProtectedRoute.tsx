import { Navigate, Outlet, useLocation } from 'react-router'
import { FullPageSpinner } from '../components/Spinner'
import { SessionUnavailable } from './SessionUnavailable'
import { useAuth } from './useAuth'

/** Renders the child routes for a signed-in user and sends anyone else to the login page. */
export function ProtectedRoute() {
  const { state } = useAuth()
  const location = useLocation()

  switch (state.status) {
    case 'loading':
      return <FullPageSpinner />
    case 'unavailable':
      return <SessionUnavailable />
    case 'anonymous':
      // After a sign-out the next user starts from the dashboard, not from the previous user's page.
      return state.signedOut ? (
        <Navigate to="/login" replace />
      ) : (
        <Navigate to="/login" replace state={{ from: location.pathname + location.search }} />
      )
    case 'authenticated':
      return <Outlet />
  }
}

/**
 * The opposite guard for the login and register pages: a signed-in user goes back to the page
 * that sent them to log in, or to the dashboard.
 */
export function AnonymousRoute() {
  const { state } = useAuth()
  const location = useLocation()
  const from = (location.state as { from?: string } | null)?.from ?? '/'

  switch (state.status) {
    case 'loading':
      return <FullPageSpinner />
    case 'unavailable':
      return <SessionUnavailable />
    case 'authenticated':
      return <Navigate to={from} replace />
    case 'anonymous':
      return <Outlet />
  }
}
