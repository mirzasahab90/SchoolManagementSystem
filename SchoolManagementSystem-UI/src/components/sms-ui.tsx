import type { ButtonHTMLAttributes, InputHTMLAttributes, ReactNode, TextareaHTMLAttributes } from "react";
import { Search } from "lucide-react";
import { cn } from "@/lib/utils";

export function PageHeader({
  title,
  description,
  actions,
}: {
  title: string;
  description?: string;
  actions?: ReactNode;
}) {
  return (
    <div className="mb-6 flex flex-wrap items-start justify-between gap-4">
      <div className="min-w-0">
        <h1 className="font-display text-2xl font-semibold tracking-tight text-[var(--foreground)]">{title}</h1>
        {description ? <p className="mt-1 max-w-2xl text-sm text-[var(--muted-foreground)]">{description}</p> : null}
      </div>
      {actions ? <div className="flex flex-wrap items-center gap-2">{actions}</div> : null}
    </div>
  );
}

export function Panel({
  children,
  className,
  title,
  description,
  action,
}: {
  children: ReactNode;
  className?: string;
  title?: string;
  description?: string;
  action?: ReactNode;
}) {
  return (
    <section className={cn("panel overflow-hidden", className)}>
      {title ? (
        <header className="flex flex-wrap items-center justify-between gap-3 border-b border-[var(--border)] px-5 py-4">
          <div>
            <h2 className="text-sm font-semibold tracking-tight">{title}</h2>
            {description ? <p className="mt-0.5 text-xs text-[var(--muted-foreground)]">{description}</p> : null}
          </div>
          {action}
        </header>
      ) : null}
      <div className={cn(title ? "p-5" : "p-5")}>{children}</div>
    </section>
  );
}

export function SectionLabel({ children }: { children: ReactNode }) {
  return (
    <div className="text-xs font-semibold uppercase tracking-wide text-[var(--muted-foreground)]">{children}</div>
  );
}

export function Btn({
  variant = "primary",
  className,
  ...props
}: ButtonHTMLAttributes<HTMLButtonElement> & { variant?: "primary" | "ghost" | "danger" | "secondary" }) {
  return (
    <button
      className={cn(
        "inline-flex h-10 items-center justify-center rounded-lg px-4 text-sm font-medium transition disabled:opacity-50",
        variant === "primary" && "bg-[var(--primary)] text-[var(--primary-foreground)] hover:opacity-90",
        variant === "secondary" && "bg-[var(--muted)] text-[var(--foreground)] hover:bg-[oklch(0.93_0.01_220)]",
        variant === "ghost" && "border border-[var(--border)] bg-white hover:bg-[var(--muted)]",
        variant === "danger" && "bg-red-600 text-white hover:bg-red-700",
        className,
      )}
      {...props}
    />
  );
}

export function Field({ label, children, hint }: { label: string; children: ReactNode; hint?: string }) {
  return (
    <label className="block space-y-1.5">
      <span className="text-xs font-medium uppercase tracking-wide text-[var(--muted-foreground)]">{label}</span>
      {children}
      {hint ? <span className="block text-[11px] text-[var(--muted-foreground)]">{hint}</span> : null}
    </label>
  );
}

export function TextInput(props: InputHTMLAttributes<HTMLInputElement>) {
  return (
    <input
      className="h-10 w-full rounded-lg border border-[var(--border)] bg-white px-3 text-sm outline-none ring-[var(--primary)] focus:ring-2 disabled:opacity-60"
      {...props}
    />
  );
}

export function TextArea({ className, ...props }: TextareaHTMLAttributes<HTMLTextAreaElement>) {
  return (
    <textarea
      className={cn(
        "w-full rounded-lg border border-[var(--border)] bg-white px-3 py-2 text-sm outline-none ring-[var(--primary)] focus:ring-2 disabled:opacity-60",
        className,
      )}
      {...props}
    />
  );
}

