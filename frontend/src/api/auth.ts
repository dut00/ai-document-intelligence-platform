import { announceSessionChange, apiFetch, apiJson, setAccessToken, withoutConcurrentRefresh } from './client'
import type { AccessTokenResponse, User } from './types'

export interface Credentials {
  email: string
  password: string
}

export function register(credentials: Credentials): Promise<User> {
  return apiJson<User>('/api/auth/register', { method: 'POST', body: credentials, anonymous: true })
}

export async function login(credentials: Credentials): Promise<void> {
  const tokens = await apiJson<AccessTokenResponse>('/api/auth/login', {
    method: 'POST',
    body: credentials,
    anonymous: true,
  })

  setAccessToken(tokens)
  announceSessionChange()
}

/** Revokes the refresh token and clears its cookie; on failure the session stays as it was. */
export async function logout(): Promise<void> {
  await withoutConcurrentRefresh(() => apiFetch('/api/auth/logout', { method: 'POST', anonymous: true }))

  setAccessToken(null)
  announceSessionChange()
}

export function getCurrentUser(): Promise<User> {
  return apiJson<User>('/api/auth/me')
}
