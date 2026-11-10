import { useCallback, useEffect, useRef, useState } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { HandCoins, Printer, Receipt, Search, Undo2 } from "lucide-react";
import { toast } from "sonner";
import { Modal } from "@/components/modal";
import { Btn, DataTable, EmptyState, Field, PageHeader, Panel, Select, StatusPill, TableSkeleton, Td, TextInput, THead, Tr } from "@/components/sms-ui";
import { api, unwrap } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import { cn } from "@/lib/utils";
import type { CollectLookup, FeePayment, PaymentMethod } from "@/lib/types";
import { VoucherStatusPill, dateLabel, errorText, methodLabel, monthLabel, rs, todayIso } from "./shared";

export function CollectFeePage() {
  const { user, can } = useAuth();
  const navigate = useNavigate();
  const [params, setParams] = useSearchParams();
  const [q, setQ] = useState(params.get("q") ?? "");
  const [results, setResults] = useState<CollectLookup[] | null>(null);
  const [picked, setPicked] = useState<CollectLookup | null>(null);
  const [today, setToday] = useState<FeePayment[] | null>(null);
  const [voiding, setVoiding] = useState<FeePayment | null>(null);
  const input = useRef<HTMLInputElement>(null);
  const noCampus = user?.activeCampusId == null;

  const loadToday = useCallback(async () => {
    try {
      setToday((await unwrap(api.get("/fees/payments/today"))) as FeePayment[]);
    } catch {
      setToday([]);
    }
  }, []);

  const search = useCallback(
    async (text: string, keep?: number) => {
      const t = text.trim();
      if (t.length < 2) return;
      setParams({ q: t }, { replace: true });
      try {
        const r = (await unwrap(api.get(`/fees/collect/lookup?q=${encodeURIComponent(t)}`))) as CollectLookup[];
        setResults(r);
        setPicked(keep ? (r.find((x) => x.studentId === keep) ?? null) : r.length === 1 ? r[0] : null);
      } catch (err) {
        toast.error(errorText(err, "Search failed"));
      }
    },
    [setParams],
  );

  useEffect(() => {
    if (noCampus) return;
    void loadToday();
    const initial = params.get("q");
    if (initial) void search(initial);
    input.current?.focus();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [noCampus]);

  if (noCampus)
    return (
      <div>
        <PageHeader title="Collect fee" />
        <div className="panel">
          <EmptyState icon={HandCoins} title="Pick a campus" description="Fees are collected for one campus. Choose a campus from the switcher at the top." />
        </div>
      </div>
    );

  const todayTotal = (today ?? []).filter((p) => !p.isVoid).reduce((s, p) => s + p.amount, 0);

  return (
    <div>
      <PageHeader title="Collect fee" description="Find the student or challan, take the payment and print the receipt. Bank-paid challans are entered here too." />
      <div className="grid gap-4 xl:grid-cols-[minmax(0,1fr)_minmax(0,1.2fr)]">
        <Panel title="Find">
          <form
            className="flex gap-2"
            onSubmit={(e) => {
              e.preventDefault();
              void search(q);
            }}
          >
            <TextInput ref={input} value={q} onChange={(e) => setQ(e.target.value)} placeholder="Admission no., challan no. or name" aria-label="Search student or challan" />
            <Btn type="submit" disabled={q.trim().length < 2}>
              <Search /> Find
            </Btn>
          </form>
          {results && results.length !== 1 ? (
            results.length === 0 ? (
              <p className="mt-3 text-sm text-[var(--muted-foreground)]">No student found.</p>
            ) : (
              <ul className="mt-3 divide-y divide-[var(--border)] rounded-lg border border-[var(--border)]">
                {results.map((r) => (
                  <li key={r.studentId}>
                    <button
                      type="button"
                      onClick={() => setPicked(r)}
                      className={cn("flex w-full items-center justify-between gap-2 px-3 py-2 text-left text-sm hover:bg-[var(--muted)]",
                        picked?.studentId === r.studentId && "bg-[var(--primary-soft)]")}
                    >
                      <span>
                        <span className="font-medium">{r.name}</span>
                        <span className="text-[var(--muted-foreground)]">
                          {" "}
                          · {r.admissionNo} · {r.className} – {r.sectionName}
                        </span>
                      </span>
                      <span className="tabular text-xs">{r.openVoucher ? rs(r.openVoucher.voucher.balance) : "No dues"}</span>
                    </button>
                  </li>
                ))}
              </ul>
            )
          ) : null}
          {picked ? <PayForm key={`${picked.studentId}-${picked.openVoucher?.voucher.id}-${picked.openVoucher?.voucher.paid}`} s={picked}
            canPay={can("fee-collection", "Add")}
            onPaid={async (paymentId) => {
              await Promise.all([search(q, picked.studentId), loadToday()]);
              navigate(`/admin/fees/receipt/${paymentId}`);
            }} /> : null}
        </Panel>

        <Panel flush title="Today's receipts" description={today ? `${today.filter((p) => !p.isVoid).length} receipts · ${rs(todayTotal)}` : undefined}>
          {today == null ? (
            <TableSkeleton cols={4} />
          ) : today.length === 0 ? (
            <EmptyState icon={Receipt} title="No receipts yet today" />
          ) : (
            <DataTable>
              <THead columns={[{ label: "Receipt" }, { label: "Method" }, { label: "Amount", align: "right" }, { label: "", align: "right" }]} />
              <tbody>
                {today.map((p) => (
                  <Tr key={p.id}>
                    <Td>
                      <Link to={`/admin/fees/receipt/${p.id}`} className={cn("font-mono text-xs text-[var(--primary)]", p.isVoid && "line-through")}>
                        {p.receiptNo}
                      </Link>
                      <div className="text-xs text-[var(--muted-foreground)]">
                        {p.voucherNo} · {p.receivedBy}
                      </div>
                    </Td>
                    <Td>{p.isVoid ? <StatusPill tone="danger">Void</StatusPill> : methodLabel[p.method]}</Td>
                    <Td className={cn("tabular text-right", p.isVoid && "text-[var(--muted-foreground)] line-through")}>{rs(p.amount)}</Td>
                    <Td className="text-right">
                      {!p.isVoid && can("fee-collection", "Delete") ? (
                        <Btn size="sm" variant="ghost" onClick={() => setVoiding(p)}>
                          <Undo2 /> Void
                        </Btn>
                      ) : null}
                    </Td>
                  </Tr>
                ))}
              </tbody>
            </DataTable>
          )}
        </Panel>
      </div>

      {voiding ? (
        <VoidDialog
          payment={voiding}
          onClose={() => setVoiding(null)}
          onDone={async () => {
            setVoiding(null);
            await Promise.all([loadToday(), picked ? search(q, picked.studentId) : Promise.resolve()]);
          }}
        />
      ) : null}
    </div>
  );
}

function PayForm({ s, canPay, onPaid }: { s: CollectLookup; canPay: boolean; onPaid: (paymentId: number) => Promise<void> }) {
  const d = s.openVoucher;
  const v = d?.voucher;
  const due = v ? v.balance + s.fineIfPaidToday : 0;
  const [amount, setAmount] = useState(due ? String(due) : "");
  const [paidOn, setPaidOn] = useState(todayIso());
  const [method, setMethod] = useState<PaymentMethod>("Cash");
  const [reference, setReference] = useState("");
  const [busy, setBusy] = useState(false);

  const pay = async () => {
    if (!v) return;
    setBusy(true);
    try {
      const p = (await unwrap(api.post("/fees/payments", { voucherId: v.id, amount: Number(amount), paidOn, method, reference: reference.trim() || null }))) as FeePayment;
      toast.success(`Received ${rs(p.amount)} · ${p.receiptNo}`);
      await onPaid(p.id);
    } catch (err) {
      toast.error(errorText(err, "Could not save the payment"));
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="mt-4 space-y-3 border-t border-[var(--border)] pt-4 text-sm">
      <div>
        <div className="flex flex-wrap items-center gap-2">
          <span className="text-base font-semibold">{s.name}</span>
          {s.studentLeft ? <StatusPill tone="neutral">Left</StatusPill> : null}
        </div>
        <div className="text-[var(--muted-foreground)]">
          {s.admissionNo} · {s.className} – {s.sectionName}
          {s.guardianName ? ` · ${s.guardianName}` : ""}
          {s.guardianPhone ? ` · ${s.guardianPhone}` : ""}
        </div>
      </div>
      {!d || !v ? (
        <p className="rounded-lg bg-[var(--muted)] p-3">No open challan. Nothing to collect.</p>
      ) : (
        <>
          <div className="rounded-lg border border-[var(--border)] p-3">
            <div className="mb-1 flex flex-wrap items-center justify-between gap-2">
              <span className="font-mono text-xs">
                {v.voucherNo} · {monthLabel(v.month)}
              </span>
              <VoucherStatusPill status={v.status} />
            </div>
            {d.lines.map((l) => (
              <div key={l.id} className="flex justify-between gap-2">
                <span>{l.description}</span>
                <span className="tabular">{rs(l.net)}</span>
              </div>
            ))}
            {v.paid ? (
              <div className="flex justify-between gap-2 text-[var(--muted-foreground)]">
                <span>Paid</span>
                <span className="tabular">−{rs(v.paid)}</span>
              </div>
            ) : null}
            <div className="mt-1 flex justify-between gap-2 border-t border-[var(--border)] pt-1 font-semibold">
              <span>Balance · due {dateLabel(v.dueDate)}</span>
              <span className="tabular">{rs(v.balance)}</span>
            </div>
            {s.fineIfPaidToday ? (
              <p className="mt-1 text-xs text-[var(--warning)]">Past the due date: a late fine of {rs(s.fineIfPaidToday)} is added when you take the payment.</p>
            ) : null}
          </div>
          {canPay ? (
            <div className="grid gap-3 sm:grid-cols-2">
              <Field label="Amount (Rs)" hint={`Up to ${rs(due)}`}>
                <TextInput type="number" min={1} max={due} value={amount} onChange={(e) => setAmount(e.target.value)} />
              </Field>
              <Field label="Paid on">
                <TextInput type="date" max={todayIso()} value={paidOn} onChange={(e) => setPaidOn(e.target.value)} />
              </Field>
              <Field label="Method">
                <Select value={method} onChange={(e) => setMethod(e.target.value as PaymentMethod)}>
                  {(Object.keys(methodLabel) as PaymentMethod[]).map((m) => (
                    <option key={m} value={m}>
                      {methodLabel[m]}
                    </option>
                  ))}
                </Select>
              </Field>
              {method !== "Cash" ? (
                <Field label="Reference" hint="Bank branch or transaction id">
                  <TextInput maxLength={60} value={reference} onChange={(e) => setReference(e.target.value)} />
                </Field>
              ) : null}
              <div className="flex items-end justify-end sm:col-span-2">
                <Btn disabled={busy || !(Number(amount) > 0) || Number(amount) > due} onClick={() => void pay()}>
                  <Printer /> Receive and print receipt
                </Btn>
              </div>
            </div>
          ) : null}
        </>
      )}
    </div>
  );
}

function VoidDialog({ payment, onClose, onDone }: { payment: FeePayment; onClose: () => void; onDone: () => Promise<void> }) {
  const [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false);
  const run = async () => {
    setBusy(true);
    try {
      await unwrap(api.post(`/fees/payments/${payment.id}/void`, { reason: reason.trim() }));
      toast.success(`${payment.receiptNo} voided`);
      await onDone();
    } catch (err) {
      toast.error(errorText(err, "Could not void"));
    } finally {
      setBusy(false);
    }
  };
  return (
    <Modal
      open
      title={`Void ${payment.receiptNo}?`}
      description={`${rs(payment.amount)} comes off the challan. The receipt stays on record, marked void.`}
      onClose={onClose}
      footer={
        <>
          <Btn variant="outline" onClick={onClose}>
            Keep
          </Btn>
          <Btn variant="danger" disabled={busy || !reason.trim()} onClick={() => void run()}>
            Void payment
          </Btn>
        </>
      }
    >
      <Field label="Reason" required>
        <TextInput autoFocus maxLength={200} value={reason} onChange={(e) => setReason(e.target.value)} placeholder="e.g. Entered twice" />
      </Field>
    </Modal>
  );
}
