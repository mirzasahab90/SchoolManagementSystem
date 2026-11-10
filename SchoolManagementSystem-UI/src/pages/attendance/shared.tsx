import { cn } from "@/lib/utils";
import { attendanceCode, attendanceLabel, type StaffAttendanceStatus } from "@/lib/types";

/** "Today" as the server counts it (UTC), so the default date is never in the server's future. */
export const isoToday = () => new Date().toISOString().slice(0, 10);

export const thisMonth = () => isoToday().slice(0, 7);

export const pctText = (v?: number | null) => (v == null ? "—" : `${v.toFixed(1)}%`);

const statusColor: Record<StaffAttendanceStatus, string> = {
  Present: "bg-[var(--success)] text-white border-[var(--success)]",
  Absent: "bg-[var(--danger)] text-white border-[var(--danger)]",
  Late: "bg-[var(--warning)] text-white border-[var(--warning)]",
  Leave: "bg-[var(--info)] text-white border-[var(--info)]",
  HalfDay: "bg-[var(--primary)] text-[var(--primary-foreground)] border-[var(--primary)]",
};

export const cellTone: Record<string, string> = {
  P: "text-[var(--success)]",
  A: "text-[var(--danger)] font-semibold",
  L: "text-[var(--warning)]",
  Lv: "text-[var(--info)]",
  H: "text-[var(--primary)]",
};

/** P / A / L / Lv (/ ½) segmented buttons. */
export function StatusButtons<T extends StaffAttendanceStatus>({
  value,
  options,
  onChange,
  disabled,
  name,
}: {
  value: T | null | undefined;
  options: T[];
  onChange: (v: T) => void;
  disabled?: boolean;
  name: string;
}) {
  return (
    <div role="radiogroup" aria-label={`Status for ${name}`} className="inline-flex gap-1">
      {options.map((o) => (
        <button
          key={o}
          type="button"
          role="radio"
          aria-checked={value === o}
          title={attendanceLabel[o]}
          disabled={disabled}
          onClick={() => onChange(o)}
          className={cn(
            "h-8 min-w-9 rounded-md border px-2 text-xs font-semibold transition outline-none focus-visible:ring-4 focus-visible:ring-[var(--ring)] disabled:cursor-not-allowed disabled:opacity-50",
            value === o ? statusColor[o] : "border-[var(--border)] bg-[var(--card)] text-[var(--muted-foreground)] hover:border-[var(--border-strong)]",
          )}
        >
          {attendanceCode[o]}
        </button>
      ))}
    </div>
  );
}
