import { useEffect, useRef, useState } from "react";
import type {
  Ref,
  ButtonHTMLAttributes,
  ComponentType,
  InputHTMLAttributes,
  ReactNode,
  SelectHTMLAttributes,
  TextareaHTMLAttributes,
} from "react";
import { ArrowDown, ArrowUp, ArrowUpDown, Check, ChevronLeft, ChevronRight, FilterX, Inbox, Search, X } from "lucide-react";
import { cn } from "@/lib/utils";

type IconType = ComponentType<{ className?: string }>;

/* ------------------------------------------------------------------ */
/* Page structure                                                      */
/* ------------------------------------------------------------------ */

export function PageHeader({
  title,
  description,
  actions,
  eyebrow,
}: {
  title: string;
  description?: string;
  actions?: ReactNode;
  eyebrow?: ReactNode;
}) {
  return (
    <div className="mb-6 flex flex-wrap items-end justify-between gap-4">
      <div className="min-w-0">
        {eyebrow ? (
          <div className="mb-1.5 text-xs font-medium text-[var(--muted-foreground)]">{eyebrow}</div>
        ) : null}
        <h1 className="text-[1.6rem] leading-tight font-semibold text-[var(--foreground)]">{title}</h1>
        {description ? (
          <p className="mt-1.5 max-w-2xl text-sm leading-relaxed text-[var(--muted-foreground)]">{description}</p>
        ) : null}
      </div>
      {actions ? <div className="flex flex-wrap items-center gap-2">{actions}</div> : null}
    </div>
  );
}

export function Panel({
  children,
  className,
  bodyClassName,
  title,
  description,
  action,
  flush = false,
}: {
  children: ReactNode;
  className?: string;
  bodyClassName?: string;
  title?: string;
  description?: string;
  action?: ReactNode;
  /** No body padding — for tables that run edge to edge. */
  flush?: boolean;
}) {
  return (
    <section className={cn("panel overflow-hidden", className)}>
      {title ? (
        <header className="flex flex-wrap items-center justify-between gap-3 border-b border-[var(--border)] px-5 py-4">
          <div>
            <h2 className="text-sm font-semibold">{title}</h2>
            {description ? <p className="mt-0.5 text-xs text-[var(--muted-foreground)]">{description}</p> : null}
          </div>
          {action}
        </header>
      ) : null}
      <div className={cn(!flush && "p-5", bodyClassName)}>{children}</div>
    </section>
  );
}

export function SectionLabel({ children, className }: { children: ReactNode; className?: string }) {
  return (
    <div className={cn("text-[11px] font-semibold tracking-wider text-[var(--muted-foreground)] uppercase", className)}>
      {children}
    </div>
  );
}

export function FormSection({
  title,
  description,
  children,
}: {
  title: string;
  description?: string;
  children: ReactNode;
}) {
  return (
    <fieldset className="space-y-3 border-t border-[var(--border)] pt-5 first:border-t-0 first:pt-0">
      <div>
        <legend className="text-sm font-semibold">{title}</legend>
        {description ? <p className="mt-0.5 text-xs text-[var(--muted-foreground)]">{description}</p> : null}
      </div>
      {children}
    </fieldset>
  );
}

/* ------------------------------------------------------------------ */
/* Buttons                                                             */
/* ------------------------------------------------------------------ */

type BtnVariant = "primary" | "secondary" | "ghost" | "outline" | "danger";

export function Btn({
  variant = "primary",
  size = "md",
  className,
  ...props
}: ButtonHTMLAttributes<HTMLButtonElement> & { variant?: BtnVariant; size?: "sm" | "md" }) {
  return (
    <button
      className={cn(
        "inline-flex shrink-0 items-center justify-center gap-2 rounded-lg font-medium whitespace-nowrap transition-colors outline-none focus-visible:ring-4 focus-visible:ring-[var(--ring)] disabled:pointer-events-none disabled:opacity-50 [&_svg]:size-4",
        size === "md" ? "h-9 px-3.5 text-sm" : "h-8 px-2.5 text-xs",
        variant === "primary" &&
          "bg-[var(--primary)] text-[var(--primary-foreground)] shadow-[var(--shadow-xs)] hover:bg-[var(--primary-hover)]",
        variant === "secondary" && "bg-[var(--muted)] text-[var(--foreground)] hover:bg-[var(--border)]",
        variant === "outline" &&
          "border border-[var(--border)] bg-[var(--card)] text-[var(--foreground)] shadow-[var(--shadow-xs)] hover:bg-[var(--muted)]",
        variant === "ghost" && "text-[var(--muted-foreground)] hover:bg-[var(--muted)] hover:text-[var(--foreground)]",
        variant === "danger" && "bg-[var(--danger)] text-white hover:opacity-90",
        className,
      )}
      {...props}
    />
  );
}

