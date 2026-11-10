import { useCallback, useEffect, useMemo, useState } from "react";
import { Pencil, Plus, RotateCcw, Save, Settings2, Trash2 } from "lucide-react";
import { toast } from "sonner";
import { Modal } from "@/components/modal";
import { OwnerPill } from "@/components/owner-pill";
import {
  Btn,
  DataTable,
  EmptyState,
  Field,
  IconButton,
  PageHeader,
  Panel,
  Select,
  Skeleton,
  StatusPill,
  Switch,
  TableSkeleton,
  Td,
  TextArea,
  TextInput,
  THead,
  Tr,
} from "@/components/sms-ui";
import { api, unwrap } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import { useUrlFilters } from "@/lib/filters";
import { cn } from "@/lib/utils";
import type { FeeFrequency, FeeHead, FeeSettings, StructureGrid } from "@/lib/types";
import { useSessions } from "@/pages/exams/shared";
import { ConcessionsTab } from "./concessions";
import { MONTHS_LONG, Tabs, errorText, rs, useFeeHeads, useFeeSettings } from "./shared";

type Tab = "heads" | "structure" | "settings" | "concessions";

export function FeeSetupPage() {
  const { user, can } = useAuth();
  const canEdit = can("fee-setup", "Edit");
  // Main Campus (no campus picked) edits the defaults every campus uses; a campus edits its own overrides.
  const mainCampus = !!user?.isHq && user.activeCampusId == null;
  const { values, set } = useUrlFilters({ tab: "heads" });
  const tab = values.tab as Tab;
  const tabs: [Tab, string][] = [
    ["heads", "Fee heads"],
    ["structure", "Class-wise amounts"],
    ["settings", "Settings"],
    ...(!mainCampus ? ([["concessions", "Concessions"]] as [Tab, string][]) : []),
  ];

  return (
    <div>
      <PageHeader
        title="Fee setup"
        description={
          mainCampus
            ? "Main Campus defaults: every campus uses these heads, amounts and settings unless it sets its own."
            : "Fee heads, class-wise amounts, settings and concessions for this campus. Main Campus values apply where the campus has none."
        }
      />
      <Tabs tabs={tabs} value={tabs.some(([k]) => k === tab) ? tab : "heads"} onChange={(t) => set({ tab: t })} />
      {tab === "structure" ? <StructurePanel canEdit={canEdit} mainCampus={mainCampus} /> : null}
      {tab === "settings" ? <SettingsPanel canEdit={canEdit} mainCampus={mainCampus} /> : null}
      {tab === "concessions" && !mainCampus ? <ConcessionsTab canEdit={canEdit} /> : null}
      {tab === "heads" || !tabs.some(([k]) => k === tab) ? <HeadsPanel canEdit={canEdit} mainCampus={mainCampus} /> : null}
    </div>
  );
}

// ---------------------------------------------------------------- heads

type HeadForm = { id?: number; name: string; frequency: FeeFrequency; oneTimeMonth: number | null; sortOrder: number; isActive: boolean };

