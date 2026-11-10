import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { ArrowLeft, Printer } from "lucide-react";
import { Btn, EmptyState, Skeleton } from "@/components/sms-ui";
import { api, unwrap } from "@/lib/api";
import type { Receipt } from "@/lib/types";
import { dateLabel, errorText, methodLabel, monthLabel, rs } from "./shared";

/** A5 payment receipt. */
export function ReceiptPrintPage() {
  const { id } = useParams();
  const [r, setR] = useState<Receipt | null | undefined>(undefined);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    unwrap(api.get(`/fees/payments/${id}/receipt`))
      .then((x) => setR(x as Receipt))
      .catch((err) => {
        setError(errorText(err, "Could not load the receipt"));
        setR(null);
      });
  }, [id]);

  return (
    <div>
      <div className="no-print mb-4 flex flex-wrap items-center justify-between gap-3">
        <Link to="/admin/fees/collect" className="inline-flex items-center gap-1 text-sm text-[var(--muted-foreground)] hover:text-[var(--foreground)]">
          <ArrowLeft className="size-4" /> Collect fee
        </Link>
        <Btn onClick={() => window.print()} disabled={!r}>
          <Printer /> Print
        </Btn>
      </div>
      {r === undefined ? (
        <Skeleton className="mx-auto h-80 max-w-[148mm] rounded-xl" />
      ) : r === null ? (
        <div className="panel">
          <EmptyState icon={Printer} title="Receipt not found" description={error ?? undefined} />
        </div>
      ) : (
        <div className="print-area">
          <article className="receipt-slip mx-auto max-w-[148mm] rounded-xl border border-[var(--border)] bg-white p-6 text-[13px] text-neutral-900 shadow-[var(--shadow-xs)]">
            <header className="mb-4 border-b-2 border-neutral-800 pb-2 text-center">
              <div className="text-lg font-bold uppercase">{r.schoolName}</div>
              <div className="text-neutral-600">{r.campusName}</div>
              <div className="mt-1 font-semibold">Fee receipt</div>
            </header>
            {r.payment.isVoid ? <p className="mb-3 text-center font-bold text-red-700 uppercase">Void · {r.payment.voidReason}</p> : null}
            <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-1">
              <dt className="text-neutral-600">Receipt no.</dt>
              <dd className="font-mono">{r.payment.receiptNo}</dd>
              <dt className="text-neutral-600">Date</dt>
              <dd>{dateLabel(r.payment.paidOn)}</dd>
              <dt className="text-neutral-600">Student</dt>
              <dd className="font-semibold">
                {r.studentName} · {r.voucher.admissionNo}
              </dd>
              <dt className="text-neutral-600">Class</dt>
              <dd>{r.className}</dd>
              <dt className="text-neutral-600">Challan</dt>
              <dd>
                {r.voucher.voucherNo} · {monthLabel(r.voucher.month)}
              </dd>
              <dt className="text-neutral-600">Method</dt>
              <dd>
                {methodLabel[r.payment.method]}
                {r.payment.reference ? ` · ${r.payment.reference}` : ""}
              </dd>
            </dl>
            <div className="mt-4 flex items-center justify-between border-y border-neutral-700 py-2 text-base font-bold">
              <span>Amount received</span>
              <span className="tabular">{rs(r.payment.amount)}</span>
            </div>
            <div className="mt-1 flex justify-between">
              <span className="text-neutral-600">Balance on this challan</span>
              <span className="tabular">{rs(r.balanceAfter)}</span>
            </div>
            <div className="mt-8 flex justify-between text-neutral-600">
              <span>Received by: {r.payment.receivedBy}</span>
              <span>Signature</span>
            </div>
          </article>
        </div>
      )}
    </div>
  );
}
