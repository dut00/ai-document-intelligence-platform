import { HubConnectionBuilder, LogLevel, type HubConnection } from '@microsoft/signalr'
import { useQueryClient } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import { getAccessToken } from '../api/client'
import { documentKeys } from '../api/documents'
import type { DocumentStatusNotification } from '../api/types'

export type HubStatus = 'connecting' | 'connected' | 'reconnecting' | 'disconnected'

// After the client's own reconnect attempts give up, keep trying to start again at this pace.
const restartDelayMs = 10_000

/**
 * Listens for the signed-in user's document status changes and refetches the affected queries.
 *
 * The API pushes only final statuses (Completed, Failed). A notification carries no document data,
 * so every document query is invalidated and TanStack Query refetches the ones on screen.
 */
export function useDocumentStatusHub(): HubStatus {
  const queryClient = useQueryClient()
  const [status, setStatus] = useState<HubStatus>('connecting')

  useEffect(() => {
    let stopped = false
    let restartTimer: ReturnType<typeof setTimeout> | undefined

    const refetchDocuments = () => queryClient.invalidateQueries({ queryKey: documentKeys.all })

    const connection: HubConnection = new HubConnectionBuilder()
      // Called on every (re)connect. Browsers cannot set headers on a WebSocket, so the client sends
      // the token in the query string; the API accepts it there on the hub path only. The hub
      // closes the connection when the token expires, and the next connect gets a fresh one.
      .withUrl('/hubs/documents', { accessTokenFactory: async () => (await getAccessToken()) ?? '' })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build()

    connection.on('DocumentStatusChanged', (_notification: DocumentStatusNotification) => {
      void refetchDocuments()
    })

    connection.onreconnecting(() => setStatus('reconnecting'))
    connection.onreconnected(() => {
      setStatus('connected')
      // Notifications sent while disconnected are lost.
      void refetchDocuments()
    })
    connection.onclose(() => {
      if (stopped) {
        return
      }

      setStatus('disconnected')
      restartTimer = setTimeout(start, restartDelayMs)
    })

    function start() {
      connection
        .start()
        .then(() => {
          setStatus('connected')
          void refetchDocuments()
        })
        .catch(() => {
          if (!stopped) {
            setStatus('disconnected')
            restartTimer = setTimeout(start, restartDelayMs)
          }
        })
    }

    // Start on the next tick: React's StrictMode unmounts a fresh effect at once in development,
    // and stopping a connection mid-negotiation logs an error.
    restartTimer = setTimeout(start, 0)

    return () => {
      stopped = true
      clearTimeout(restartTimer)
      void connection.stop()
    }
  }, [queryClient])

  return status
}