export function IconButton({
  label,
  tone = "default",
  className,
  children,
  ...props
}: ButtonHTMLAttributes<HTMLButtonElement> & { label: string; tone?: "default" | "danger" }) {
  return (
    <button
      type="button"
      title={label}
      aria-label={label}
      className={cn(
        "inline-flex size-8 items-center justify-center rounded-lg text-[var(--muted-foreground)] transition-colors outline-none hover:bg-[var(--muted)] focus-visible:ring-4 focus-visible:ring-[var(--ring)] disabled:opacity-40 [&_svg]:size-4",
        tone === "danger" ? "hover:text-[var(--danger)]" : "hover:text-[var(--foreground)]",
        className,
      )}
      {...props}
    >
      {children}
    </button>
  );
}

/* ------------------------------------------------------------------ */
/* Form controls                                                       */
/* ------------------------------------------------------------------ */

export function Field({
  label,
  children,
  hint,
  required,
  className,
}: {
  label: string;
  children: ReactNode;
  hint?: string;
  required?: boolean;
  className?: string;
}) {
  return (
    <label className={cn("block space-y-1.5", className)}>
      <span className="text-[13px] font-medium text-[var(--foreground)]">
        {label}
        {required ? <span className="ml-0.5 text-[var(--danger)]">*</span> : null}
      </span>
      {children}
      {hint ? <span className="block text-xs text-[var(--muted-foreground)]">{hint}</span> : null}
    </label>
  );
}

const controlBase =
  "w-full rounded-lg border border-[var(--border-strong)] bg-[var(--card)] text-sm text-[var(--foreground)] shadow-[var(--shadow-xs)] outline-none transition placeholder:text-[var(--muted-foreground)]/70 hover:border-[var(--control-border)] focus:border-[var(--primary)] focus:ring-4 focus:ring-[var(--ring)] disabled:cursor-not-allowed disabled:opacity-60";

export function TextInput({ className, ...props }: InputHTMLAttributes<HTMLInputElement> & { ref?: Ref<HTMLInputElement> }) {
  return <input className={cn(controlBase, "h-9 px-3", className)} {...props} />;
}

export function TextArea({ className, ...props }: TextareaHTMLAttributes<HTMLTextAreaElement>) {
  return <textarea className={cn(controlBase, "px-3 py-2", className)} {...props} />;
}

export function Select({ className, ...props }: SelectHTMLAttributes<HTMLSelectElement>) {
  return <select className={cn(controlBase, "h-9 px-2.5", className)} {...props} />;
}

export function SearchField({
  value,
  onChange,
  placeholder = "Search…",
  className,
  debounce = 0,
}: {
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  className?: string;
  /** Milliseconds to wait after typing stops before calling onChange. */
  debounce?: number;
}) {
  const [text, setText] = useState(value);
  const last = useRef(value);
  const notify = useRef(onChange);
  notify.current = onChange;
  // Follow outside changes (e.g. "Clear filters", back button).
  useEffect(() => {
    if (value !== last.current) {
      last.current = value;
      setText(value);
    }
  }, [value]);
  useEffect(() => {
    if (!debounce || text === last.current) return;
    const t = setTimeout(() => {
      last.current = text;
      notify.current(text);
    }, debounce);
    return () => clearTimeout(t);
  }, [text, debounce]);

  const emit = (v: string) => {
    setText(v);
    if (!debounce) {
      last.current = v;
      onChange(v);
    }
  };

  return (
    <div className={cn("relative w-full sm:max-w-xs", className)}>
      <Search className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-[var(--muted-foreground)]" />
      <input
        value={text}
        onChange={(e) => emit(e.target.value)}
        onKeyDown={(e) => {
          if (e.key === "Escape" && text) {
            e.preventDefault();
            last.current = "";
            setText("");
            onChange("");
          }
        }}
        placeholder={placeholder}
        aria-label={placeholder}
        className={cn(controlBase, "h-9 pr-8 pl-9")}
      />
      {text ? (
        <button
          type="button"
          aria-label="Clear search"
          title="Clear search"
          onClick={() => {
            last.current = "";
            setText("");
            onChange("");
          }}
          className="absolute top-1/2 right-2 inline-flex size-5 -translate-y-1/2 items-center justify-center rounded text-[var(--muted-foreground)] hover:bg-[var(--muted)] hover:text-[var(--foreground)]"
        >
          <X className="size-3.5" />
        </button>
      ) : null}
    </div>
  );
}

