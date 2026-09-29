import { useQueryClient } from '@tanstack/react-query'
import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import * as authApi from '../api/auth'
import { ApiError, getAccessToken, onSessionEvent } from '../api/client'
import { AuthContext, type AuthContextValue, type AuthState } from './AuthContext'

export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient()
  const [state, setState] = useState<AuthState>({ status: 'loading' })

  useEffect(() => {
    void resolveSession(false).then(setState)
  }, [])

  useEffect(
    () =>
      onSessionEvent((event) => {
        // Nothing of the previous session may stay cached for whoever comes next.
        queryClient.clear()

        if (event === 'ended') {
          // Expired or revoked: the same user signs in again and returns to the same page.
          setState({ status: 'anonymous', signedOut: false })
        } else {
          setState({ status: 'loading' })
          void resolveSession(true).then(setState)
        }
      }),
    [queryClient],
  )

  const login = useCallback(async (credentials: authApi.Credentials) => {
    await authApi.login(credentials)
    const user = await authApi.getCurrentUser()
    setState({ status: 'authenticated', user })
  }, [])

  const register = useCallback(
    async (credentials: authApi.Credentials) => {
      await authApi.register(credentials)

      try {
        await login(credentials)
        return 'signedIn' as const
      } catch {
        return 'signInFailed' as const
      }
    },
    [login],
  )

  const logout = useCallback(async () => {
    await authApi.logout()
    queryClient.clear()
    setState({ status: 'anonymous', signedOut: true })
  }, [queryClient])

  const retry = useCallback(() => {
    setState({ status: 'loading' })
    void resolveSession(false).then(setState)
  }, [])

  const value = useMemo<AuthContextValue>(
    () => ({ state, login, register, logout, retry }),
    [state, login, register, logout, retry],
  )

  return <AuthContext value={value}>{children}</AuthContext>
}

/**
 * Finds out who is signed in: after a reload only the refresh-token cookie is left, and after a
 * sign-in or sign-out in another tab the cookie may belong to someone else.
 */
async function resolveSession(signedOut: boolean): Promise<AuthState> {
  try {
    const token = await getAccessToken()
    return token ? { status: 'authenticated', user: await authApi.getCurrentUser() } : { status: 'anonymous', signedOut }
  } catch (error) {
    return error instanceof ApiError && error.status === 401 ? { status: 'anonymous', signedOut } : { status: 'unavailable', error }
  }
}
