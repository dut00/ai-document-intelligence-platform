import { createContext } from 'react'
import type { Credentials } from '../api/auth'
import type { User } from '../api/types'

export type AuthState =
  | { status: 'loading' }
  // signedOut: the user (here or in another tab) signed out, so the next sign-in starts from the
  // dashboard instead of returning to the page the previous user was on.
  | { status: 'anonymous'; signedOut: boolean }
  | { status: 'authenticated'; user: User }
  // The session could not be checked (API down, rate limited); it may well still be valid.
  | { status: 'unavailable'; error: unknown }

export type RegisterOutcome = 'signedIn' | 'signInFailed'

export interface AuthContextValue {
  state: AuthState
  login: (credentials: Credentials) => Promise<void>
  // The account is created even when the sign-in that follows fails; the caller then asks for a login.
  register: (credentials: Credentials) => Promise<RegisterOutcome>
  // Throws when the API could not end the session; the user then stays signed in.
  logout: () => Promise<void>
  retry: () => void
}

export const AuthContext = createContext<AuthContextValue | null>(null)