export function Switch({
  checked,
  onChange,
  label,
  description,
  disabled,
}: {
  checked: boolean;
  onChange: (value: boolean) => void;
  label: string;
  description?: string;
  disabled?: boolean;
}) {
  return (
    <label className={cn("flex items-start gap-3", disabled ? "cursor-not-allowed opacity-60" : "cursor-pointer")}>
      <button
        type="button"
        role="switch"
        aria-checked={checked}
        disabled={disabled}
        onClick={() => onChange(!checked)}
        className={cn(
          "relative mt-0.5 inline-flex h-5 w-9 shrink-0 rounded-full transition-colors outline-none focus-visible:ring-4 focus-visible:ring-[var(--ring)]",
          checked ? "bg-[var(--primary)]" : "bg-[var(--control-border)]",
        )}
      >
        <span
          className={cn(
            "absolute top-0.5 left-0.5 size-4 rounded-full bg-white shadow-sm transition-transform",
            checked && "translate-x-4",
          )}
        />
      </button>
      <span className="min-w-0">
        <span className="block text-sm font-medium">{label}</span>
        {description ? <span className="block text-xs text-[var(--muted-foreground)]">{description}</span> : null}
      </span>
    </label>
  );
}

/** A selectable card with a checkbox look — for picking roles, campuses, etc. */
export function ChoiceCard({
  checked,
  onToggle,
  title,
  description,
  disabled,
  badge,
}: {
  checked: boolean;
  onToggle: () => void;
  title: ReactNode;
  description?: ReactNode;
  disabled?: boolean;
  badge?: ReactNode;
}) {
  return (
    <button
      type="button"
      role="checkbox"
      aria-checked={checked}
      disabled={disabled}
      onClick={onToggle}
      className={cn(
        "flex w-full items-start gap-3 rounded-lg border px-3 py-2.5 text-left text-sm transition outline-none focus-visible:ring-4 focus-visible:ring-[var(--ring)] disabled:cursor-not-allowed disabled:opacity-50",
        checked
          ? "border-[var(--primary)] bg-[var(--primary-soft)]"
          : "border-[var(--border)] bg-[var(--card)] hover:border-[var(--border-strong)]",
      )}
    >
      <CheckMark checked={checked} />
      <span className="min-w-0 flex-1">
        <span className="flex items-center gap-2 font-medium">
          {title}
          {badge}
        </span>
        {description ? (
          <span className="mt-0.5 block text-xs text-[var(--muted-foreground)]">{description}</span>
        ) : null}
      </span>
    </button>
  );
}

export function CheckMark({ checked, className }: { checked: boolean; className?: string }) {
  return (
    <span
      aria-hidden
      className={cn(
        "mt-0.5 inline-flex size-4 shrink-0 items-center justify-center rounded-[5px] border-[1.5px] transition",
        checked
          ? "border-[color:var(--primary)] bg-[var(--primary)] text-[var(--primary-foreground)]"
          : "border-[color:var(--checkbox-border)] bg-[var(--card)]",
        className,
      )}
    >
      {checked ? <Check className="size-3" strokeWidth={3} /> : null}
    </span>
  );
}

/* ------------------------------------------------------------------ */
/* Data display                                                        */
/* ------------------------------------------------------------------ */

export type StatusTone = "neutral" | "success" | "warning" | "danger" | "info" | "primary";

const toneStyles: Record<StatusTone, string> = {
  neutral: "bg-[var(--muted)] text-[var(--muted-foreground)]",
  success: "bg-[var(--success-soft)] text-[var(--success)]",
  warning: "bg-[var(--warning-soft)] text-[var(--warning)]",
  danger: "bg-[var(--danger-soft)] text-[var(--danger)]",
  info: "bg-[var(--info-soft)] text-[var(--info)]",
  primary: "bg-[var(--primary-soft)] text-[var(--primary-soft-foreground)]",
};