export function SearchField({
  value,
  onChange,
  placeholder = "Search…",
}: {
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
}) {
  return (
    <div className="relative min-w-[220px] flex-1">
      <Search className="pointer-events-none absolute top-1/2 left-3 h-4 w-4 -translate-y-1/2 text-[var(--muted-foreground)]" />
      <input
        value={value}
        onChange={(e) => onChange(e.target.value)}
        placeholder={placeholder}
        className="h-10 w-full rounded-lg border border-[var(--border)] bg-white pr-3 pl-9 text-sm outline-none ring-[var(--primary)] focus:ring-2"
      />
    </div>
  );
}

export type StatusTone = "neutral" | "success" | "warning" | "danger" | "info";

const toneStyles: Record<StatusTone, string> = {
  neutral: "border-[var(--border)] bg-[var(--muted)] text-[var(--muted-foreground)]",
  success: "border-emerald-200 bg-emerald-50 text-emerald-800",
  warning: "border-amber-200 bg-amber-50 text-amber-800",
  danger: "border-red-200 bg-red-50 text-red-700",
  info: "border-sky-200 bg-sky-50 text-sky-800",
};

const toneDot: Record<StatusTone, string> = {
  neutral: "bg-[var(--muted-foreground)]",
  success: "bg-emerald-500",
  warning: "bg-amber-500",
  danger: "bg-red-500",
  info: "bg-sky-500",
};

export function StatusPill({
  children,
  tone = "neutral",
  dot = false,
}: {
  children: ReactNode;
  tone?: StatusTone;
  dot?: boolean;
}) {
  return (
    <span
      className={cn(
        "inline-flex items-center gap-1.5 rounded-full border px-2.5 py-[3px] text-[11px] font-medium whitespace-nowrap",
        toneStyles[tone],
      )}
    >
      {dot ? <span className={cn("size-1.5 shrink-0 rounded-full", toneDot[tone])} /> : null}
      {children}
    </span>
  );
}

export function DataTable({ children }: { children: ReactNode }) {
  return (
    <div className="overflow-hidden rounded-xl border border-[var(--border)]">
      <table className="w-full text-left text-sm">{children}</table>
    </div>
  );
}

export function EmptyState({ title, description }: { title: string; description?: string }) {
  return (
    <div className="rounded-xl border border-dashed border-[var(--border)] px-6 py-12 text-center">
      <div className="font-display text-base font-semibold">{title}</div>
      {description ? <p className="mt-1 text-sm text-[var(--muted-foreground)]">{description}</p> : null}
    </div>
  );
}

export function KpiGrid({ children }: { children: ReactNode }) {
  return <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">{children}</div>;
}

export function KpiCard({
  label,
  value,
  hint,
  tone = "neutral",
  onClick,
}: {
  label: string;
  value: string;
  hint?: string | null;
  tone?: string;
  onClick?: () => void;
}) {
  const Comp = onClick ? "button" : "div";
  return (
    <Comp
      type={onClick ? "button" : undefined}
      onClick={onClick}
      className={cn(
        "panel relative overflow-hidden p-5 text-left transition",
        onClick && "cursor-pointer hover:-translate-y-0.5",
      )}
    >
      <div
        className={cn(
          "absolute inset-y-0 left-0 w-1",
          tone === "primary" ? "bg-[var(--primary)]" : "bg-[var(--border)]",
        )}
      />
      <div className="text-xs font-medium uppercase tracking-wide text-[var(--muted-foreground)]">{label}</div>
      <div className="font-display mt-2 text-3xl font-semibold tracking-tight">{value}</div>
      {hint ? <div className="mt-1 text-sm text-[var(--muted-foreground)]">{hint}</div> : null}
    </Comp>
  );
}

export function Toolbar({ children }: { children: ReactNode }) {
  return <div className="mb-4 flex flex-wrap items-center gap-3">{children}</div>;
}
