import { useEffect, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { ArrowLeft, Printer } from "lucide-react";
import { Btn, EmptyState, Skeleton } from "@/components/sms-ui";
import { api, unwrap } from "@/lib/api";
import type { VoucherPrint } from "@/lib/types";
import { dateLabel, errorText, monthLabel, rs } from "./shared";

const COPIES = ["Bank copy", "School copy", "Parent copy"];

/** Print page: one A4 landscape sheet per challan with bank, school and parent copies side by side. */
export function ChallanPrintPage() {
  const [params] = useSearchParams();
  const [items, setItems] = useState<VoucherPrint[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const qs = new URLSearchParams();
    for (const k of ["ids", "month", "sectionId"]) {
      const v = params.get(k);
      if (v) qs.set(k, v);
    }
    unwrap(api.get(`/fees/vouchers/print?${qs}`))
      .then((r) => setItems(r as VoucherPrint[]))
      .catch((err) => {
        setError(errorText(err, "Could not load the challans"));
        setItems([]);
      });
  }, [params]);

  return (
    <div>
      <div className="no-print mb-4 flex flex-wrap items-center justify-between gap-3">
        <Link to="/admin/fees/challans" className="inline-flex items-center gap-1 text-sm text-[var(--muted-foreground)] hover:text-[var(--foreground)]">
          <ArrowLeft className="size-4" /> Challans
        </Link>
        <div className="flex items-center gap-3">
          {items?.length ? <span className="text-sm text-[var(--muted-foreground)]">{items.length} challans · A4 landscape, one per page</span> : null}
          <Btn onClick={() => window.print()} disabled={!items?.length}>
            <Printer /> Print
          </Btn>
        </div>
      </div>

      {items == null ? (
        <Skeleton className="h-[420px] rounded-xl" />
      ) : items.length === 0 ? (
        <div className="panel">
          <EmptyState icon={Printer} title="No challans" description={error ?? "There is nothing to print for this selection."} />
        </div>
      ) : (
        <div className="print-area challan-sheets space-y-6">
          {items.map((it) => (
            <article key={it.voucher.id} className="challan-sheet overflow-x-auto rounded-xl border border-[var(--border)] bg-white p-4 text-neutral-900 shadow-[var(--shadow-xs)]">
              <div className="grid min-w-[900px] grid-cols-3 divide-x divide-dashed divide-neutral-400">
                {COPIES.map((copy) => (
                  <Copy key={copy} copy={copy} it={it} />
                ))}
              </div>
            </article>
          ))}
        </div>
      )}
    </div>
  );
}

function Copy({ copy, it }: { copy: string; it: VoucherPrint }) {
  const v = it.voucher;
  const open = v.status === "Unpaid" || v.status === "PartiallyPaid";
  const afterDue = open && !v.lateFineApplied ? v.payableAfterDue : v.balance;
  return (
    <section className="px-3 text-[11px] leading-snug">
      <div className="mb-1 flex items-center justify-between">
        <span className="rounded border border-neutral-500 px-1.5 py-0.5 text-[10px] font-semibold uppercase">{copy}</span>
        <span className="font-mono">{v.voucherNo}</span>
      </div>
      <header className="mb-2 border-b border-neutral-700 pb-1 text-center">
        <div className="text-[13px] font-bold uppercase">{it.schoolName}</div>
        <div>{it.campusName}</div>
        {it.campusAddress ? <div className="text-neutral-600">{it.campusAddress}</div> : null}
      </header>
      {it.bankName || it.accountNo ? (
        <div className="mb-2 rounded border border-neutral-300 p-1.5">
          <div>
            <b>Bank:</b> {it.bankName ?? "—"}
          </div>
          <div>
            <b>Account:</b> {it.accountTitle ?? "—"} · {it.accountNo ?? "—"}
          </div>
        </div>
      ) : null}
      <dl className="mb-2 grid grid-cols-[auto_1fr] gap-x-2">
        <dt className="text-neutral-600">Student</dt>
        <dd className="font-semibold">{v.studentName}</dd>
        <dt className="text-neutral-600">Admission no.</dt>
        <dd>{v.admissionNo}</dd>
        {it.guardianName ? (
          <>
            <dt className="text-neutral-600">Guardian</dt>
            <dd>{it.guardianName}</dd>
          </>
        ) : null}
        <dt className="text-neutral-600">Class</dt>
        <dd>
          {v.className} – {v.sectionName} · Roll {v.rollNo}
        </dd>
        <dt className="text-neutral-600">Month</dt>
        <dd>{monthLabel(v.month)}</dd>
        <dt className="text-neutral-600">Issued</dt>
        <dd>{dateLabel(v.issueDate)}</dd>
      </dl>
      <table className="mb-2 w-full border-collapse">
        <thead>
          <tr className="border-y border-neutral-500">
            <th className="py-0.5 text-left">Description</th>
            <th className="py-0.5 text-right">Rs</th>
          </tr>
        </thead>
        <tbody>
          {it.lines.map((l) => (
            <tr key={l.id} className="border-b border-neutral-200">
              <td className="py-0.5">
                {l.description}
                {l.concession ? <span className="text-neutral-600"> (−{rs(l.concession)} {l.concessionNote})</span> : null}
              </td>
              <td className="tabular py-0.5 text-right">{rs(l.net).replace("Rs ", "")}</td>
            </tr>
          ))}
          {v.paid ? (
            <tr>
              <td className="py-0.5">Paid so far</td>
              <td className="tabular py-0.5 text-right">−{rs(v.paid).replace("Rs ", "")}</td>
            </tr>
          ) : null}
        </tbody>
      </table>
      <div className="space-y-0.5 border-t border-neutral-700 pt-1 font-semibold">
        <div className="flex justify-between">
          <span>Payable by {dateLabel(v.dueDate)}</span>
          <span className="tabular">{rs(v.balance)}</span>
        </div>
        {afterDue !== v.balance ? (
          <div className="flex justify-between">
            <span>Payable after {dateLabel(v.dueDate)}</span>
            <span className="tabular">{rs(afterDue)}</span>
          </div>
        ) : null}
      </div>
      {it.challanNote ? <p className="mt-2 text-neutral-600">{it.challanNote}</p> : null}
      <div className="mt-6 flex justify-between text-neutral-600">
        <span>Cashier / Bank stamp</span>
        <span>Signature</span>
      </div>
    </section>
  );
}
