import { useCallback, useEffect, useState } from "react";
import { toast } from "sonner";
import { StatusPill, type StatusTone } from "@/components/sms-ui";
import { api, unwrap } from "@/lib/api";
import { cn } from "@/lib/utils";
import type { FeeHead, FeeSettings, PaymentMethod, VoucherStatus } from "@/lib/types";
import { voucherStatusLabel } from "@/lib/types";
import { errorText } from "@/pages/hiring/shared";

export { errorText };

const money = new Intl.NumberFormat("en-PK", { maximumFractionDigits: 2 });

/** "Rs 1,234" (negative as "-Rs 500"). */
export const rs = (v?: number | null) => (v == null ? "—" : `${v < 0 ? "-" : ""}Rs ${money.format(Math.abs(v))}`);

export const MONTHS = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];
export const MONTHS_LONG = ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"];

/** "2026-11-01" -> "Nov 2026". */
export const monthLabel = (iso?: string | null) => {
  if (!iso) return "—";
  const [y, m] = iso.split("-").map(Number);
  return `${MONTHS[m - 1]} ${y}`;
};

/** "2026-11-10" -> "10 Nov 2026". */
export const dateLabel = (iso?: string | null) => {
  if (!iso) return "—";
  const [y, m, d] = iso.split("-").map(Number);
  return `${d} ${MONTHS[m - 1]} ${y}`;
};

const pad = (n: number) => String(n).padStart(2, "0");
export const isoDate = (d: Date) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
export const todayIso = () => isoDate(new Date());
/** First day of this month, "YYYY-MM-01". */
export const thisMonthIso = () => {
  const d = new Date();
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-01`;
};
/** "YYYY-MM" (month input) <-> "YYYY-MM-01". */
export const toMonthInput = (iso: string) => iso.slice(0, 7);
export const fromMonthInput = (v: string) => (v ? `${v}-01` : "");

export const voucherTone: Record<VoucherStatus, StatusTone> = {
  Unpaid: "warning",
  PartiallyPaid: "info",
  Paid: "success",
  CarriedForward: "neutral",
  Cancelled: "danger",
};

export function VoucherStatusPill({ status }: { status: VoucherStatus }) {
  return (
    <StatusPill tone={voucherTone[status]} dot>
      {voucherStatusLabel[status]}
    </StatusPill>
  );
}

export const methodLabel: Record<PaymentMethod, string> = { Cash: "Cash", Bank: "Bank", Online: "Online / transfer" };

export function useFeeHeads() {
  const [heads, setHeads] = useState<FeeHead[] | null>(null);
  const load = useCallback(async () => {
    try {
      setHeads((await unwrap(api.get("/fees/heads"))) as FeeHead[]);
    } catch (err) {
      toast.error(errorText(err, "Could not load fee heads"));
      setHeads([]);
    }
  }, []);
  useEffect(() => {
    void load();
  }, [load]);
  return { heads, reload: load };
}

export function useFeeSettings() {
  const [settings, setSettings] = useState<FeeSettings | null>(null);
  const load = useCallback(async () => {
    try {
      setSettings((await unwrap(api.get("/fees/settings"))) as FeeSettings);
    } catch (err) {
      toast.error(errorText(err, "Could not load fee settings"));
    }
  }, []);
  useEffect(() => {
    void load();
  }, [load]);
  return { settings, reload: load, setSettings };
}

/** Underlined tab strip used across the fee pages. */
export function Tabs<T extends string>({ tabs, value, onChange }: { tabs: [T, string][]; value: T; onChange: (v: T) => void }) {
  return (
    <div role="tablist" className="mb-4 flex gap-1 overflow-x-auto overflow-y-hidden border-b border-[var(--border)]">
      {tabs.map(([key, label]) => (
        <button
          key={key}
          type="button"
          role="tab"
          aria-selected={value === key}
          onClick={() => onChange(key)}
          className={cn(
            "-mb-px shrink-0 border-b-2 px-3 py-2 text-sm font-medium transition-colors",
            value === key ? "border-[var(--primary)] text-[var(--foreground)]" : "border-transparent text-[var(--muted-foreground)] hover:text-[var(--foreground)]",
          )}
        >
          {label}
        </button>
      ))}
    </div>
  );
}

/** Pick a campus first: fee data belongs to one campus. */
export function useNeedsCampus(user: { isHq?: boolean; activeCampusId?: number | null } | null) {
  return user?.activeCampusId == null;
}
