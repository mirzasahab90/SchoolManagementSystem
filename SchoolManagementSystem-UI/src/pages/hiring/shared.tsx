import { useEffect, useRef, useState, type ReactNode } from "react";
import { MoreHorizontal, Star } from "lucide-react";
import { Modal } from "@/components/modal";
import { Btn, Field, IconButton, StatusPill, TextArea, type StatusTone } from "@/components/sms-ui";
import { cn } from "@/lib/utils";
import {
  candidateStageLabel,
  type CandidateStage,
  type InterviewResult,
  type OfferStatus,
  type StaffStatus,
  type VacancyStatus,
} from "@/lib/types";

export const formatDate = (v?: string | null) =>
  v
    ? new Date(v.length === 10 ? `${v}T00:00:00` : v).toLocaleDateString(undefined, { day: "numeric", month: "short", year: "numeric" })
    : "—";

export const formatDateTime = (v?: string | null) =>
  v
    ? new Date(v).toLocaleString(undefined, { day: "numeric", month: "short", year: "numeric", hour: "numeric", minute: "2-digit" })
    : "—";

export const formatMoney = (v?: number | null) => (v == null ? "—" : `Rs ${v.toLocaleString()}`);

export const todayIso = () => {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
};

export const errorText = (err: unknown, fallback: string) => (err instanceof Error ? err.message : fallback);

const stageTone: Record<CandidateStage, StatusTone> = {
  Applied: "neutral",
  Shortlisted: "info",
  Interview: "primary",
  Offered: "warning",
  Hired: "success",
  Rejected: "danger",
  Withdrawn: "neutral",
};

export function StagePill({ stage }: { stage: CandidateStage }) {
  return (
    <StatusPill tone={stageTone[stage]} dot>
      {candidateStageLabel[stage]}
    </StatusPill>
  );
}

export const vacancyTone: Record<VacancyStatus, StatusTone> = { Open: "success", OnHold: "warning", Closed: "neutral", Filled: "primary" };
export const resultTone: Record<InterviewResult, StatusTone> = { Scheduled: "info", Pass: "success", Fail: "danger", Hold: "warning" };
export const offerTone: Record<OfferStatus, StatusTone> = { Pending: "warning", Accepted: "success", Declined: "danger", Withdrawn: "neutral" };
export const staffTone: Record<StaffStatus, StatusTone> = {
  Onboarding: "warning",
  Active: "success",
  OnLeave: "info",
  Resigned: "neutral",
  Terminated: "danger",
};

export function Stars({ value, className }: { value?: number | null; className?: string }) {
  if (!value) return null;
  return (
    <span className={cn("inline-flex items-center gap-0.5 text-[var(--warning)]", className)} aria-label={`${value} of 5`} title={`${value} of 5`}>
      {[1, 2, 3, 4, 5].map((i) => (
        <Star key={i} className={cn("size-3.5", i <= value ? "fill-current" : "opacity-30")} />
      ))}
    </span>
  );
}

export function RatingInput({ value, onChange }: { value: number | null; onChange: (v: number) => void }) {
  return (
    <div className="flex gap-1" role="radiogroup" aria-label="Rating">
      {[1, 2, 3, 4, 5].map((i) => (
        <button
          key={i}
          type="button"
          role="radio"
          aria-checked={value === i}
          aria-label={`${i} star${i > 1 ? "s" : ""}`}
          onClick={() => onChange(i)}
          className="rounded-md p-1 text-[var(--warning)] outline-none hover:bg-[var(--muted)] focus-visible:ring-4 focus-visible:ring-[var(--ring)]"
        >
          <Star className={cn("size-6", value != null && i <= value ? "fill-current" : "opacity-40")} />
        </button>
      ))}
    </div>
  );
}

export type MenuItemDef = { label: string; onSelect: () => void; danger?: boolean; disabled?: boolean };

/** Small "more actions" dropdown. */
export function ActionMenu({ items, label = "More actions", trigger }: { items: MenuItemDef[]; label?: string; trigger?: ReactNode }) {
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    const onDown = (e: MouseEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false);
    };
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && setOpen(false);
    document.addEventListener("mousedown", onDown);
    document.addEventListener("keydown", onKey);
    return () => {
      document.removeEventListener("mousedown", onDown);
      document.removeEventListener("keydown", onKey);
    };
  }, [open]);

  if (items.length === 0) return null;
  return (
    <div ref={ref} className="relative" onClick={(e) => e.stopPropagation()}>
      {trigger ? (
        <span onClick={() => setOpen((o) => !o)}>{trigger}</span>
      ) : (
        <IconButton label={label} onClick={() => setOpen((o) => !o)} aria-haspopup="menu" aria-expanded={open}>
          <MoreHorizontal />
        </IconButton>
      )}
      {open ? (
        <div
          role="menu"
          className="animate-pop absolute right-0 z-30 mt-1 min-w-44 overflow-hidden rounded-lg border border-[var(--border)] bg-[var(--card)] py-1 shadow-[var(--shadow-lg)]"
        >
          {items.map((item) => (
            <button
              key={item.label}
              type="button"
              role="menuitem"
              disabled={item.disabled}
              onClick={() => {
                setOpen(false);
                item.onSelect();
              }}
              className={cn(
                "block w-full px-3 py-2 text-left text-sm hover:bg-[var(--muted)] disabled:opacity-40",
                item.danger ? "text-[var(--danger)]" : "text-[var(--foreground)]",
              )}
            >
              {item.label}
            </button>
          ))}
        </div>
      ) : null}
    </div>
  );
}

/** Asks for the reason a reject / withdraw / reopen needs. */
export function ReasonDialog({
  title,
  description,
  confirmLabel,
  danger,
  onClose,
  onConfirm,
}: {
  title: string;
  description?: string;
  confirmLabel: string;
  danger?: boolean;
  onClose: () => void;
  onConfirm: (reason: string) => Promise<void>;
}) {
  const [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false);
  return (
    <Modal
      open
      title={title}
      description={description}
      onClose={onClose}
      footer={
        <>
          <Btn type="button" variant="outline" onClick={onClose}>
            Cancel
          </Btn>
          <Btn
            type="button"
            variant={danger ? "danger" : "primary"}
            disabled={busy || reason.trim().length === 0}
            onClick={async () => {
              setBusy(true);
              try {
                await onConfirm(reason.trim());
              } finally {
                setBusy(false);
              }
            }}
          >
            {busy ? "Saving…" : confirmLabel}
          </Btn>
        </>
      }
    >
      <Field label="Reason" required>
        <TextArea rows={3} maxLength={500} value={reason} onChange={(e) => setReason(e.target.value)} autoFocus />
      </Field>
    </Modal>
  );
}
