// The UI text is English, so dates and amounts follow it rather than the browser's language.
const locale = 'en-GB'

const dateTimeFormat = new Intl.DateTimeFormat(locale, { dateStyle: 'medium', timeStyle: 'short' })
const dateFormat = new Intl.DateTimeFormat(locale, { dateStyle: 'medium', timeZone: 'UTC' })

export function formatDateTime(value: string): string {
  return dateTimeFormat.format(new Date(value))
}

/** Formats a calendar date ("2026-11-11") without shifting it into the local time zone. */
export function formatDate(value: string): string {
  return dateFormat.format(new Date(`${value}T00:00:00Z`))
}

export function formatWeekday(value: string): string {
  return new Intl.DateTimeFormat(locale, { weekday: 'long', timeZone: 'UTC' }).format(new Date(`${value}T00:00:00Z`))
}

export function formatFileSize(bytes: number): string {
  if (bytes < 1024) {
    return `${bytes} B`
  }

  const kilobytes = bytes / 1024
  return kilobytes < 1024 ? `${kilobytes.toFixed(1)} KB` : `${(kilobytes / 1024).toFixed(1)} MB`
}

export function formatMoney(amount: number, currency: string): string {
  try {
    return new Intl.NumberFormat(locale, { style: 'currency', currency }).format(amount)
  } catch {
    // Not an ISO 4217 code the browser knows.
    return `${amount.toLocaleString(locale)} ${currency}`
  }
}

/** "PaymentDeadline" -> "Payment deadline" */
export function humanize(value: string): string {
  const words = value.replace(/([a-z])([A-Z])/g, '$1 $2').toLowerCase()
  return words.charAt(0).toUpperCase() + words.slice(1)
}
