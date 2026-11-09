import { useEffect, type ReactNode } from "react";
import { X } from "lucide-react";
import { IconButton } from "@/components/sms-ui";
import { cn } from "@/lib/utils";

export function Modal({
  open,
  title,
  description,
  onClose,
  children,
  footer,
  wide = false,
}: {
  open: boolean;
  title: string;
  description?: string;
  onClose: () => void;
  children?: ReactNode;
  footer?: ReactNode;
  wide?: boolean;
}) {
  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") onClose();
    };
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    window.addEventListener("keydown", onKey);
    return () => {
      document.body.style.overflow = previousOverflow;
      window.removeEventListener("keydown", onKey);
    };
  }, [open, onClose]);

  if (!open) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-end justify-center sm:items-center sm:p-4">
      <button
        type="button"
        className="animate-fade absolute inset-0 bg-[var(--overlay)] backdrop-blur-[2px]"
        aria-label="Close dialog"
        onClick={onClose}
      />
      <div
        role="dialog"
        aria-modal="true"
        aria-label={title}
        className={cn(
          "animate-pop relative z-10 flex max-h-[92vh] w-full flex-col overflow-hidden rounded-t-2xl border border-[var(--border)] bg-[var(--card)] shadow-[var(--shadow-lg)] sm:rounded-2xl",
          wide ? "sm:max-w-2xl" : "sm:max-w-md",
        )}
      >
        <div className="flex items-start justify-between gap-4 px-6 pt-5 pb-4">
          <div className="min-w-0">
            <h2 className="text-base font-semibold">{title}</h2>
            {description ? <p className="mt-1 text-sm text-[var(--muted-foreground)]">{description}</p> : null}
          </div>
          <IconButton label="Close" onClick={onClose} className="-mt-1 -mr-2">
            <X />
          </IconButton>
        </div>
        {children ? <div className="flex-1 overflow-y-auto px-6 pb-6">{children}</div> : <div className="pb-1" />}
        {footer ? (
          <div className="flex justify-end gap-2 border-t border-[var(--border)] bg-[var(--muted)]/50 px-6 py-3.5">
            {footer}
          </div>
        ) : null}
      </div>
    </div>
  );
}

/** Small confirm dialog to replace window.confirm for destructive actions. */
export function ConfirmDialog({
  open,
  title,
  description,
  confirmLabel = "Delete",
  busy,
  onConfirm,
  onClose,
}: {
  open: boolean;
  title: string;
  description: string;
  confirmLabel?: string;
  busy?: boolean;
  onConfirm: () => void;
  onClose: () => void;
}) {
  return (
    <Modal
      open={open}
      title={title}
      description={description}
      onClose={onClose}
      footer={
        <>
          <button
            type="button"
            onClick={onClose}
            className="inline-flex h-9 items-center rounded-lg border border-[var(--border)] bg-[var(--card)] px-3.5 text-sm font-medium hover:bg-[var(--muted)]"
          >
            Cancel
          </button>
          <button
            type="button"
            onClick={onConfirm}
            disabled={busy}
            className="inline-flex h-9 items-center rounded-lg bg-[var(--danger)] px-3.5 text-sm font-medium text-white hover:opacity-90 disabled:opacity-50"
          >
            {busy ? "Working…" : confirmLabel}
          </button>
        </>
      }
    />
  );
}