export function StatusPill({
  children,
  tone = "neutral",
  dot = false,
  className,
}: {
  children: ReactNode;
  tone?: StatusTone;
  dot?: boolean;
  className?: string;
}) {
  return (
    <span
      className={cn(
        "inline-flex items-center gap-1.5 rounded-md px-2 py-0.5 text-xs font-medium whitespace-nowrap",
        toneStyles[tone],
        className,
      )}
    >
      {dot ? <span className="size-1.5 shrink-0 rounded-full bg-current" /> : null}
      {children}
    </span>
  );
}

const avatarHues = [277, 200, 155, 25, 330, 60, 240];

export function Avatar({ name, size = "md" }: { name: string; size?: "sm" | "md" | "lg" }) {
  const initials =
    name
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((p) => p[0]?.toUpperCase())
      .join("") || "?";
  const hue = avatarHues[[...name].reduce((sum, ch) => sum + ch.charCodeAt(0), 0) % avatarHues.length];
  return (
    <span
      aria-hidden
      className={cn(
        "inline-flex shrink-0 items-center justify-center rounded-full font-semibold",
        size === "sm" && "size-7 text-[11px]",
        size === "md" && "size-9 text-xs",
        size === "lg" && "size-11 text-sm",
      )}
      style={{
        background: `oklch(0.93 0.05 ${hue})`,
        color: `oklch(0.42 0.14 ${hue})`,
      }}
    >
      {initials}
    </span>
  );
}

export function DataTable({ children }: { children: ReactNode }) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full min-w-[640px] text-left text-sm">{children}</table>
    </div>
  );
}

export type SortState = { key: string; dir: "asc" | "desc" };

export function THead({
  columns,
  sort,
  onSort,
}: {
  columns: { label: string; align?: "left" | "right" | "center"; className?: string; sortKey?: string }[];
  /** Current sort; columns with a sortKey become clickable when onSort is given. */
  sort?: SortState | null;
  onSort?: (next: SortState | null) => void;
}) {
  return (
    <thead>
      <tr className="border-b border-[var(--border)] bg-[var(--muted)]/60">
        {columns.map((c) => {
          const sortable = !!(c.sortKey && onSort);
          const active = sortable && sort?.key === c.sortKey;
          const Icon = !active ? ArrowUpDown : sort?.dir === "desc" ? ArrowDown : ArrowUp;
          return (
            <th
              key={c.label}
              aria-sort={active ? (sort?.dir === "desc" ? "descending" : "ascending") : undefined}
              className={cn(
                "px-5 py-2.5 text-xs font-medium text-[var(--muted-foreground)]",
                c.align === "right" && "text-right",
                c.align === "center" && "text-center",
                c.className,
              )}
            >
              {sortable ? (
                <button
                  type="button"
                  // asc → desc → off
                  onClick={() =>
                    onSort!(!active ? { key: c.sortKey!, dir: "asc" } : sort?.dir === "asc" ? { key: c.sortKey!, dir: "desc" } : null)
                  }
                  className={cn(
                    "group inline-flex items-center gap-1 rounded outline-none hover:text-[var(--foreground)] focus-visible:ring-2 focus-visible:ring-[var(--ring)]",
                    active && "text-[var(--foreground)]",
                  )}
                >
                  {c.label}
                  <Icon className={cn("size-3", !active && "opacity-0 group-hover:opacity-60 group-focus-visible:opacity-60")} />
                </button>
              ) : (
                c.label
              )}
            </th>
          );
        })}
      </tr>
    </thead>
  );
}

export function Tr({ children, onClick }: { children: ReactNode; onClick?: () => void }) {
  return (
    <tr
      onClick={onClick}
      className={cn(
        "border-b border-[var(--border)] transition-colors last:border-b-0 hover:bg-[var(--card-hover)]",
        onClick && "cursor-pointer",
      )}
    >
      {children}
    </tr>
  );
}

export function Td({
  children,
  className,
  align,
}: {
  children?: ReactNode;
  className?: string;
  align?: "left" | "right" | "center";
}) {
  return (
    <td
      className={cn(
        "px-5 py-3 align-middle",
        align === "right" && "text-right",
        align === "center" && "text-center",
        className,
      )}
    >
      {children}
    </td>
  );
}

