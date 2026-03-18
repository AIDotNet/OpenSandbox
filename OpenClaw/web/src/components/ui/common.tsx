import type { ReactNode } from "react"
import { LayoutDashboard } from "lucide-react"

export const inputClass = "h-11 w-full rounded-xl border border-border bg-background px-3 text-sm outline-none transition focus:border-ring focus:ring-2 focus:ring-ring/20"
export const textareaClass = "min-h-28 w-full rounded-xl border border-border bg-background px-3 py-2 text-sm outline-none transition focus:border-ring focus:ring-2 focus:ring-ring/20"
export const primaryButtonClass = "inline-flex h-10 items-center justify-center rounded-xl bg-primary px-4 text-sm font-medium text-primary-foreground transition hover:opacity-90 disabled:cursor-not-allowed disabled:opacity-60"
export const secondaryButtonClass = "inline-flex h-10 items-center justify-center rounded-xl border border-border bg-background px-4 text-sm font-medium transition hover:bg-accent hover:text-accent-foreground disabled:cursor-not-allowed disabled:opacity-60"
export const tinyButtonClass = "inline-flex h-9 items-center justify-center rounded-xl border border-border bg-background px-3 text-xs font-medium transition hover:bg-accent hover:text-accent-foreground"

export function Card(props: { title: string; description?: string; actions?: ReactNode; children: ReactNode }) {
  return (
    <section className="rounded-3xl border border-border bg-card p-5 shadow-sm">
      <div className="mb-5 flex flex-wrap items-start justify-between gap-3">
        <div>
          <div className="text-lg font-semibold tracking-tight">{props.title}</div>
          {props.description ? <div className="mt-1 text-sm text-muted-foreground">{props.description}</div> : null}
        </div>
        {props.actions ? <div className="flex flex-wrap gap-2">{props.actions}</div> : null}
      </div>
      {props.children}
    </section>
  )
}

export function MetricCard(props: { icon: typeof LayoutDashboard; label: string; value: string; hint: string }) {
  const Icon = props.icon
  return (
    <div className="rounded-3xl border border-border bg-card p-5 shadow-sm">
      <div className="flex items-start justify-between gap-3">
        <div>
          <div className="text-sm text-muted-foreground">{props.label}</div>
          <div className="mt-3 text-3xl font-semibold tracking-tight">{props.value}</div>
          <div className="mt-2 text-xs text-muted-foreground">{props.hint}</div>
        </div>
        <div className="flex h-12 w-12 items-center justify-center rounded-2xl bg-muted text-muted-foreground"><Icon className="h-5 w-5" /></div>
      </div>
    </div>
  )
}

export function DetailCard(props: { label: string; value: string }) {
  return (
    <div className="rounded-3xl border border-border bg-background px-4 py-4">
      <div className="text-xs uppercase tracking-[0.18em] text-muted-foreground">{props.label}</div>
      <div className="mt-2 break-all text-sm font-medium">{props.value}</div>
    </div>
  )
}

export function EmptyState(props: { icon: typeof LayoutDashboard; title: string; description: string }) {
  const Icon = props.icon
  return (
    <div className="rounded-2xl border border-dashed border-border bg-muted/20 px-6 py-10 text-center">
      <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-2xl bg-muted text-muted-foreground"><Icon className="h-5 w-5" /></div>
      <div className="mt-4 text-sm font-medium">{props.title}</div>
      <div className="mt-2 text-sm text-muted-foreground">{props.description}</div>
    </div>
  )
}

export function StatusBadge(props: { label: string; tone: "success" | "danger" | "brand" | "neutral" }) {
  const toneClass = {
    success: "border-border bg-muted text-foreground",
    danger: "border-border bg-muted text-foreground",
    brand: "border-border bg-accent text-accent-foreground",
    neutral: "border-border bg-muted text-muted-foreground",
  }[props.tone]
  return <span className={`inline-flex rounded-full border px-2.5 py-1 text-xs font-medium ${toneClass}`}>{props.label}</span>
}

export function DetailRow(props: { label: string; value: string }) {
  return (
    <div className="rounded-2xl border border-border bg-background px-4 py-3">
      <div className="text-xs uppercase tracking-[0.18em] text-muted-foreground">{props.label}</div>
      <div className="mt-2 break-all text-sm font-medium">{props.value}</div>
    </div>
  )
}

export function InfoBlock(props: { title: string; description: string }) {
  return (
    <div className="rounded-2xl border border-border bg-background p-4">
      <div className="font-medium">{props.title}</div>
      <div className="mt-2 break-all text-sm leading-6 text-muted-foreground">{props.description}</div>
    </div>
  )
}

export function TableWrap(props: { children: ReactNode }) {
  return <div className="overflow-hidden rounded-2xl border border-border">{props.children}</div>
}

export function Th(props: { children: ReactNode; className?: string }) {
  return <th className={`px-4 py-3 text-left text-xs font-medium uppercase tracking-[0.18em] text-muted-foreground ${props.className ?? ""}`}>{props.children}</th>
}

export function Td(props: { children: ReactNode; className?: string }) {
  return <td className={`px-4 py-3 align-top ${props.className ?? ""}`}>{props.children}</td>
}
