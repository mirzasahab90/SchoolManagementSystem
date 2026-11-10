import { useCallback, useEffect, useState } from "react";
import { BadgePercent, Check, Pencil, Plus, Save, Trash2 } from "lucide-react";
import { toast } from "sonner";
import { Modal } from "@/components/modal";
import {
  Btn,
  DataTable,
  EmptyState,
  Field,
  FilterBar,
  IconButton,
  NoResults,
  Pagination,
  Panel,
  SearchField,
  Select,
  StatusPill,
  TableSkeleton,
  Td,
  TextInput,
  THead,
  Tr,
} from "@/components/sms-ui";
import { api, unwrap } from "@/lib/api";
import { useUrlFilters } from "@/lib/filters";
import type { Concession, ConcessionValueType, LineCalc, PagedResult, StaffChildSuggestion, StudentListItem } from "@/lib/types";
import { useSessions } from "@/pages/exams/shared";
import { errorText, rs, thisMonthIso, useFeeHeads } from "./shared";

type Form = {
  id?: number;
  student: { id: number; name: string; admissionNo: string } | null;
  feeHeadId: number | null;
  valueType: ConcessionValueType;
  value: string;
  reason: string;
  kind: "Manual" | "StaffChild";
  staffId: number | null;
  isActive: boolean;
};

const blank: Form = { student: null, feeHeadId: null, valueType: "Percent", value: "", reason: "", kind: "Manual", staffId: null, isActive: true };

export function ConcessionsTab({ canEdit }: { canEdit: boolean }) {
  const sessions = useSessions();
  const sessionId = sessions?.effectiveCurrentId ?? sessions?.items[0]?.id ?? 0;
  const { values: f, set, clear, activeCount } = useUrlFilters({ tab: "concessions", q: "", page: "1", pageSize: "25" });
  const page = Number(f.page) || 1;
  const pageSize = Number(f.pageSize) || 25;
  const [result, setResult] = useState<PagedResult<Concession> | null>(null);
  const [suggestions, setSuggestions] = useState<StaffChildSuggestion[]>([]);
  const [form, setForm] = useState<Form | null>(null);

  const load = useCallback(async () => {
    if (!sessionId) return;
    const qs = new URLSearchParams({ sessionId: String(sessionId), page: String(page), pageSize: String(pageSize) });
    if (f.q.trim()) qs.set("q", f.q.trim());
    try {
      setResult((await unwrap(api.get(`/fees/concessions?${qs}`))) as PagedResult<Concession>);
    } catch (err) {
      toast.error(errorText(err, "Could not load concessions"));
      setResult({ items: [], total: 0 });
    }
    if (canEdit) {
      try {
        setSuggestions((await unwrap(api.get("/fees/concessions/staff-child-suggestions"))) as StaffChildSuggestion[]);
      } catch {
        setSuggestions([]);
      }
    }
  }, [sessionId, page, pageSize, f.q, canEdit]);

  useEffect(() => {
    void load();
  }, [load]);

  const remove = async (c: Concession) => {
    try {
      await unwrap(api.delete(`/fees/concessions/${c.id}`));
      toast.success("Concession removed");
      await load();
    } catch (err) {
      toast.error(errorText(err, "Could not remove"));
    }
  };

  return (
    <div className="space-y-4">
      {canEdit && suggestions.length ? (
        <Panel title="Staff children" description="These students' guardian CNIC matches a staff member. Confirm to give the staff child discount.">
          <ul className="divide-y divide-[var(--border)]">
            {suggestions.map((s) => (
              <li key={`${s.studentId}-${s.staffId}`} className="flex flex-wrap items-center gap-3 py-2 text-sm">
                <div className="min-w-0 flex-1">
                  <div className="font-medium">{s.studentName}</div>
                  <div className="text-xs text-[var(--muted-foreground)]">
                    {s.className} · child of {s.staffName} · CNIC {s.maskedCnic}
                  </div>
                </div>
                <Btn
                  size="sm"
                  variant="outline"
                  onClick={() =>
                    setForm({ ...blank, kind: "StaffChild", staffId: s.staffId, student: { id: s.studentId, name: s.studentName, admissionNo: "" }, reason: `Child of ${s.staffName}` })
                  }
                >
                  <Check /> Confirm
                </Btn>
              </li>
            ))}
          </ul>
        </Panel>
      ) : null}

      <Panel
        flush
        title="Concessions"
        description="Per-student discounts this session. Per head, the largest of manual, staff child and sibling discount applies."
        action={
          canEdit ? (
            <Btn size="sm" onClick={() => setForm({ ...blank })}>
              <Plus /> Add concession
            </Btn>
          ) : null
        }
      >
        <FilterBar shown={result ? result.total : null} noun={["concession", "concessions"]} activeCount={activeCount} onClear={clear}>
          <SearchField value={f.q} onChange={(q) => set({ q })} debounce={300} placeholder="Student, admission no. or reason" />
        </FilterBar>
        {result == null ? (
          <TableSkeleton cols={5} />
        ) : result.items.length === 0 ? (
          activeCount > 0 ? (
            <NoResults noun="concessions" icon={BadgePercent} onClear={clear} />
          ) : (
            <EmptyState icon={BadgePercent} title="No concessions" description="Sibling discounts apply on their own; add manual concessions here." />
          )
        ) : (
          <>
            <DataTable>
              <THead columns={[{ label: "Student" }, { label: "Head" }, { label: "Discount" }, { label: "Reason" }, { label: "", align: "right" }]} />
              <tbody>
                {result.items.map((c) => (
                  <Tr key={c.id}>
                    <Td>
                      <div className="font-medium">{c.studentName}</div>
                      <div className="text-xs text-[var(--muted-foreground)]">
                        {c.admissionNo}
                        {c.className ? ` · ${c.className} – ${c.sectionName}` : ""}
                      </div>
                    </Td>
                    <Td>{c.headName}</Td>
                    <Td className="tabular">
                      {c.valueType === "Percent" ? `${c.value}%` : rs(c.value)}
                      {c.kind === "StaffChild" ? <StatusPill tone="info" className="ml-2">Staff child</StatusPill> : null}
                      {!c.isActive ? <StatusPill className="ml-2">Inactive</StatusPill> : null}
                    </Td>
                    <Td className="max-w-[16rem] truncate text-[var(--muted-foreground)]">{c.reason}</Td>
                    <Td className="text-right">
                      {canEdit ? (
                        <div className="inline-flex gap-1">
                          <IconButton
                            label="Edit concession"
                            onClick={() =>
                              setForm({
                                id: c.id,
                                student: { id: c.studentId, name: c.studentName, admissionNo: c.admissionNo },
                                feeHeadId: c.feeHeadId ?? null,
                                valueType: c.valueType,
                                value: String(c.value),
                                reason: c.reason,
                                kind: c.kind,
                                staffId: c.staffId ?? null,
                                isActive: c.isActive,
                              })
                            }
                          >
                            <Pencil />
                          </IconButton>
                          <IconButton label="Remove concession" tone="danger" onClick={() => void remove(c)}>
                            <Trash2 />
                          </IconButton>
                        </div>
                      ) : null}
                    </Td>
                  </Tr>
                ))}
              </tbody>
            </DataTable>
            <Pagination page={page} pageSize={pageSize} total={result.total} onPage={(p) => set({ page: String(p) })} onPageSize={(n) => set({ pageSize: String(n) })} />
          </>
        )}
      </Panel>

      {form ? (
        <ConcessionDialog
          form={form}
          sessionId={sessionId}
          onClose={() => setForm(null)}
          onSaved={async () => {
            setForm(null);
            await load();
          }}
        />
      ) : null}
    </div>
  );
}