function HeadsPanel({ canEdit, mainCampus }: { canEdit: boolean; mainCampus: boolean }) {
  const { heads, reload } = useFeeHeads();
  const [form, setForm] = useState<HeadForm | null>(null);
  const [busy, setBusy] = useState(false);

  const editable = (h: FeeHead) => canEdit && (h.campusId == null ? mainCampus : !mainCampus);

  const save = async () => {
    if (!form) return;
    setBusy(true);
    try {
      const body = { ...form, name: form.name.trim(), oneTimeMonth: form.frequency === "OneTime" ? form.oneTimeMonth : null, mainCampus };
      await unwrap(form.id ? api.put(`/fees/heads/${form.id}`, body) : api.post("/fees/heads", body));
      toast.success(form.id ? "Fee head updated" : "Fee head added");
      setForm(null);
      await reload();
    } catch (err) {
      toast.error(errorText(err, "Could not save the fee head"));
    } finally {
      setBusy(false);
    }
  };

  const remove = async (h: FeeHead) => {
    try {
      await unwrap(api.delete(`/fees/heads/${h.id}`));
      toast.success(`${h.name} deleted`);
      await reload();
    } catch (err) {
      toast.error(errorText(err, "Could not delete the head"));
    }
  };

  const when = (h: FeeHead) =>
    h.frequency === "Monthly" ? "Every month" : h.oneTimeMonth ? `Once, in ${MONTHS_LONG[h.oneTimeMonth - 1]}` : "Once, on the first challan after admission";

  return (
    <Panel
      flush
      title="Fee heads"
      description="What a challan can charge. Monthly heads appear on every challan; one-time heads once a year or after admission."
      action={
        canEdit ? (
          <Btn size="sm" onClick={() => setForm({ name: "", frequency: "Monthly", oneTimeMonth: null, sortOrder: (heads?.length ?? 0) + 1, isActive: true })}>
            <Plus /> Add head
          </Btn>
        ) : null
      }
    >
      {heads == null ? (
        <TableSkeleton cols={4} />
      ) : heads.length === 0 ? (
        <EmptyState icon={Settings2} title="No fee heads" description="Add Tuition and the other heads your challans charge." />
      ) : (
        <DataTable>
          <THead columns={[{ label: "Head" }, { label: "When" }, { label: "Owner" }, { label: "Status" }, { label: "", align: "right" }]} />
          <tbody>
            {heads.map((h) => (
              <Tr key={h.id}>
                <Td className="font-medium">{h.name}</Td>
                <Td className="text-[var(--muted-foreground)]">{when(h)}</Td>
                <Td>
                  <OwnerPill campusCode={h.campusId == null ? null : "This campus"} />
                </Td>
                <Td>
                  <StatusPill tone={h.isActive ? "success" : "neutral"} dot>
                    {h.isActive ? "Active" : "Inactive"}
                  </StatusPill>
                </Td>
                <Td className="text-right">
                  {editable(h) ? (
                    <div className="inline-flex gap-1">
                      <IconButton
                        label={`Edit ${h.name}`}
                        onClick={() =>
                          setForm({ id: h.id, name: h.name, frequency: h.frequency, oneTimeMonth: h.oneTimeMonth ?? null, sortOrder: h.sortOrder, isActive: h.isActive })
                        }
                      >
                        <Pencil />
                      </IconButton>
                      <IconButton label={`Delete ${h.name}`} tone="danger" onClick={() => void remove(h)}>
                        <Trash2 />
                      </IconButton>
                    </div>
                  ) : null}
                </Td>
              </Tr>
            ))}
          </tbody>
        </DataTable>
      )}

      <Modal
        open={form != null}
        title={form?.id ? "Edit fee head" : "Add fee head"}
        description={mainCampus ? "Main Campus heads are used by every campus." : "This head is used by this campus only."}
        onClose={() => setForm(null)}
        footer={
          <>
            <Btn variant="outline" onClick={() => setForm(null)}>
              Cancel
            </Btn>
            <Btn disabled={busy || !form?.name.trim()} onClick={() => void save()}>
              <Save /> Save
            </Btn>
          </>
        }
      >
        {form ? (
          <div className="space-y-3">
            <Field label="Name">
              <TextInput autoFocus value={form.name} maxLength={60} onChange={(e) => setForm({ ...form, name: e.target.value })} placeholder="e.g. Tuition" />
            </Field>
            <div className="grid gap-3 sm:grid-cols-2">
              <Field label="Charged">
                <Select value={form.frequency} onChange={(e) => setForm({ ...form, frequency: e.target.value as FeeFrequency })}>
                  <option value="Monthly">Every month</option>
                  <option value="OneTime">Once</option>
                </Select>
              </Field>
              {form.frequency === "OneTime" ? (
                <Field label="In">
                  <Select value={form.oneTimeMonth ?? 0} onChange={(e) => setForm({ ...form, oneTimeMonth: Number(e.target.value) || null })}>
                    <option value={0}>First challan after admission</option>
                    {MONTHS_LONG.map((m, i) => (
                      <option key={m} value={i + 1}>
                        {m}
                      </option>
                    ))}
                  </Select>
                </Field>
              ) : null}
              <Field label="Order">
                <TextInput type="number" min={0} value={form.sortOrder} onChange={(e) => setForm({ ...form, sortOrder: Number(e.target.value) })} />
              </Field>
            </div>
            <Switch checked={form.isActive} onChange={(v) => setForm({ ...form, isActive: v })} label="Active" description="Inactive heads are not charged." />
          </div>
        ) : null}
      </Modal>
    </Panel>
  );
}