export function TableSkeleton({ rows = 4, cols = 4 }: { rows?: number; cols?: number }) {
  return (
    <div className="divide-y divide-[var(--border)]">
      {Array.from({ length: rows }).map((_, r) => (
        <div key={r} className="flex items-center gap-6 px-5 py-4">
          <div className="flex flex-1 items-center gap-3">
            <Skeleton className="size-9 rounded-full" />
            <div className="flex-1 space-y-2">
              <Skeleton className="h-3 w-1/3" />
              <Skeleton className="h-2.5 w-1/4" />
            </div>
          </div>
          {Array.from({ length: cols - 1 }).map((__, c) => (
            <Skeleton key={c} className="hidden h-3 w-20 md:block" />
          ))}
        </div>
      ))}
    </div>
  );
}

export function Skeleton({ className }: { className?: string }) {
  return <div className={cn("animate-pulse rounded-md bg-[var(--muted)]", className)} />;
}

export function EmptyState({
  title,
  description,
  icon: Icon = Inbox,
  action,
}: {
  title: string;
  description?: string;
  icon?: IconType;
  action?: ReactNode;
}) {
  return (
    <div className="flex flex-col items-center px-6 py-14 text-center">
      <div className="mb-3 inline-flex size-11 items-center justify-center rounded-xl bg-[var(--muted)] text-[var(--muted-foreground)]">
        <Icon className="size-5" />
      </div>
      <div className="text-sm font-semibold">{title}</div>
      {description ? <p className="mt-1 max-w-sm text-sm text-[var(--muted-foreground)]">{description}</p> : null}
      {action ? <div className="mt-4">{action}</div> : null}
    </div>
  );
}

export function KpiGrid({ children }: { children: ReactNode }) {
  return <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-5">{children}</div>;
}

export function KpiCard({
  label,
  value,
  hint,
  icon: Icon,
  accent = "primary",
  onClick,
}: {
  label: string;
  value: string;
  hint?: string | null;
  icon?: IconType;
  accent?: StatusTone;
  onClick?: () => void;
}) {
  const Comp = onClick ? "button" : "div";
  return (
    <Comp
      type={onClick ? "button" : undefined}
      onClick={onClick}
      className={cn(
        "panel group p-5 text-left transition outline-none focus-visible:ring-4 focus-visible:ring-[var(--ring)]",
        onClick && "cursor-pointer hover:border-[var(--border-strong)] hover:shadow-[var(--shadow-sm)]",
      )}
    >
      <div className="flex items-start justify-between gap-3">
        <div className="text-[13px] font-medium text-[var(--muted-foreground)]">{label}</div>
        {Icon ? (
          <span className={cn("inline-flex size-8 items-center justify-center rounded-lg", toneStyles[accent])}>
            <Icon className="size-4" />
          </span>
        ) : null}
      </div>
      <div className="tabular mt-3 text-[1.75rem] leading-none font-semibold tracking-tight">{value}</div>
      {hint ? <div className="mt-2 truncate text-xs text-[var(--muted-foreground)]">{hint}</div> : null}
    </Comp>
  );
}

export function Toolbar({ children, className }: { children: ReactNode; className?: string }) {
  return (
    <div
      className={cn(
        "flex flex-wrap items-center justify-between gap-3 border-b border-[var(--border)] px-5 py-3",
        className,
      )}
    >
      {children}
    </div>
  );
}

export function Kbd({ children }: { children: ReactNode }) {
  return (
    <kbd className="rounded border border-[var(--border)] bg-[var(--muted)] px-1.5 py-0.5 font-sans text-[10px] font-medium text-[var(--muted-foreground)]">
      {children}
    </kbd>
  );
}

/* ------------------------------------------------------------------ */
/* Filtering and paging                                                */
/* ------------------------------------------------------------------ */

/**
 * The filter row above a list: controls on the left; result count and "Clear filters" on the right.
 * `shown` is how many match, `total` how many exist without filters (omit when unknown).
 */
