import { useState } from 'react'
import { NavLink, Outlet } from 'react-router'
import { useAuth } from '../auth/useAuth'
import { useDocumentStatusHub, type HubStatus } from '../realtime/useDocumentStatusHub'

const navItems = [
  { to: '/', label: 'Dashboard', end: true },
  { to: '/documents', label: 'Documents', end: false },
  { to: '/upload', label: 'Upload', end: false },
]

const hubStatusStyles: Record<HubStatus, { label: string; dot: string }> = {
  connecting: { label: 'Connecting', dot: 'bg-slate-400' },
  connected: { label: 'Live', dot: 'bg-emerald-500' },
  reconnecting: { label: 'Reconnecting', dot: 'bg-amber-500' },
  disconnected: { label: 'Offline', dot: 'bg-red-500' },
}

/** The signed-in shell: navigation, the live-update connection and the current page. */
export function Layout() {
  const { state, logout } = useAuth()
  const hubStatus = useDocumentStatusHub()
  const { label, dot } = hubStatusStyles[hubStatus]
  const [logoutFailed, setLogoutFailed] = useState(false)

  async function handleLogout() {
    setLogoutFailed(false)
    try {
      await logout()
    } catch {
      // The refresh token was not revoked: pretending to be signed out would leave the session usable.
      setLogoutFailed(true)
    }
  }

  return (
    <div className="min-h-screen bg-slate-50 text-slate-900">
      <header className="border-b border-slate-200 bg-white">
        <div className="mx-auto flex max-w-6xl flex-wrap items-center gap-x-6 gap-y-2 px-4 py-3">
          <span className="font-semibold text-indigo-700">Document Intelligence</span>
          <nav className="flex gap-1">
            {navItems.map((item) => (
              <NavLink
                key={item.to}
                to={item.to}
                end={item.end}
                className={({ isActive }) =>
                  `rounded-md px-3 py-1.5 text-sm font-medium ${
                    isActive ? 'bg-indigo-50 text-indigo-700' : 'text-slate-600 hover:bg-slate-100'
                  }`
                }
              >
                {item.label}
              </NavLink>
            ))}
          </nav>
          <div className="ml-auto flex items-center gap-4 text-sm">
            <span className="flex items-center gap-1.5 text-slate-500" title="Live status updates">
              <span className={`size-2 rounded-full ${dot}`} />
              {label}
            </span>
            {state.status === 'authenticated' && (
              <span className="hidden text-slate-600 sm:inline">{state.user.email}</span>
            )}
            <button
              type="button"
              onClick={() => void handleLogout()}
              className="rounded-md px-3 py-1.5 font-medium text-slate-600 hover:bg-slate-100"
            >
              Log out
            </button>
          </div>
        </div>
      </header>
      {logoutFailed && (
        <div role="alert" className="border-b border-red-200 bg-red-50 px-4 py-2 text-center text-sm text-red-800">
          Could not sign out: the server did not respond. You are still signed in; please try again.
        </div>
      )}
      <main className="mx-auto max-w-6xl px-4 py-8">
        <Outlet />
      </main>
    </div>
  )
}
