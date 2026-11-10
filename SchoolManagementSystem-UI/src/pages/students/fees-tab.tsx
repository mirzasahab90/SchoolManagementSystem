import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { Printer, Wallet } from "lucide-react";
import { DataTable, EmptyState, Panel, Skeleton, StatusPill, Td, THead, Tr } from "@/components/sms-ui";
import { api, unwrap } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import type { StudentFees } from "@/lib/types";
import { VoucherStatusPill, dateLabel, methodLabel, monthLabel, rs } from "@/pages/fees/shared";

/** A student's challans and payments, newest first, with the current balance. */
export function StudentFeesTab({ studentId }: { studentId: number }) {
  const { can } = useAuth();
  const [data, setData] = useState<StudentFees | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    unwrap(api.get(`/students/${studentId}/fees`))
      .then((d) => setData(d as StudentFees))
      .catch((err) => setError(err instanceof Error ? err.message : "Could not load fees"));
  }, [studentId]);

  if (error) return <p className="text-sm text-[var(--muted-foreground)]">{error}</p>;
  if (!data) return <Skeleton className="h-48 rounded-xl" />;
  const open = data.vouchers.find((v) => v.status === "Unpaid" || v.status === "PartiallyPaid");

  return (
    <div className="grid gap-4 lg:grid-cols-3">
      <Panel title="Balance">
        <div className="text-4xl font-semibold tabular-nums">{rs(data.balance)}</div>
        <div className="mt-2 text-sm text-[var(--muted-foreground)]">
          {open ? `Challan ${open.voucherNo} · due ${dateLabel(open.dueDate)}` : "No open challan"}
        </div>
        {open && can("fee-collection", "Add") ? (
          <Link to={`/admin/fees/collect?q=${encodeURIComponent(open.voucherNo)}`} className="mt-3 inline-block text-sm text-[var(--primary)]">
            Collect fee
          </Link>
        ) : null}
      </Panel>

      <Panel flush title="Challans" className="lg:col-span-2">
        {data.vouchers.length === 0 ? (
          <EmptyState icon={Wallet} title="No challans yet" />
        ) : (
          <DataTable>
            <THead columns={[{ label: "Month" }, { label: "Challan" }, { label: "Total", align: "right" }, { label: "Balance", align: "right" }, { label: "Status" }, { label: "", align: "right" }]} />
            <tbody>
              {data.vouchers.map((v) => (
                <Tr key={v.id}>
                  <Td>{monthLabel(v.month)}</Td>
                  <Td className="font-mono text-xs">{v.voucherNo}</Td>
                  <Td className="tabular text-right">{rs(v.total)}</Td>
                  <Td className="tabular text-right">{rs(v.balance)}</Td>
                  <Td>
                    <VoucherStatusPill status={v.status} />
                  </Td>
                  <Td className="text-right">
                    {v.status !== "Cancelled" ? (
                      <Link to={`/admin/fees/challans/print?ids=${v.id}`} aria-label={`Print ${v.voucherNo}`} className="text-[var(--muted-foreground)] hover:text-[var(--foreground)]">
                        <Printer className="inline size-4" />
                      </Link>
                    ) : null}
                  </Td>
                </Tr>
              ))}
            </tbody>
          </DataTable>
        )}
      </Panel>

      <Panel flush title="Payments" className="lg:col-span-3">
        {data.payments.length === 0 ? (
          <EmptyState icon={Wallet} title="No payments yet" />
        ) : (
          <DataTable>
            <THead columns={[{ label: "Receipt" }, { label: "Date" }, { label: "Challan" }, { label: "Method" }, { label: "Amount", align: "right" }]} />
            <tbody>
              {data.payments.map((p) => (
                <Tr key={p.id}>
                  <Td>
                    <Link to={`/admin/fees/receipt/${p.id}`} className="font-mono text-xs text-[var(--primary)]">
                      {p.receiptNo}
                    </Link>
                    {p.isVoid ? <StatusPill tone="danger" className="ml-2">Void</StatusPill> : null}
                  </Td>
                  <Td>{dateLabel(p.paidOn)}</Td>
                  <Td className="font-mono text-xs">{p.voucherNo}</Td>
                  <Td>{methodLabel[p.method]}</Td>
                  <Td className={`tabular text-right ${p.isVoid ? "text-[var(--muted-foreground)] line-through" : ""}`}>{rs(p.amount)}</Td>
                </Tr>
              ))}
            </tbody>
          </DataTable>
        )}
      </Panel>
    </div>
  );
}
