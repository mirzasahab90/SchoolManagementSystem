import { useCallback, useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { Download, Printer, UserX, Wallet } from "lucide-react";
import { toast } from "sonner";
import {
  Btn,
  DataTable,
  EmptyState,
  FilterBar,
  NoResults,
  PageHeader,
  Pagination,
  Panel,
  Select,
  StatusPill,
  TableSkeleton,
  Td,
  TextInput,
  THead,
  Tr,
} from "@/components/sms-ui";
import { api, downloadBlob, saveBlob, unwrap } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import { useUrlFilters } from "@/lib/filters";
import type { CampusTotal, CollectionReport, Defaulters, HeadReport, FeeMonthlyReport, SchoolClass, Section } from "@/lib/types";
import { Tabs, dateLabel, errorText, fromMonthInput, methodLabel, monthLabel, rs, thisMonthIso, toMonthInput, todayIso } from "./shared";

type Tab = "defaulters" | "collection" | "monthly" | "heads";

export function FeeReportsPage() {
  const { values, set } = useUrlFilters({ tab: "defaulters" });
  const tab = values.tab as Tab;
  return (
    <div>
      <PageHeader title="Fee reports" description="Who owes, what came in and how each month and head is doing." />
      <Tabs
        tabs={[
          ["defaulters", "Defaulters"],
          ["collection", "Daily collection"],
          ["monthly", "Monthly summary"],
          ["heads", "Head-wise"],
        ]}
        value={tab}
        onChange={(t) => set({ tab: t })}
      />
      {tab === "collection" ? <CollectionTab /> : tab === "monthly" ? <MonthlyTab /> : tab === "heads" ? <HeadsTab /> : <DefaultersTab />}
    </div>
  );
}

async function csv(url: string, name: string) {
  try {
    const { blob, fileName } = await downloadBlob(url);
    saveBlob(blob, fileName ?? name);
  } catch (err) {
    toast.error(errorText(err, "Could not download"));
  }
}

// ---------------------------------------------------------------- defaulters

function DefaultersTab() {
  const { user, can } = useAuth();
  const { values: f, set, clear, activeCount } = useUrlFilters({ tab: "defaulters", classId: "", sectionId: "", minMonths: "", minAmount: "", page: "1", pageSize: "50" });
  const page = Number(f.page) || 1;
  const pageSize = Number(f.pageSize) || 50;
  const [data, setData] = useState<Defaulters | null>(null);
  const [classes, setClasses] = useState<SchoolClass[]>([]);
  const [sections, setSections] = useState<Section[]>([]);

  useEffect(() => {
    Promise.all([unwrap(api.get("/classes")), unwrap(api.get("/sections"))])
      .then(([c, s]) => {
        setClasses((c as SchoolClass[]).filter((x) => x.isActive).sort((a, b) => a.displayOrder - b.displayOrder));
        setSections(s as Section[]);
      })
      .catch(() => undefined);
  }, []);

  const qs = useMemo(() => {
    const q = new URLSearchParams();
    for (const k of ["classId", "sectionId", "minMonths", "minAmount"] as const) if (f[k]) q.set(k, f[k]);
    return q;
  }, [f]);

  const load = useCallback(async () => {
    setData(null);
    try {
      setData((await unwrap(api.get(`/fees/defaulters?${qs}&page=${page}&pageSize=${pageSize}`))) as Defaulters);
    } catch (err) {
      toast.error(errorText(err, "Could not load defaulters"));
      setData({ items: [], total: 0, totalBalance: 0 });
    }
  }, [qs, page, pageSize]);

  useEffect(() => {
    void load();
  }, [load]);

  const classSections = sections.filter((s) => String(s.classId) === f.classId && (user?.activeCampusId == null || s.campusId === user.activeCampusId));

  return (
    <Panel
      flush
      className="print-area"
      title="Defaulters"
      description={data ? `Students with a challan past its due date · ${rs(data.totalBalance)} outstanding` : "Students with a challan past its due date."}
      action={
        <div className="no-print flex gap-2">
          <Btn size="sm" variant="outline" onClick={() => window.print()} disabled={!data?.items.length}>
            <Printer /> Print
          </Btn>
          <Btn size="sm" variant="outline" onClick={() => void csv(`/fees/defaulters/csv?${qs}`, "defaulters.csv")} disabled={!data?.items.length}>
            <Download /> CSV
          </Btn>
        </div>
      }
    >
      <div className="no-print">
        <FilterBar shown={data ? data.total : null} noun={["student", "students"]} activeCount={activeCount} onClear={clear}>
          <Select aria-label="Class" value={f.classId} onChange={(e) => set({ classId: e.target.value, sectionId: "" })} className="w-auto">
            <option value="">All classes</option>
            {classes.map((c) => (
              <option key={c.id} value={c.id}>
                {c.name}
              </option>
            ))}
          </Select>
          <Select aria-label="Section" value={f.sectionId} disabled={!f.classId} onChange={(e) => set({ sectionId: e.target.value })} className="w-auto">
            <option value="">All sections</option>
            {classSections.map((s) => (
              <option key={s.id} value={s.id}>
                {s.name}
              </option>
            ))}
          </Select>
          <Select aria-label="Months" value={f.minMonths} onChange={(e) => set({ minMonths: e.target.value })} className="w-auto">
            <option value="">Any months</option>
            <option value="2">2+ months</option>
            <option value="3">3+ months</option>
            <option value="6">6+ months</option>
          </Select>
          <TextInput type="number" min={0} aria-label="Minimum balance" placeholder="Min. Rs" value={f.minAmount}
            onChange={(e) => set({ minAmount: e.target.value })} className="w-28" />
        </FilterBar>
      </div>
      {data == null ? (
        <TableSkeleton cols={5} />
      ) : data.items.length === 0 ? (
        activeCount > 0 ? <NoResults noun="defaulters" icon={UserX} onClear={clear} /> : <EmptyState icon={UserX} title="No defaulters" description="Every challan past its due date is paid." />
      ) : (
        <>
          <DataTable>
            <THead columns={[{ label: "Student" }, { label: "Class" }, { label: "Guardian phone" }, { label: "Since" }, { label: "Months", align: "right" }, { label: "Balance", align: "right" }, { label: "", align: "right" }]} />
            <tbody>
              {data.items.map((d) => (
                <Tr key={d.studentId}>
                  <Td>
                    <Link to={`/admin/students/${d.studentId}`} className="font-medium hover:text-[var(--primary)]">
                      {d.name}
                    </Link>
                    {d.studentLeft ? <StatusPill className="ml-2">Left</StatusPill> : null}
                    <div className="text-xs text-[var(--muted-foreground)]">{d.admissionNo}</div>
                  </Td>
                  <Td>
                    {d.className} – {d.sectionName}
                  </Td>
                  <Td className="tabular">{d.guardianPhone ?? "—"}</Td>
                  <Td>{monthLabel(d.oldestMonth)}</Td>
                  <Td className="tabular text-right">{d.months}</Td>
                  <Td className="tabular text-right font-medium">{rs(d.balance)}</Td>
                  <Td className="no-print text-right">
                    {can("fee-collection", "Add") ? (
                      <Link to={`/admin/fees/collect?q=${encodeURIComponent(d.admissionNo)}`} className="text-sm text-[var(--primary)]">
                        Collect
                      </Link>
                    ) : null}
                  </Td>
                </Tr>
              ))}
            </tbody>
          </DataTable>
          <div className="no-print">
            <Pagination page={page} pageSize={pageSize} total={data.total} onPage={(p) => set({ page: String(p) })} onPageSize={(n) => set({ pageSize: String(n) })} pageSizes={[25, 50, 100, 500]} />
          </div>
        </>
      )}
    </Panel>
  );
}

// ---------------------------------------------------------------- collection

function CampusTotals({ rows }: { rows?: CampusTotal[] | null }) {
  if (!rows?.length) return null;
  return (
    <Panel flush title="By campus" className="mt-4">
      <DataTable>
        <THead columns={[{ label: "Campus" }, { label: "Billed", align: "right" }, { label: "Collected", align: "right" }, { label: "Outstanding", align: "right" }]} />
        <tbody>
          {rows.map((c) => (
            <Tr key={c.campusCode}>
              <Td>
                {c.campusName} <span className="font-mono text-xs text-[var(--muted-foreground)]">{c.campusCode}</span>
              </Td>
              <Td className="tabular text-right">{c.billed ? rs(c.billed) : "—"}</Td>
              <Td className="tabular text-right">{rs(c.collected)}</Td>
              <Td className="tabular text-right">{c.outstanding ? rs(c.outstanding) : "—"}</Td>
            </Tr>
          ))}
        </tbody>
      </DataTable>
    </Panel>
  );
}

function CollectionTab() {
  const { values: f, set } = useUrlFilters({ tab: "collection", from: todayIso(), to: todayIso() });
  const [r, setR] = useState<CollectionReport | null>(null);

  useEffect(() => {
    setR(null);
    unwrap(api.get(`/fees/reports/collection?from=${f.from}&to=${f.to}`))
      .then((x) => setR(x as CollectionReport))
      .catch((err) => {
        toast.error(errorText(err, "Could not load the report"));
        setR({ from: f.from, to: f.to, payments: [], byMethod: [], byUser: [], total: 0, voided: 0 });
      });
  }, [f.from, f.to]);

  return (
    <>
      <Panel
        flush
        title="Collection"
        description={r ? `${rs(r.total)} received${r.voided ? ` · ${rs(r.voided)} voided` : ""}` : undefined}
        action={
          <Btn size="sm" variant="outline" disabled={!r?.payments.length} onClick={() => void csv(`/fees/reports/collection/csv?from=${f.from}&to=${f.to}`, "collection.csv")}>
            <Download /> CSV
          </Btn>
        }
      >
        <FilterBar>
          <div className="flex items-center gap-1.5 text-xs text-[var(--muted-foreground)]">
            <TextInput type="date" aria-label="From" value={f.from} max={f.to} onChange={(e) => set({ from: e.target.value || todayIso() })} className="w-auto" />
            <span>to</span>
            <TextInput type="date" aria-label="To" value={f.to} min={f.from} onChange={(e) => set({ to: e.target.value || todayIso() })} className="w-auto" />
          </div>
        </FilterBar>
        {r == null ? (
          <TableSkeleton cols={4} />
        ) : r.payments.length === 0 ? (
          <EmptyState icon={Wallet} title="Nothing received" description="No payments in these dates." />
        ) : (
          <>
            <div className="grid gap-4 border-b border-[var(--border)] p-5 sm:grid-cols-2">
              <Totals title="By method" rows={r.byMethod.map((m) => [methodLabel[m.method], m.count, m.total])} />
              <Totals title="By cashier" rows={r.byUser.map((u) => [u.user || "—", u.count, u.total])} />
            </div>
            <DataTable>
              <THead columns={[{ label: "Receipt" }, { label: "Date" }, { label: "Challan" }, { label: "Method" }, { label: "Amount", align: "right" }]} />
              <tbody>
                {r.payments.map((p) => (
                  <Tr key={p.id}>
                    <Td>
                      <Link to={`/admin/fees/receipt/${p.id}`} className="font-mono text-xs text-[var(--primary)]">
                        {p.receiptNo}
                      </Link>
                      {p.isVoid ? <StatusPill tone="danger" className="ml-2">Void</StatusPill> : null}
                    </Td>
                    <Td>{dateLabel(p.paidOn)}</Td>
                    <Td className="font-mono text-xs">{p.voucherNo}</Td>
                    <Td>
                      {methodLabel[p.method]}
                      {p.reference ? <span className="text-xs text-[var(--muted-foreground)]"> · {p.reference}</span> : null}
                    </Td>
                    <Td className={`tabular text-right ${p.isVoid ? "text-[var(--muted-foreground)] line-through" : ""}`}>{rs(p.amount)}</Td>
                  </Tr>
                ))}
              </tbody>
            </DataTable>
          </>
        )}
      </Panel>
      <CampusTotals rows={r?.campuses} />
    </>
  );
}

function Totals({ title, rows }: { title: string; rows: [string, number, number][] }) {
  return (
    <div>
      <div className="mb-1 text-xs font-medium text-[var(--muted-foreground)]">{title}</div>
      {rows.map(([label, count, total]) => (
        <div key={label} className="flex justify-between gap-2 text-sm">
          <span>
            {label} <span className="text-xs text-[var(--muted-foreground)]">× {count}</span>
          </span>
          <span className="tabular font-medium">{rs(total)}</span>
        </div>
      ))}
    </div>
  );
}

// ---------------------------------------------------------------- monthly

function MonthPicker({ value, onChange }: { value: string; onChange: (v: string) => void }) {
  return (
    <FilterBar>
      <TextInput type="month" aria-label="Month" value={toMonthInput(value)} onChange={(e) => onChange(fromMonthInput(e.target.value) || thisMonthIso())} className="w-auto" />
    </FilterBar>
  );
}

function MonthlyTab() {
  const { values: f, set } = useUrlFilters({ tab: "monthly", month: thisMonthIso() });
  const [r, setR] = useState<FeeMonthlyReport | null>(null);

  useEffect(() => {
    setR(null);
    unwrap(api.get(`/fees/reports/monthly?month=${f.month}`))
      .then((x) => setR(x as FeeMonthlyReport))
      .catch((err) => toast.error(errorText(err, "Could not load the report")));
  }, [f.month]);

  const row = (x: FeeMonthlyReport["totals"], bold = false) => (
    <Tr key={x.className}>
      <Td className={bold ? "font-semibold" : ""}>{x.className}</Td>
      <Td className="tabular text-right">{x.vouchers}</Td>
      <Td className="tabular text-right">{rs(x.billed)}</Td>
      <Td className="tabular text-right">{x.concession ? rs(x.concession) : "—"}</Td>
      <Td className="tabular text-right">{rs(x.collected)}</Td>
      <Td className="tabular text-right">{rs(x.outstanding)}</Td>
      <Td className={`tabular text-right ${bold ? "font-semibold" : ""}`}>{x.collectionPercent.toFixed(2)}%</Td>
    </Tr>
  );

  return (
    <>
      <Panel
        flush
        title={`Monthly summary · ${monthLabel(f.month)}`}
        description="Billed is the month's head amounts (arrears and fines not included)."
        action={
          <Btn size="sm" variant="outline" disabled={!r?.rows.length} onClick={() => void csv(`/fees/reports/monthly/csv?month=${f.month}`, "fees-month.csv")}>
            <Download /> CSV
          </Btn>
        }
      >
        <MonthPicker value={f.month} onChange={(month) => set({ month })} />
        {r == null ? (
          <TableSkeleton cols={7} />
        ) : r.rows.length === 0 ? (
          <EmptyState icon={Wallet} title={`No challans for ${monthLabel(f.month)}`} />
        ) : (
          <DataTable>
            <THead
              columns={[
                { label: "Class" },
                { label: "Challans", align: "right" },
                { label: "Billed", align: "right" },
                { label: "Concession", align: "right" },
                { label: "Collected", align: "right" },
                { label: "Outstanding", align: "right" },
                { label: "Collected %", align: "right" },
              ]}
            />
            <tbody>
              {r.rows.map((x) => row(x))}
              {row(r.totals, true)}
            </tbody>
          </DataTable>
        )}
      </Panel>
      <CampusTotals rows={r?.campuses} />
    </>
  );
}

function HeadsTab() {
  const { values: f, set } = useUrlFilters({ tab: "heads", month: thisMonthIso() });
  const [r, setR] = useState<HeadReport | null>(null);

  useEffect(() => {
    setR(null);
    unwrap(api.get(`/fees/reports/heads?month=${f.month}`))
      .then((x) => setR(x as HeadReport))
      .catch((err) => toast.error(errorText(err, "Could not load the report")));
  }, [f.month]);

  const total = (k: "amount" | "concession" | "net") => (r?.rows ?? []).reduce((s, x) => s + x[k], 0);

  return (
    <Panel
      flush
      title={`Head-wise · ${monthLabel(f.month)}`}
      action={
        <Btn size="sm" variant="outline" disabled={!r?.rows.length} onClick={() => void csv(`/fees/reports/heads/csv?month=${f.month}`, "fee-heads.csv")}>
          <Download /> CSV
        </Btn>
      }
    >
      <MonthPicker value={f.month} onChange={(month) => set({ month })} />
      {r == null ? (
        <TableSkeleton cols={4} />
      ) : r.rows.length === 0 ? (
        <EmptyState icon={Wallet} title={`No challans for ${monthLabel(f.month)}`} />
      ) : (
        <DataTable>
          <THead columns={[{ label: "Head" }, { label: "Amount", align: "right" }, { label: "Concession", align: "right" }, { label: "Net", align: "right" }]} />
          <tbody>
            {r.rows.map((x) => (
              <Tr key={x.head}>
                <Td>{x.head}</Td>
                <Td className="tabular text-right">{rs(x.amount)}</Td>
                <Td className="tabular text-right">{x.concession ? rs(x.concession) : "—"}</Td>
                <Td className="tabular text-right">{rs(x.net)}</Td>
              </Tr>
            ))}
            <Tr>
              <Td className="font-semibold">Total</Td>
              <Td className="tabular text-right font-semibold">{rs(total("amount"))}</Td>
              <Td className="tabular text-right font-semibold">{rs(total("concession"))}</Td>
              <Td className="tabular text-right font-semibold">{rs(total("net"))}</Td>
            </Tr>
          </tbody>
        </DataTable>
      )}
    </Panel>
  );
}
