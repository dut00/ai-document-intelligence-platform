import type { ReactNode } from 'react'
import type { DocumentAnalysis, EntityType, ImportantDate, RiskSeverity } from '../api/types'
import { formatDate, formatMoney, formatWeekday, humanize } from '../lib/format'

export function AnalysisSections({ analysis }: { analysis: DocumentAnalysis }) {
  return (
    <div className="space-y-6">
      <Section title="Summary">
        <p className="text-sm leading-6 whitespace-pre-line text-slate-700">{analysis.summary}</p>
      </Section>
      <Section title="Important dates">
        <ImportantDates dates={analysis.importantDates} />
      </Section>
      <div className="grid gap-6 lg:grid-cols-2">
        <Section title="Entities">
          <Entities analysis={analysis} />
        </Section>
        <Section title="Financial information">
          <FinancialInformation analysis={analysis} />
        </Section>
      </div>
      <Section title="Potential risks">
        <Risks analysis={analysis} />
      </Section>
    </div>
  )
}

function Section({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="rounded-lg border border-slate-200 bg-white p-5">
      <h2 className="mb-3 text-base font-semibold">{title}</h2>
      {children}
    </section>
  )
}

function Empty({ children }: { children: ReactNode }) {
  return <p className="text-sm text-slate-500">{children}</p>
}

function Badge({ className, children }: { className: string; children: ReactNode }) {
  return (
    <span className={`inline-flex rounded-full px-2 py-0.5 text-xs font-medium ring-1 ring-inset ${className}`}>
      {children}
    </span>
  )
}

function ImportantDates({ dates }: { dates: ImportantDate[] }) {
  if (dates.length === 0) {
    return <Empty>No dates detected.</Empty>
  }

  const sorted = [...dates].sort((a, b) => a.date.localeCompare(b.date))

  return (
    <ul className="divide-y divide-slate-100">
      {sorted.map((date, index) => (
        <li key={`${date.date}-${date.type}-${index}`} className="flex flex-wrap items-start gap-x-4 gap-y-2 py-3">
          <div className="w-36 shrink-0">
            <p className="text-sm font-semibold">{formatDate(date.date)}</p>
            <p className="text-xs text-slate-500">{formatWeekday(date.date)}</p>
          </div>
          <div className="min-w-0 flex-1 space-y-1.5">
            <p className="text-sm text-slate-700">
              <span className="font-medium text-slate-900">{humanize(date.type)}</span> · {date.description}
            </p>
            <CalendarBadges date={date} />
          </div>
        </li>
      ))}
    </ul>
  )
}

// The AI only identifies dates; these checks come from the backend's holiday calendar.
function CalendarBadges({ date }: { date: ImportantDate }) {
  const check = date.calendarCheck

  if (!check) {
    return <Badge className="bg-slate-50 text-slate-600 ring-slate-200">Calendar check unavailable</Badge>
  }

  if (check.isBusinessDay) {
    return <Badge className="bg-emerald-50 text-emerald-700 ring-emerald-200">Business day</Badge>
  }

  return (
    <div className="flex flex-wrap gap-1.5">
      {check.isWeekend && <Badge className="bg-amber-50 text-amber-800 ring-amber-200">Weekend</Badge>}
      {check.isPublicHoliday && (
        <Badge className="bg-rose-50 text-rose-800 ring-rose-200">
          Public holiday{check.holidayName ? `: ${check.holidayName}` : ''}
        </Badge>
      )}
      <Badge className="bg-indigo-50 text-indigo-700 ring-indigo-200">
        Next business day: {formatDate(check.nextBusinessDay)}
      </Badge>
    </div>
  )
}

function Entities({ analysis }: { analysis: DocumentAnalysis }) {
  if (analysis.entities.length === 0) {
    return <Empty>No entities detected.</Empty>
  }

  const byType = new Map<EntityType, string[]>()
  for (const entity of analysis.entities) {
    byType.set(entity.type, [...(byType.get(entity.type) ?? []), entity.name])
  }

  return (
    <dl className="space-y-3">
      {[...byType].map(([type, names]) => (
        <div key={type}>
          <dt className="text-xs font-semibold tracking-wide text-slate-500 uppercase">{humanize(type)}</dt>
          <dd className="mt-1 flex flex-wrap gap-1.5">
            {[...new Set(names)].map((name) => (
              <span key={name} className="rounded-md bg-slate-100 px-2 py-0.5 text-sm text-slate-800">
                {name}
              </span>
            ))}
          </dd>
        </div>
      ))}
    </dl>
  )
}

function FinancialInformation({ analysis }: { analysis: DocumentAnalysis }) {
  if (analysis.financialInformation.length === 0) {
    return <Empty>No amounts detected.</Empty>
  }

  return (
    <table className="w-full text-sm">
      <tbody className="divide-y divide-slate-100">
        {analysis.financialInformation.map((item, index) => (
          <tr key={`${item.description}-${index}`}>
            <td className="py-2 pr-4 text-slate-700">{item.description}</td>
            <td className="py-2 text-right font-medium whitespace-nowrap">{formatMoney(item.amount, item.currency)}</td>
          </tr>
        ))}
      </tbody>
    </table>
  )
}

const severityOrder: Record<RiskSeverity, number> = { High: 0, Medium: 1, Low: 2 }

const severityStyles: Record<RiskSeverity, string> = {
  High: 'bg-red-50 text-red-800 ring-red-200',
  Medium: 'bg-amber-50 text-amber-800 ring-amber-200',
  Low: 'bg-slate-50 text-slate-700 ring-slate-200',
}

function Risks({ analysis }: { analysis: DocumentAnalysis }) {
  if (analysis.potentialRisks.length === 0) {
    return <Empty>No risks identified.</Empty>
  }

  const sorted = [...analysis.potentialRisks].sort((a, b) => severityOrder[a.severity] - severityOrder[b.severity])

  return (
    <ul className="space-y-2">
      {sorted.map((risk, index) => (
        <li key={`${risk.severity}-${index}`} className="flex items-start gap-3 text-sm">
          <span className="w-16 shrink-0">
            <Badge className={severityStyles[risk.severity]}>{risk.severity}</Badge>
          </span>
          <span className="text-slate-700">{risk.description}</span>
        </li>
      ))}
    </ul>
  )
}
