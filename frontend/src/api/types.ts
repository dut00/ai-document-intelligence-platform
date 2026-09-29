// Mirrors the API's DTOs (DocumentIntelligence.Application). Enums travel as their names.

export type DocumentStatus = 'Pending' | 'Processing' | 'Completed' | 'Failed'

export type EntityType = 'Person' | 'Organization' | 'Location' | 'Other'

export type ImportantDateType =
  | 'StartDate'
  | 'EndDate'
  | 'EffectiveDate'
  | 'SigningDate'
  | 'PaymentDeadline'
  | 'TerminationDeadline'
  | 'ExpiryDate'
  | 'Other'

export type RiskSeverity = 'Low' | 'Medium' | 'High'

export interface AccessTokenResponse {
  accessToken: string
  expiresAt: string
}

export interface User {
  id: string
  email: string
}

export interface PagedResponse<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export interface DocumentSummary {
  id: string
  fileName: string
  contentType: string
  sizeBytes: number
  status: DocumentStatus
  uploadedAt: string
  processedAt: string | null
}

export interface DocumentStats {
  total: number
  pending: number
  processing: number
  completed: number
  failed: number
}

export interface CalendarCheck {
  isWeekend: boolean
  isPublicHoliday: boolean
  holidayName: string | null
  isBusinessDay: boolean
  nextBusinessDay: string
}

export interface ImportantDate {
  date: string
  type: ImportantDateType
  description: string
  // Null when the holiday calendar was unavailable during processing.
  calendarCheck: CalendarCheck | null
}

export interface Entity {
  type: EntityType
  name: string
}

export interface FinancialItem {
  description: string
  amount: number
  currency: string
}

export interface Risk {
  severity: RiskSeverity
  description: string
}

export interface DocumentAnalysis {
  documentType: string
  summary: string
  entities: Entity[]
  importantDates: ImportantDate[]
  financialInformation: FinancialItem[]
  potentialRisks: Risk[]
  model: string
  createdAt: string
}

export interface DocumentDetails extends DocumentSummary {
  failureReason: string | null
  // Null until processing completes.
  analysis: DocumentAnalysis | null
}

// Pushed by the hub when processing of a document finishes.
export interface DocumentStatusNotification {
  documentId: string
  status: DocumentStatus
  failureReason: string | null
}

// RFC 9457 problem details, as returned by the API for every error.
export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  code?: string
  errors?: Record<string, string[]>
}