// ---------------------------------------------------------------- structure

function StructurePanel({ canEdit, mainCampus }: { canEdit: boolean; mainCampus: boolean }) {
  const sessions = useSessions();
  const [sessionId, setSessionId] = useState(0);
  const [grid, setGrid] = useState<StructureGrid | null>(null);
  const [edits, setEdits] = useState<Record<string, string>>({});
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (!sessionId && sessions) setSessionId(sessions.effectiveCurrentId ?? sessions.items[0]?.id ?? 0);
  }, [sessions, sessionId]);

  const load = useCallback(async () => {
    if (!sessionId) return;
    try {
      setGrid((await unwrap(api.get(`/fees/structure?sessionId=${sessionId}`))) as StructureGrid);
      setEdits({});
    } catch (err) {
      toast.error(errorText(err, "Could not load the amounts"));
    }
  }, [sessionId]);

  useEffect(() => {
    void load();
  }, [load]);

  const heads = useMemo(() => grid?.heads.filter((h) => h.isActive) ?? [], [grid]);
  const key = (c: number, h: number) => `${c}:${h}`;
  const dirty = Object.keys(edits).length;

  const save = async (rows: { classId: number; feeHeadId: number; amount: number | null }[]) => {
    setBusy(true);
    try {
      setGrid((await unwrap(api.put("/fees/structure", { sessionId, mainCampus, rows }))) as StructureGrid);
      setEdits({});
      toast.success("Amounts saved");
    } catch (err) {
      toast.error(errorText(err, "Could not save the amounts"));
    } finally {
      setBusy(false);
    }
  };

  const saveEdits = () =>
    save(
      Object.entries(edits).map(([k, v]) => {
        const [classId, feeHeadId] = k.split(":").map(Number);
        return { classId, feeHeadId, amount: v.trim() === "" ? null : Number(v) };
      }),
    );

  return (
    <Panel
      flush
      title="Class-wise amounts"
      description={
        mainCampus
          ? "Main Campus amounts per class. Leave a cell empty to not charge that head."
          : "Amounts this campus charges. Cells marked with a dot are this campus's own; others come from Main Campus."
      }
      action={
        <div className="flex items-center gap-2">
          <Select aria-label="Session" value={sessionId} onChange={(e) => setSessionId(Number(e.target.value))} className="w-auto">
            {sessions?.items.map((s) => (
              <option key={s.id} value={s.id}>
                {s.name}
              </option>
            ))}
          </Select>
          {canEdit ? (
            <Btn size="sm" disabled={busy || dirty === 0} onClick={() => void saveEdits()}>
              <Save /> Save{dirty ? ` (${dirty})` : ""}
            </Btn>
          ) : null}
        </div>
      }
    >
      {grid == null ? (
        <TableSkeleton cols={5} />
      ) : grid.rows.length === 0 || heads.length === 0 ? (
        <EmptyState icon={Settings2} title="Nothing to set yet" description="Add classes and active fee heads first." />
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full min-w-[560px] text-left text-sm">
            <thead>
              <tr className="border-b border-[var(--border)] bg-[var(--muted)]/60 text-xs text-[var(--muted-foreground)]">
                <th className="sticky left-0 bg-[var(--muted)] px-4 py-2.5 font-medium">Class</th>
                {heads.map((h) => (
                  <th key={h.id} className="px-3 py-2.5 font-medium">
                    {h.name}
                    <div className="font-normal">{h.frequency === "Monthly" ? "monthly" : "once"}</div>
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {grid.rows.map((r) => (
                <tr key={r.classId} className="border-b border-[var(--border)] last:border-0">
                  <td className="sticky left-0 bg-[var(--card)] px-4 py-2 font-medium">{r.className}</td>
                  {heads.map((h) => {
                    const c = r.cells.find((x) => x.feeHeadId === h.id);
                    const own = mainCampus ? c?.mainAmount : c?.campusAmount;
                    const k = key(r.classId, h.id);
                    const value = k in edits ? edits[k] : own != null ? String(own) : "";
                    const inherited = !mainCampus && c?.campusAmount == null && c?.mainAmount != null;
                    return (
                      <td key={h.id} className="px-3 py-1.5">
                        <div className="flex items-center gap-1">
                          {!mainCampus && c?.campusAmount != null ? (
                            <span className="size-1.5 shrink-0 rounded-full bg-[var(--primary)]" title="This campus's amount" />
                          ) : null}
                          <TextInput
                            aria-label={`${h.name} for ${r.className}`}
                            type="number"
                            min={0}
                            inputMode="decimal"
                            disabled={!canEdit}
                            className={cn("h-8 w-24 tabular", k in edits && "border-[color:var(--primary)]")}
                            value={value}
                            placeholder={inherited ? String(c?.mainAmount) : "—"}
                            onChange={(e) => setEdits((x) => ({ ...x, [k]: e.target.value }))}
                          />
                          {canEdit && !mainCampus && c?.campusAmount != null ? (
                            <IconButton label={`Use the Main Campus amount for ${h.name}, ${r.className}`} disabled={busy}
                              onClick={() => void save([{ classId: r.classId, feeHeadId: h.id, amount: null }])}>
                              <RotateCcw />
                            </IconButton>
                          ) : null}
                        </div>
                        {inherited ? <div className="mt-0.5 text-[11px] text-[var(--muted-foreground)]">Main Campus {rs(c?.mainAmount)}</div> : null}
                      </td>
                    );
                  })}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </Panel>
  );
}

// ---------------------------------------------------------------- settings

function SettingsPanel({ canEdit, mainCampus }: { canEdit: boolean; mainCampus: boolean }) {
  const { settings, reload } = useFeeSettings();
  const { heads } = useFeeHeads();
  const [form, setForm] = useState<FeeSettings | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (settings) setForm(settings);
  }, [settings]);

  const save = async () => {
    if (!form || !settings) return;
    setBusy(true);
    // A campus sends only the fields it owns or changed; empty ones keep following Main Campus.
    const keep = (k: keyof FeeSettings) =>
      mainCampus || settings.overriddenFields.includes(k) || JSON.stringify(form[k] ?? null) !== JSON.stringify(settings[k] ?? null);
    const pick = <T,>(k: keyof FeeSettings, v: T) => (keep(k) ? v : null);
    try {
      await unwrap(
        api.put("/fees/settings", {
          dueDay: pick("dueDay", Number(form.dueDay)),
          lateFine: pick("lateFine", Number(form.lateFine)),
          siblingSecondPercent: pick("siblingSecondPercent", Number(form.siblingSecondPercent)),
          siblingThirdPlusPercent: pick("siblingThirdPlusPercent", Number(form.siblingThirdPlusPercent)),
          staffChildPercent: pick("staffChildPercent", Number(form.staffChildPercent)),
          concessionHeadIds: pick("concessionHeadIds", form.concessionHeadIds),
          bankName: pick("bankName", form.bankName || null),
          accountTitle: pick("accountTitle", form.accountTitle || null),
          accountNo: pick("accountNo", form.accountNo || null),
          challanNote: pick("challanNote", form.challanNote || null),
          mainCampus,
        }),
      );
      toast.success("Fee settings saved");
      await reload();
    } catch (err) {
      toast.error(errorText(err, "Could not save the settings"));
    } finally {
      setBusy(false);
    }
  };

  const reset = async () => {
    setBusy(true);
    try {
      await unwrap(api.delete("/fees/settings"));
      toast.success("Using the Main Campus settings again");
      await reload();
    } catch (err) {
      toast.error(errorText(err, "Could not reset"));
    } finally {
      setBusy(false);
    }
  };

  if (!form) return <Skeleton className="h-80 rounded-xl" />;
  const n = (k: keyof FeeSettings, label: string, hint?: string, max?: number) => (
    <Field label={label} hint={hint}>
      <TextInput type="number" min={0} max={max} disabled={!canEdit} value={form[k] as number}
        onChange={(e) => setForm({ ...form, [k]: e.target.value === "" ? 0 : Number(e.target.value) })} />
    </Field>
  );

  return (
    <div className="grid gap-4 xl:grid-cols-2">
      <Panel
        title="Due date and discounts"
        description={mainCampus ? "Defaults for every campus." : "Fields you change here become this campus's own; the rest keep following Main Campus."}
        action={!mainCampus ? <OwnerPill campusCode={form.hasCampusOverride ? "This campus" : null} /> : null}
      >
        <div className="grid gap-3 sm:grid-cols-2">
          {n("dueDay", "Due day of the month", "1 to 28", 28)}
          {n("lateFine", "Late fine (Rs)", "Added once when paid after the due date")}
          {n("siblingSecondPercent", "Second child discount %", "Brothers and sisters on this campus", 100)}
          {n("siblingThirdPlusPercent", "Third and later child %", undefined, 100)}
          {n("staffChildPercent", "Staff child discount %", "Used when you confirm a staff child", 100)}
        </div>
        <div className="mt-4">
          <div className="mb-1.5 text-sm font-medium">Discounts apply to</div>
          <div className="flex flex-wrap gap-2">
            {(heads ?? []).filter((h) => h.isActive).map((h) => {
              const on = form.concessionHeadIds.includes(h.id);
              return (
                <label key={h.id} className={cn("inline-flex cursor-pointer items-center gap-2 rounded-lg border px-3 py-1.5 text-sm",
                  on ? "border-[color:var(--primary)] bg-[var(--primary-soft)]" : "border-[var(--border)]")}>
                  <input type="checkbox" className="size-4 accent-[var(--primary)]" disabled={!canEdit} checked={on}
                    onChange={() => setForm({ ...form, concessionHeadIds: on ? form.concessionHeadIds.filter((x) => x !== h.id) : [...form.concessionHeadIds, h.id] })} />
                  {h.name}
                </label>
              );
            })}
          </div>
        </div>
      </Panel>

      <Panel title="Challan details" description="Printed on every challan.">
        <div className="space-y-3">
          <Field label="Bank name">
            <TextInput disabled={!canEdit} maxLength={100} value={form.bankName ?? ""} onChange={(e) => setForm({ ...form, bankName: e.target.value })} />
          </Field>
          <div className="grid gap-3 sm:grid-cols-2">
            <Field label="Account title">
              <TextInput disabled={!canEdit} maxLength={100} value={form.accountTitle ?? ""} onChange={(e) => setForm({ ...form, accountTitle: e.target.value })} />
            </Field>
            <Field label="Account number">
              <TextInput disabled={!canEdit} maxLength={100} value={form.accountNo ?? ""} onChange={(e) => setForm({ ...form, accountNo: e.target.value })} />
            </Field>
          </div>
          <Field label="Note on the challan" hint="e.g. Pay at any branch. Keep the parent copy.">
            <TextArea disabled={!canEdit} rows={3} maxLength={500} value={form.challanNote ?? ""} onChange={(e) => setForm({ ...form, challanNote: e.target.value })} />
          </Field>
        </div>
        {canEdit ? (
          <div className="mt-4 flex flex-wrap justify-end gap-2">
            {!mainCampus && form.hasCampusOverride ? (
              <Btn variant="outline" size="sm" disabled={busy} onClick={() => void reset()}>
                <RotateCcw /> Use Main Campus settings
              </Btn>
            ) : null}
            <Btn size="sm" disabled={busy || form.dueDay < 1 || form.dueDay > 28} onClick={() => void save()}>
              <Save /> Save settings
            </Btn>
          </div>
        ) : null}
      </Panel>
    </div>
  );
}
