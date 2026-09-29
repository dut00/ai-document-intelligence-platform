import type { AccessTokenResponse, ProblemDetails } from './types'

/**
 * Fetch wrapper for the API.
 *
 * The access token lives only in this module's memory, never in storage that page scripts could
 * read later. The refresh token is an httpOnly cookie the browser sends to /api/auth only, so the
 * session survives a reload through `refreshAccessToken()`.
 */

export class ApiError extends Error {
  readonly status: number
  readonly problem: ProblemDetails | null

  constructor(status: number, problem: ProblemDetails | null) {
    super(problem?.title ?? `Request failed with status ${status}.`)
    this.name = 'ApiError'
    this.status = status
    this.problem = problem
  }

  // Validation errors flattened into one message per field, e.g. for a form.
  get fieldErrors(): string[] {
    return Object.values(this.problem?.errors ?? {}).flat()
  }
}

// Refresh this long before the access token expires, so a request never races its expiry.
const expirySkewMs = 30_000

const refreshLockName = 'document-intelligence:refresh-token'

let accessToken: string | null = null
let accessTokenExpiresAt = 0
let accessTokenSubject: string | null = null
let pendingRefresh: Promise<string | null> | null = null

/**
 * - `ended`: the session cannot be renewed any more (the refresh token expired or was revoked).
 * - `changed`: the cookie now belongs to another sign-in (a login or logout in another tab), so
 *   whatever this tab shows may be someone else's; it must start over.
 */
export type SessionEvent = 'ended' | 'changed'

const sessionListeners = new Set<(event: SessionEvent) => void>()

// Tabs share the refresh-token cookie, so a login or logout in one tab changes the session of all.
const sessionChannel = typeof BroadcastChannel !== 'undefined' ? new BroadcastChannel('document-intelligence:session') : null
sessionChannel?.addEventListener('message', () => {
  // This tab's token may belong to the user who just signed out; the cookie tells who is signed in now.
  setAccessToken(null)
  emit('changed')
})

export function setAccessToken(response: AccessTokenResponse | null): void {
  accessToken = response?.accessToken ?? null
  accessTokenExpiresAt = response ? Date.parse(response.expiresAt) : 0
  accessTokenSubject = accessToken ? readSubject(accessToken) : null
}

export function onSessionEvent(listener: (event: SessionEvent) => void): () => void {
  sessionListeners.add(listener)
  return () => sessionListeners.delete(listener)
}

/** Tells the other tabs that this one signed in or out. */
export function announceSessionChange(): void {
  sessionChannel?.postMessage('changed')
}

/**
 * Runs `callback` (a logout) so that no refresh overlaps it: a refresh in flight would rotate the
 * token the logout revokes, and its response would store the new token in the cookie again.
 */
export async function withoutConcurrentRefresh<T>(callback: () => Promise<T>): Promise<T> {
  await pendingRefresh?.catch(() => null)
  return withCrossTabLock(callback)
}

/**
 * Exchanges the refresh-token cookie for a new access token.
 *
 * Concurrent callers share one request: the API rotates the refresh token on every use and treats
 * a rotated token coming back as theft, revoking the whole session. The Web Locks API extends that
 * to other tabs, which share the cookie; by the time a waiting tab gets the lock, the cookie holds
 * the token the previous tab received.
 */
export function refreshAccessToken(): Promise<string | null> {
  pendingRefresh ??= withCrossTabLock(requestNewAccessToken).finally(() => {
    pendingRefresh = null
  })

  return pendingRefresh
}

/** The current access token, renewed first when it is missing or about to expire. */
export async function getAccessToken(): Promise<string | null> {
  if (accessToken && Date.now() < accessTokenExpiresAt - expirySkewMs) {
    return accessToken
  }

  return refreshAccessToken()
}

export interface RequestOptions extends Omit<RequestInit, 'body'> {
  body?: unknown
  // Anonymous endpoints (login, register) skip the token and the refresh-and-retry.
  anonymous?: boolean
}

/** Sends a request and returns the raw response; any non-2xx status becomes an `ApiError`. */
export async function apiFetch(path: string, options: RequestOptions = {}): Promise<Response> {
  const { anonymous = false, ...init } = options

  if (anonymous) {
    return ensureOk(await send(path, init, null))
  }

  const token = await getAccessToken()
  if (!token) {
    // The session is over (already reported); sending the request would only fail the same way.
    throw new ApiError(401, null)
  }

  let response = await send(path, init, token)

  // The token may still be rejected, e.g. after the signing key changed: renew once and retry.
  if (response.status === 401) {
    const renewed = await refreshAccessToken()
    if (renewed) {
      response = await send(path, init, renewed)
    }
  }

  if (response.status === 401) {
    endSession()
  }

  return ensureOk(response)
}

/** Sends a request and parses the JSON body; 204 No Content resolves to `undefined`. */
export async function apiJson<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const response = await apiFetch(path, options)

  return response.status === 204 ? (undefined as T) : ((await response.json()) as T)
}

async function requestNewAccessToken(): Promise<string | null> {
  const response = await fetch('/api/auth/refresh', { method: 'POST', credentials: 'same-origin' })

  if (response.status === 401) {
    endSession()
    return null
  }

  // Anything else (rate limit, API down) is not a verdict on the session: keep it and report the error.
  await ensureOk(response)

  const previousSubject = accessTokenSubject
  setAccessToken((await response.json()) as AccessTokenResponse)

  // Another tab signed in as someone else and the cookie followed: never continue as that user here.
  if (previousSubject && previousSubject !== accessTokenSubject) {
    emit('changed')
  }

  return accessToken
}

function withCrossTabLock<T>(callback: () => Promise<T>): Promise<T> {
  return typeof navigator !== 'undefined' && navigator.locks
    ? navigator.locks.request(refreshLockName, callback)
    : callback()
}

function send(path: string, init: Omit<RequestOptions, 'anonymous'>, token: string | null): Promise<Response> {
  const headers = new Headers(init.headers)
  let body: BodyInit | undefined

  if (init.body instanceof FormData) {
    // The browser sets the multipart boundary itself.
    body = init.body
  } else if (init.body !== undefined) {
    headers.set('Content-Type', 'application/json')
    body = JSON.stringify(init.body)
  }

  if (token) {
    headers.set('Authorization', `Bearer ${token}`)
  }

  return fetch(path, { ...init, headers, body, credentials: 'same-origin' })
}

async function ensureOk(response: Response): Promise<Response> {
  if (response.ok) {
    return response
  }

  throw new ApiError(response.status, await readProblem(response))
}

async function readProblem(response: Response): Promise<ProblemDetails | null> {
  const contentType = response.headers.get('Content-Type') ?? ''
  if (!contentType.includes('json')) {
    return null
  }

  try {
    return (await response.json()) as ProblemDetails
  } catch {
    return null
  }
}

function endSession(): void {
  setAccessToken(null)
  emit('ended')
}

function emit(event: SessionEvent): void {
  sessionListeners.forEach((listener) => listener(event))
}

// The user id (`sub`) from the token's payload. Only compared, never trusted: the API verifies tokens.
function readSubject(token: string): string | null {
  try {
    const payload = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/')
    return (JSON.parse(atob(payload)) as { sub?: string }).sub ?? null
  } catch {
    return null
  }
}
