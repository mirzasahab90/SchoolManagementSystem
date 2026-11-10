import { useCallback, useEffect, useMemo, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { Ban, FilePlus2, Printer, Receipt, SlidersHorizontal } from "lucide-react";
import { toast } from "sonner";
import { Modal } from "@/components/modal";
import {
  Btn,
  DataTable,
  EmptyState,
  Field,
  FilterBar,
  NoResults,
  PageHeader,
  Pagination,
  Panel,
  SearchField,
  Select,
  TableSkeleton,
  Td,
  TextInput,
  THead,
  Tr,
} from "@/components/sms-ui";
import { api, unwrap } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import { useUrlFilters } from "@/lib/filters";
import { cn } from "@/lib/utils";
import type { GeneratePreview, GenerateResult, PagedResult, SchoolClass, Section, Voucher, VoucherDetail, VoucherStatus } from "@/lib/types";
import { voucherStatusLabel } from "@/lib/types";
import { VoucherStatusPill, dateLabel, errorText, fromMonthInput, methodLabel, monthLabel, rs, thisMonthIso, toMonthInput, todayIso } from "./shared";

const STATUSES: VoucherStatus[] = ["Unpaid", "PartiallyPaid", "Paid", "CarriedForward", "Cancelled"];

export function ChallansPage() {
  const { user, can } = useAuth();
  const navigate = useNavigate();
  const noCampus = user?.activeCampusId == null;
  const { values: f, set, clear, activeCount } = useUrlFilters({
    month: thisMonthIso(),
    classId: "",
    sectionId: "",
    status: "",
    q: "",
    page: "1",
    pageSize: "25",
    open: "",
  });
  const page = Number(f.page) || 1;
  const pageSize = Number(f.pageSize) || 25;
  const [result, setResult] = useState<PagedResult<Voucher> | null>(null);
  const [classes, setClasses] = useState<SchoolClass[]>([]);
  const [sections, setSections] = useState<Section[]>([]);
  const [generating, setGenerating] = useState(false);

  useEffect(() => {
    Promise.all([unwrap(api.get("/classes")), unwrap(api.get("/sections"))])
      .then(([c, s]) => {
        setClasses((c as SchoolClass[]).filter((x) => x.isActive).sort((a, b) => a.displayOrder - b.displayOrder));
        setSections(s as Section[]);
      })
      .catch(() => undefined);
  }, []);

  const load = useCallback(async () => {
    const qs = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
    if (f.month) qs.set("month", f.month);
    if (f.classId) qs.set("classId", f.classId);
    if (f.sectionId) qs.set("sectionId", f.sectionId);
    if (f.status) qs.set("status", f.status);
    if (f.q.trim()) qs.set("q", f.q.trim());
    setResult(null);
    try {
      setResult((await unwrap(api.get(`/fees/vouchers?${qs}`))) as PagedResult<Voucher>);
    } catch (err) {
      toast.error(errorText(err, "Could not load challans"));
      setResult({ items: [], total: 0 });
    }
  }, [f.month, f.classId, f.sectionId, f.status, f.q, page, pageSize]);

  useEffect(() => {
    if (!noCampus) void load();
  }, [load, noCampus]);

  const classSections = useMemo(
    () => sections.filter((s) => String(s.classId) === f.classId && (user?.activeCampusId == null || s.campusId === user.activeCampusId)),
    [sections, f.classId, user?.activeCampusId],
  );

  const printUrl = `/admin/fees/challans/print?month=${f.month}${f.sectionId ? `&sectionId=${f.sectionId}` : ""}`;

  return (
    <div>
      <PageHeader
        title="Challans"
        description="Monthly fee challans. Generate a month, print bank copies, and open a challan for its lines and payments."
        actions={
          !noCampus ? (
            <div className="flex flex-wrap gap-2">
              <Btn variant="outline" onClick={() => navigate(printUrl)} disabled={!result?.total}>
                <Printer /> Print
              </Btn>
              {can("fee-vouchers", "Add") ? (
                <Btn onClick={() => setGenerating(true)}>
                  <FilePlus2 /> Generate month
                </Btn>
              ) : null}
            </div>
          ) : null
        }
      />

      {noCampus ? (
        <div className="panel">
          <EmptyState icon={Receipt} title="Pick a campus" description="Challans belong to one campus. Choose a campus from the switcher at the top." />
        </div>
      ) : (
        <Panel flush>
          <FilterBar shown={result ? result.total : null} noun={["challan", "challans"]} activeCount={activeCount} onClear={clear}>
            <SearchField value={f.q} onChange={(q) => set({ q })} debounce={300} placeholder="Name, admission or challan no." className="sm:max-w-xs" />
            <TextInput type="month" aria-label="Month" value={toMonthInput(f.month)} onChange={(e) => set({ month: fromMonthInput(e.target.value) })} className="w-auto" />
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
            <Select aria-label="Status" value={f.status} onChange={(e) => set({ status: e.target.value })} className="w-auto">
              <option value="">All statuses</option>
              {STATUSES.map((s) => (
                <option key={s} value={s}>
                  {voucherStatusLabel[s]}
                </option>
              ))}
            </Select>
          </FilterBar>

          {result == null ? (
            <TableSkeleton cols={6} />
          ) : result.items.length === 0 ? (
            activeCount > 0 ? (
              <NoResults noun="challans" icon={Receipt} onClear={clear} />
            ) : (
              <EmptyState icon={Receipt} title={`No challans for ${monthLabel(f.month)}`} description="Generate the month to create challans for every active student." />
            )
          ) : (
            <>
              <DataTable>
                <THead
                  columns={[
                    { label: "Challan" },
                    { label: "Student" },
                    { label: "Class" },
                    { label: "Due" },
                    { label: "Total", align: "right" },
                    { label: "Balance", align: "right" },
                    { label: "Status" },
                  ]}
                />
                <tbody>
                  {result.items.map((v) => (
                    <Tr key={v.id} onClick={() => set({ open: String(v.id) })}>
                      <Td>
                        <div className="font-mono text-xs whitespace-nowrap">{v.voucherNo}</div>
                        <div className="text-xs text-[var(--muted-foreground)]">{monthLabel(v.month)}</div>
                      </Td>
                      <Td>
                        <div className="font-medium">{v.studentName}</div>
                        <div className="text-xs text-[var(--muted-foreground)]">{v.admissionNo}</div>
                      </Td>
                      <Td className="whitespace-nowrap">
                        {v.className} – {v.sectionName}
                        <div className="text-xs text-[var(--muted-foreground)]">Roll {v.rollNo}</div>
                      </Td>
                      <Td className="whitespace-nowrap">{dateLabel(v.dueDate)}</Td>
                      <Td className="tabular text-right whitespace-nowrap">{rs(v.total)}</Td>
                      <Td className={cn("tabular text-right whitespace-nowrap", v.balance > 0 && "font-medium")}>{rs(v.balance)}</Td>
                      <Td>
                        <VoucherStatusPill status={v.status} />
                      </Td>
                    </Tr>
                  ))}
                </tbody>
              </DataTable>
              <Pagination page={page} pageSize={pageSize} total={result.total} onPage={(p) => set({ page: String(p) })} onPageSize={(n) => set({ pageSize: String(n) })} />
            </>
          )}
        </Panel>
      )}

      {generating ? (
        <GenerateDialog
          classes={classes}
          sections={sections}
          initialMonth={f.month}
          onClose={() => setGenerating(false)}
          onDone={(month) => {
            setGenerating(false);
            set({ month, status: "", q: "" });
            void load();
          }}
        />
      ) : null}
      {f.open ? <VoucherDrawer id={Number(f.open)} onClose={() => set({ open: "" })} onChanged={load} /> : null}
    </div>
  );
}

// ---------------------------------------------------------------- generate

function GenerateDialog({
  classes,
  sections,
  initialMonth,
  onClose,
  onDone,
}: {
  classes: SchoolClass[];
  sections: Section[];
  initialMonth: string;
  onClose: () => void;
  onDone: (month: string) => void;
}) {
  const [month, setMonth] = useState(initialMonth);
  const [classId, setClassId] = useState(0);
  const [sectionId, setSectionId] = useState(0);
  const [issueDate, setIssueDate] = useState(todayIso());
  const [dueDate, setDueDate] = useState("");
  const [preview, setPreview] = useState<GeneratePreview | null>(null);
  const [busy, setBusy] = useState(false);

  const body = (p: boolean) => ({ month, classId: classId || null, sectionId: sectionId || null, issueDate, dueDate: dueDate || null, preview: p });

  const runPreview = async () => {
    setBusy(true);
    try {
      const r = (await unwrap(api.post("/fees/vouchers/generate", body(true)))) as GeneratePreview;
      setPreview(r);
      if (!dueDate) setDueDate(r.dueDate);
    } catch (err) {
      toast.error(errorText(err, "Could not preview"));
    } finally {
      setBusy(false);
    }
  };

  const generate = async () => {
    setBusy(true);
    try {
      const r = (await unwrap(api.post("/fees/vouchers/generate", body(false)))) as GenerateResult;
      toast.success(`${r.created} challan${r.created === 1 ? "" : "s"} created${r.skipped ? `, ${r.skipped} skipped` : ""}`);
      onDone(month);
    } catch (err) {
      toast.error(errorText(err, "Could not generate"));
    } finally {
      setBusy(false);
    }
  };

  const reset = () => setPreview(null);

  return (
    <Modal
      open
      wide
      title="Generate challans"
      description="Creates one challan per active student for the month. Unpaid balances move to the new challan as arrears."
      onClose={onClose}
      footer={
        <>
          <Btn variant="outline" onClick={onClose}>
            Cancel
          </Btn>
          {preview ? (
            <Btn disabled={busy || preview.toCreate === 0} onClick={() => void generate()}>
              <FilePlus2 /> Generate {preview.toCreate} challan{preview.toCreate === 1 ? "" : "s"}
            </Btn>
          ) : (
            <Btn disabled={busy || !month} onClick={() => void runPreview()}>
              Preview
            </Btn>
          )}
        </>
      }
    >
      <div className="grid gap-3 sm:grid-cols-3">
        <Field label="Month">
          <TextInput type="month" value={toMonthInput(month)} onChange={(e) => (setMonth(fromMonthInput(e.target.value)), setDueDate(""), reset())} />
        </Field>
        <Field label="Class">
          <Select value={classId} onChange={(e) => (setClassId(Number(e.target.value)), setSectionId(0), reset())}>
            <option value={0}>All classes</option>
            {classes.map((c) => (
              <option key={c.id} value={c.id}>
                {c.name}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Section">
          <Select value={sectionId} disabled={!classId} onChange={(e) => (setSectionId(Number(e.target.value)), reset())}>
            <option value={0}>All sections</option>
            {sections.filter((s) => s.classId === classId).map((s) => (
              <option key={s.id} value={s.id}>
                {s.name}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Issue date">
          <TextInput type="date" value={issueDate} onChange={(e) => (setIssueDate(e.target.value), reset())} />
        </Field>
        <Field label="Due date" hint="Empty uses the due day from settings">
          <TextInput type="date" value={dueDate} min={issueDate} onChange={(e) => (setDueDate(e.target.value), reset())} />
        </Field>
      </div>

      {preview ? (
        <div className="mt-4">
          <div className="mb-2 flex flex-wrap gap-x-4 gap-y-1 text-sm">
            <span>
              <span className="font-semibold tabular">{preview.toCreate}</span> to create
            </span>
            {preview.skipped ? (
              <span className="text-[var(--muted-foreground)]">
                <span className="tabular">{preview.skipped}</span> skipped
              </span>
            ) : null}
            <span className="ml-auto font-semibold tabular">{rs(preview.total)}</span>
          </div>
          <div className="max-h-[45vh] overflow-auto rounded-lg border border-[var(--border)]">
            <table className="w-full text-left text-sm">
              <thead className="sticky top-0 bg-[var(--muted)] text-xs text-[var(--muted-foreground)]">
                <tr>
                  <th className="px-3 py-2 font-medium">Student</th>
                  <th className="px-3 py-2 font-medium">Charges</th>
                  <th className="px-3 py-2 text-right font-medium">Arrears</th>
                  <th className="px-3 py-2 text-right font-medium">Total</th>
                </tr>
              </thead>
              <tbody>
                {preview.rows.map((r) => (
                  <tr key={r.studentId} className={cn("border-t border-[var(--border)]", r.skippedReason && "text-[var(--muted-foreground)]")}>
                    <td className="px-3 py-1.5">
                      <div className="font-medium">{r.name}</div>
                      <div className="text-xs">
                        {r.className} – {r.sectionName}
                      </div>
                    </td>
                    <td className="px-3 py-1.5 text-xs">
                      {r.skippedReason ?? r.lines.map((l) => `${l.description} ${rs(l.net)}${l.concession ? ` (${l.concessionNote})` : ""}`).join(" · ")}
                    </td>
                    <td className="tabular px-3 py-1.5 text-right">{r.skippedReason ? "" : r.arrears ? rs(r.arrears) : "—"}</td>
                    <td className="tabular px-3 py-1.5 text-right font-medium">{r.skippedReason ? "" : rs(r.total)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      ) : null}
    </Modal>
  );
}

// ---------------------------------------------------------------- detail

function VoucherDrawer({ id, onClose, onChanged }: { id: number; onClose: () => void; onChanged: () => Promise<void> | void }) {
  const { can } = useAuth();
  const [d, setD] = useState<VoucherDetail | null>(null);
  const [mode, setMode] = useState<"" | "cancel" | "adjust">("");
  const [reason, setReason] = useState("");
  const [amount, setAmount] = useState("");
  const [busy, setBusy] = useState(false);

  const load = useCallback(async () => {
    try {
      setD((await unwrap(api.get(`/fees/vouchers/${id}`))) as VoucherDetail);
    } catch (err) {
      toast.error(errorText(err, "Could not load the challan"));
      onClose();
    }
  }, [id, onClose]);

  useEffect(() => {
    void load();
  }, [load]);

  const act = async () => {
    setBusy(true);
    try {
      const r = (await unwrap(
        mode === "cancel"
          ? api.post(`/fees/vouchers/${id}/cancel`, { reason: reason.trim() })
          : api.post(`/fees/vouchers/${id}/adjustments`, { amount: Number(amount), reason: reason.trim() }),
      )) as VoucherDetail;
      setD(r);
      toast.success(mode === "cancel" ? "Challan cancelled" : "Adjustment added");
      setMode("");
      setReason("");
      setAmount("");
      await onChanged();
    } catch (err) {
      toast.error(errorText(err, "Could not save"));
    } finally {
      setBusy(false);
    }
  };

  const v = d?.voucher;
  const open = v && (v.status === "Unpaid" || v.status === "PartiallyPaid");
  return (
    <Modal open wide title={v ? `Challan ${v.voucherNo}` : "Challan"} description={v ? `${v.studentName} · ${monthLabel(v.month)}` : undefined} onClose={onClose}>
      {!d || !v ? (
        <TableSkeleton rows={4} cols={3} />
      ) : (
        <div className="space-y-4 text-sm">
          <div className="flex flex-wrap items-center gap-x-6 gap-y-2">
            <VoucherStatusPill status={v.status} />
            <span>
              Due <span className="font-medium">{dateLabel(v.dueDate)}</span>
            </span>
            <span>
              {v.className} – {v.sectionName} · Roll {v.rollNo}
            </span>
            <Link to={`/admin/students/${v.studentId}`} className="text-[var(--primary)]">
              Student profile
            </Link>
          </div>
          {d.cancelReason ? <p className="text-[var(--danger)]">Cancelled: {d.cancelReason}</p> : null}
          {d.carriedToVoucherId ? <p className="text-[var(--muted-foreground)]">The balance moved to a later challan as arrears.</p> : null}

          <table className="w-full">
            <tbody>
              {d.lines.map((l) => (
                <tr key={l.id} className="border-b border-[var(--border)]">
                  <td className="py-1.5">
                    {l.description}
                    {l.concession ? <span className="text-xs text-[var(--muted-foreground)]"> · {l.concessionNote} (−{rs(l.concession)})</span> : null}
                  </td>
                  <td className="tabular py-1.5 text-right">{rs(l.net)}</td>
                </tr>
              ))}
              <tr className="font-semibold">
                <td className="py-1.5">Total</td>
                <td className="tabular py-1.5 text-right">{rs(v.total)}</td>
              </tr>
              <tr>
                <td className="py-0.5 text-[var(--muted-foreground)]">Paid</td>
                <td className="tabular py-0.5 text-right">{rs(v.paid)}</td>
              </tr>
              <tr className="font-semibold">
                <td className="py-0.5">Balance</td>
                <td className="tabular py-0.5 text-right">{rs(v.balance)}</td>
              </tr>
              {open && !v.lateFineApplied && v.lateFineAmount ? (
                <tr className="text-xs text-[var(--muted-foreground)]">
                  <td className="py-0.5">After the due date (with late fine)</td>
                  <td className="tabular py-0.5 text-right">{rs(v.payableAfterDue)}</td>
                </tr>
              ) : null}
            </tbody>
          </table>

          {d.payments.length ? (
            <div>
              <div className="mb-1 font-medium">Payments</div>
              <ul className="space-y-1">
                {d.payments.map((p) => (
                  <li key={p.id} className={cn("flex flex-wrap justify-between gap-2", p.isVoid && "text-[var(--muted-foreground)] line-through")}>
                    <span>
                      {p.receiptNo} · {dateLabel(p.paidOn)} · {methodLabel[p.method]}
                      {p.reference ? ` · ${p.reference}` : ""}
                    </span>
                    <span className="tabular">{rs(p.amount)}</span>
                  </li>
                ))}
              </ul>
            </div>
          ) : null}

          {mode ? (
            <div className="space-y-2 rounded-lg border border-[var(--border)] p-3">
              {mode === "adjust" ? (
                <Field label="Amount (Rs)" hint="Negative to reduce, positive to add">
                  <TextInput type="number" autoFocus value={amount} onChange={(e) => setAmount(e.target.value)} />
                </Field>
              ) : null}
              <Field label="Reason">
                <TextInput autoFocus={mode === "cancel"} maxLength={mode === "cancel" ? 200 : 120} value={reason} onChange={(e) => setReason(e.target.value)} />
              </Field>
              <div className="flex justify-end gap-2">
                <Btn variant="outline" size="sm" onClick={() => setMode("")}>
                  Back
                </Btn>
                <Btn size="sm" variant={mode === "cancel" ? "danger" : "primary"} disabled={busy || !reason.trim() || (mode === "adjust" && !Number(amount))}
                  onClick={() => void act()}>
                  {mode === "cancel" ? "Cancel challan" : "Add adjustment"}
                </Btn>
              </div>
            </div>
          ) : (
            <div className="flex flex-wrap justify-end gap-2">
              {open && can("fee-vouchers", "Edit") ? (
                <>
                  <Btn variant="outline" size="sm" onClick={() => setMode("adjust")}>
                    <SlidersHorizontal /> Adjustment
                  </Btn>
                  <Btn variant="outline" size="sm" onClick={() => setMode("cancel")}>
                    <Ban /> Cancel challan
                  </Btn>
                </>
              ) : null}
              {open && can("fee-collection", "Add") ? (
                <Link to={`/admin/fees/collect?q=${encodeURIComponent(v.voucherNo)}`}>
                  <Btn variant="outline" size="sm">
                    <Receipt /> Collect
                  </Btn>
                </Link>
              ) : null}
              {v.status !== "Cancelled" ? (
                <Link to={`/admin/fees/challans/print?ids=${v.id}`}>
                  <Btn size="sm">
                    <Printer /> Print
                  </Btn>
                </Link>
              ) : null}
            </div>
          )}
        </div>
      )}
    </Modal>
  );
}