function ConcessionDialog({ form: initial, sessionId, onClose, onSaved }: { form: Form; sessionId: number; onClose: () => void; onSaved: () => Promise<void> }) {
  const { heads } = useFeeHeads();
  const [form, setForm] = useState<Form>(initial);
  const [q, setQ] = useState("");
  const [found, setFound] = useState<StudentListItem[]>([]);
  const [lines, setLines] = useState<LineCalc[] | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (form.student || q.trim().length < 2) {
      setFound([]);
      return;
    }
    const t = setTimeout(() => {
      unwrap(api.get(`/students?q=${encodeURIComponent(q.trim())}&pageSize=8&status=Active`))
        .then((r) => setFound((r as PagedResult<StudentListItem>).items))
        .catch(() => setFound([]));
    }, 250);
    return () => clearTimeout(t);
  }, [q, form.student]);

  // What the student pays now (before this concession is saved).
  useEffect(() => {
    if (!form.student) return setLines(null);
    unwrap(api.get(`/fees/concessions/preview?studentId=${form.student.id}&month=${thisMonthIso()}`))
      .then((r) => setLines(r as LineCalc[]))
      .catch(() => setLines([]));
  }, [form.student]);

  const save = async () => {
    if (!form.student) return;
    setBusy(true);
    const body = {
      studentId: form.student.id,
      sessionId,
      feeHeadId: form.feeHeadId,
      kind: form.kind,
      staffId: form.kind === "StaffChild" ? form.staffId : null,
      valueType: form.valueType,
      value: form.value.trim() === "" ? null : Number(form.value),
      reason: form.reason.trim(),
      isActive: form.isActive,
    };
    try {
      await unwrap(form.id ? api.put(`/fees/concessions/${form.id}`, body) : api.post("/fees/concessions", body));
      toast.success(form.id ? "Concession updated" : "Concession added");
      await onSaved();
    } catch (err) {
      toast.error(errorText(err, "Could not save the concession"));
    } finally {
      setBusy(false);
    }
  };

  const staffChild = form.kind === "StaffChild";
  return (
    <Modal
      open
      title={form.id ? "Edit concession" : staffChild ? "Staff child concession" : "Add concession"}
      onClose={onClose}
      footer={
        <>
          <Btn variant="outline" onClick={onClose}>
            Cancel
          </Btn>
          <Btn disabled={busy || !form.student || !form.reason.trim() || (!staffChild && !form.value.trim())} onClick={() => void save()}>
            <Save /> Save
          </Btn>
        </>
      }
    >
      <div className="space-y-3">
        <Field label="Student" required>
          {form.student ? (
            <div className="flex items-center justify-between rounded-lg border border-[var(--border)] px-3 py-2 text-sm">
              <span>
                <span className="font-medium">{form.student.name}</span>
                {form.student.admissionNo ? <span className="ml-2 text-[var(--muted-foreground)]">{form.student.admissionNo}</span> : null}
              </span>
              {!form.id && !staffChild ? (
                <button type="button" className="text-xs text-[var(--primary)]" onClick={() => setForm({ ...form, student: null })}>
                  Change
                </button>
              ) : null}
            </div>
          ) : (
            <div>
              <TextInput autoFocus value={q} onChange={(e) => setQ(e.target.value)} placeholder="Search by name or admission no." />
              {found.length ? (
                <ul className="mt-1 max-h-48 overflow-y-auto rounded-lg border border-[var(--border)]">
                  {found.map((s) => (
                    <li key={s.id}>
                      <button
                        type="button"
                        className="w-full px-3 py-1.5 text-left text-sm hover:bg-[var(--muted)]"
                        onClick={() => setForm({ ...form, student: { id: s.id, name: s.fullName, admissionNo: s.admissionNo } })}
                      >
                        {s.fullName} <span className="text-[var(--muted-foreground)]">· {s.admissionNo} · {s.className ?? "—"}</span>
                      </button>
                    </li>
                  ))}
                </ul>
              ) : null}
            </div>
          )}
        </Field>
        <div className="grid gap-3 sm:grid-cols-3">
          <Field label="Head" className="sm:col-span-3">
            <Select value={form.feeHeadId ?? 0} onChange={(e) => setForm({ ...form, feeHeadId: Number(e.target.value) || null })}>
              <option value={0}>{staffChild ? "Discount heads (from settings)" : "All monthly heads"}</option>
              {(heads ?? []).filter((h) => h.isActive).map((h) => (
                <option key={h.id} value={h.id}>
                  {h.name}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Type">
            <Select value={form.valueType} onChange={(e) => setForm({ ...form, valueType: e.target.value as ConcessionValueType })}>
              <option value="Percent">Percent</option>
              <option value="Fixed">Fixed amount</option>
            </Select>
          </Field>
          <Field label={form.valueType === "Percent" ? "Percent" : "Amount (Rs)"} hint={staffChild && !form.value ? "Empty uses the settings %" : undefined}>
            <TextInput type="number" min={0} max={form.valueType === "Percent" ? 100 : undefined} value={form.value}
              onChange={(e) => setForm({ ...form, value: e.target.value })} />
          </Field>
          <Field label="Status">
            <Select value={form.isActive ? "1" : "0"} onChange={(e) => setForm({ ...form, isActive: e.target.value === "1" })}>
              <option value="1">Active</option>
              <option value="0">Inactive</option>
            </Select>
          </Field>
        </div>
        <Field label="Reason" required>
          <TextInput maxLength={200} value={form.reason} onChange={(e) => setForm({ ...form, reason: e.target.value })} placeholder="e.g. Merit scholarship" />
        </Field>
        {lines && lines.length ? (
          <div className="rounded-lg bg-[var(--muted)] p-3 text-xs">
            <div className="mb-1 font-medium">This month now</div>
            {lines.map((l) => (
              <div key={l.feeHeadId} className="flex justify-between gap-2">
                <span>
                  {l.description}
                  {l.concessionNote ? <span className="text-[var(--muted-foreground)]"> · {l.concessionNote}</span> : null}
                </span>
                <span className="tabular">{rs(l.net)}</span>
              </div>
            ))}
          </div>
        ) : null}
      </div>
    </Modal>
  );
}