export function FilterBar({
  children,
  shown,
  total,
  noun,
  activeCount = 0,
  onClear,
  className,
}: {
  children?: ReactNode;
  shown?: number | null;
  total?: number | null;
  noun?: [string, string];
  activeCount?: number;
  onClear?: () => void;
  className?: string;
}) {
  const word = noun ? (shown === 1 ? noun[0] : noun[1]) : "";
  return (
    <Toolbar className={className}>
      <div className="flex w-full flex-wrap items-center gap-2">
        {children}
        <div className="ml-auto flex items-center gap-3 text-xs text-[var(--muted-foreground)]">
          {shown != null ? (
            <span aria-live="polite">
              {activeCount > 0 && total != null && total !== shown ? (
                <>
                  <span className="tabular font-medium text-[var(--foreground)]">{shown}</span> of{" "}
                  <span className="tabular">{total}</span> {word}
                </>
              ) : (
                <>
                  <span className="tabular font-medium text-[var(--foreground)]">{shown}</span> {word}
                </>
              )}
            </span>
          ) : null}
          {activeCount > 0 && onClear ? (
            <Btn type="button" size="sm" variant="ghost" onClick={onClear} title="Reset every filter">
              <FilterX />
              Clear filters
              <span className="tabular rounded-full bg-[var(--primary-soft)] px-1.5 text-[10px] font-semibold text-[var(--primary-soft-foreground)]">
                {activeCount}
              </span>
            </Btn>
          ) : null}
        </div>
      </div>
    </Toolbar>
  );
}

/** Empty state for "the filters hide everything", with a way out. */
export function NoResults({ noun = "results", onClear, icon }: { noun?: string; onClear?: () => void; icon?: IconType }) {
  return (
    <EmptyState
      icon={icon ?? Search}
      title={`No matching ${noun}`}
      description="Nothing matches the current search and filters."
      action={
        onClear ? (
          <Btn type="button" variant="outline" onClick={onClear}>
            <FilterX />
            Clear filters
          </Btn>
        ) : null
      }
    />
  );
}

export function Pagination({
  page,
  pageSize,
  total,
  onPage,
  onPageSize,
  pageSizes = [10, 25, 50, 100],
}: {
  page: number;
  pageSize: number;
  total: number;
  onPage: (page: number) => void;
  onPageSize?: (size: number) => void;
  pageSizes?: number[];
}) {
  const pages = Math.max(1, Math.ceil(total / pageSize));
  const current = Math.min(Math.max(1, page), pages);
  const from = total === 0 ? 0 : (current - 1) * pageSize + 1;
  const to = Math.min(current * pageSize, total);
  return (
    <div className="flex flex-wrap items-center justify-between gap-3 border-t border-[var(--border)] px-5 py-3 text-xs text-[var(--muted-foreground)]">
      <span>
        Showing <span className="tabular">{from}</span>–<span className="tabular">{to}</span> of{" "}
        <span className="tabular">{total}</span>
      </span>
      <div className="flex flex-wrap items-center gap-2">
        {onPageSize ? (
          <label className="flex items-center gap-1.5">
            Rows
            <select
              aria-label="Rows per page"
              value={pageSize}
              onChange={(e) => onPageSize(Number(e.target.value))}
              className={cn(controlBase, "h-8 w-auto px-2 text-xs")}
            >
              {pageSizes.map((n) => (
                <option key={n} value={n}>
                  {n}
                </option>
              ))}
            </select>
          </label>
        ) : null}
        <Btn type="button" size="sm" variant="outline" disabled={current <= 1} onClick={() => onPage(current - 1)} aria-label="Previous page">
          <ChevronLeft />
          <span className="hidden sm:inline">Prev</span>
        </Btn>
        <label className="flex items-center gap-1.5">
          <span className="sr-only">Page</span>
          <select
            aria-label="Page"
            value={current}
            onChange={(e) => onPage(Number(e.target.value))}
            disabled={pages <= 1}
            className={cn(controlBase, "h-8 w-auto px-2 text-xs")}
          >
            {Array.from({ length: Math.min(pages, 500) }, (_, i) => (
              <option key={i + 1} value={i + 1}>
                {i + 1}
              </option>
            ))}
          </select>
          <span>of {pages}</span>
        </label>
        <Btn type="button" size="sm" variant="outline" disabled={current >= pages} onClick={() => onPage(current + 1)} aria-label="Next page">
          <span className="hidden sm:inline">Next</span>
          <ChevronRight />
        </Btn>
      </div>
    </div>
  );
}
