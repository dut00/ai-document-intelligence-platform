import type { DocumentStatus } from '../api/types'

export function isInProgress(status: DocumentStatus): boolean {
  return status === 'Pending' || status === 'Processing'
}
