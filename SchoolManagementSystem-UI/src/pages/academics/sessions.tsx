import { useEffect, useMemo, useState, type FormEvent } from "react";
import { CalendarRange, CheckCircle2, Info, Pencil, Plus, Trash2 } from "lucide-react";
import { toast } from "sonner";
import { ConfirmDialog, Modal } from "@/components/modal";
import { canWriteOwned, OwnerPill } from "@/components/owner-pill";
import {
  Btn,
  DataTable,
  EmptyState,
  Field,
  FilterBar,
  IconButton,
  NoResults,
  PageHeader,
  Panel,
  SearchField,
  Select,
  StatusPill,
  TableSkeleton,
  Td,
  TextInput,
  THead,
  Tr,
  type StatusTone,
} from "@/components/sms-ui";
import { useAuth } from "@/lib/auth";
import { api, unwrap } from "@/lib/api";
import { matches, useUrlFilters } from "@/lib/filters";
import type { AcademicSession, AcademicSessionList, SaveAcademicSessionRequest, SessionStatus } from "@/lib/types";

const statusTone: Record<SessionStatus, StatusTone> = { Planned: "neutral", Active: "success", Closed: "warning" };

export function formatDate(value: string) {
  return new Date(`${value}T00:00:00`).toLocaleDateString(undefined, { day: "numeric", month: "short", year: "numeric" });
}

const emptyForm: SaveAcademicSessionRequest = { name: "", startDate: "", endDate: "", status: "Planned" };

export function AcademicSessionsPage() {
  const { can, user: me } = useAuth();
  const [list, setList] = useState<AcademicSessionList | null>(null);
  const [loading, setLoading] = useState(true);
  const [open, setOpen] = useState(false);
  const [editing, setEditing] = useState<AcademicSession | null>(null);
  const [form, setForm] = useState<SaveAcademicSessionRequest>(emptyForm);
  const [owner, setOwner] = useState("hq");
  const { values: f, set, clear, activeCount: filterCount } = useUrlFilters({ q: "", status: "" });
  const [saving, setSaving] = useState(false);
  const [pendingDelete, setPendingDelete] = useState<AcademicSession | null>(null);
  const [busy, setBusy] = useState(false);

  const isHq = !!me?.isHq;
  const onAllCampuses = isHq && me?.activeCampusId == null;
  const canAdd = can("academic-sessions", "Add") && (isHq || me?.activeCampusId != null);

  async function load() {
    setLoading(true);
    try {
      setList((await unwrap(api.get("/academic-sessions"))) as AcademicSessionList);
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Failed to load sessions");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void load();
  }, []);

  const items = list?.items ?? [];
  const effectiveId = list?.effectiveCurrentId ?? null;
  const hqCurrent = items.find((s) => s.campusId == null && s.isCurrent);
  const overridden = effectiveId != null && hqCurrent != null && effectiveId !== hqCurrent.id;
  const writable = (s: AcademicSession) => canWriteOwned(me, s.campusId);
  const filtered = useMemo(
    () => (list?.items ?? []).filter((s) => (!f.status || s.status === f.status) && matches(f.q, s.name, s.campusCode)),
    [list, f.status, f.q],
  );

  function openCreate() {
    setEditing(null);
    setForm(emptyForm);
    setOwner("hq");
    setOpen(true);
  }

  function openEdit(s: AcademicSession) {
    setEditing(s);
    setForm({ name: s.name, startDate: s.startDate, endDate: s.endDate, status: s.status });
    setOpen(true);
  }

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setSaving(true);
    try {
      if (editing) {
        await unwrap(api.put(`/academic-sessions/${editing.id}`, form));
        toast.success("Session updated");
      } else {
        const campusId = onAllCampuses && owner !== "hq" ? Number(owner) : undefined;
        await unwrap(api.post("/academic-sessions", { ...form, campusId }));
        toast.success("Session created");
      }
      setOpen(false);
      await load();
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Save failed");
    } finally {
      setSaving(false);
    }
  }

  async function makeCurrent(s: AcademicSession) {
    setBusy(true);
    try {
      await unwrap(api.post(`/academic-sessions/${s.id}/set-current`));
      toast.success(s.campusId == null && !isHq ? "Your campus now follows the Main Campus session" : `${s.name} is now current`);
      await load();
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Could not change the current session");
    } finally {
      setBusy(false);
    }
  }

  async function confirmDelete() {
    if (!pendingDelete) return;
    setBusy(true);
    try {
      await unwrap(api.delete(`/academic-sessions/${pendingDelete.id}`));
      toast.success("Session deleted");
      setPendingDelete(null);
      await load();
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Delete failed");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div>
      <PageHeader
        title="Academic sessions"
        description="School years such as 2026-27. Main Campus sessions apply to every campus; a campus can run its own calendar if it needs to."
        actions={
          canAdd ? (
            <Btn type="button" onClick={openCreate}>
              <Plus />
              Add session
            </Btn>
          ) : null
        }
      />

      {!isHq && overridden ? (
        <div className="mb-5 flex items-start gap-3 rounded-xl border border-[var(--border)] bg-[var(--info-soft)] px-4 py-3 text-sm text-[var(--info)]">
          <Info className="mt-0.5 size-4 shrink-0" />
          Your campus uses its own current session instead of Main Campus's {hqCurrent?.name}.
        </div>
      ) : null}

      <Panel flush>
        <FilterBar
          shown={loading ? null : filtered.length}
          total={items.length}
          noun={["session", "sessions"]}
          activeCount={filterCount}
          onClear={clear}
        >
          <SearchField value={f.q} onChange={(q) => set({ q })} debounce={200} placeholder="Search sessions" className="sm:max-w-xs" />
          <Select value={f.status} onChange={(e) => set({ status: e.target.value })} aria-label="Filter by status" className="w-auto">
            <option value="">All statuses</option>
            <option value="Planned">Planned</option>
            <option value="Active">Active</option>
            <option value="Closed">Closed</option>
          </Select>
        </FilterBar>

        {loading ? (
          <TableSkeleton cols={5} />
        ) : items.length === 0 ? (
          <EmptyState
            icon={CalendarRange}
            title="No sessions yet"
            description="Add the school year your campuses are running."
            action={
              canAdd ? (
                <Btn type="button" onClick={openCreate}>
                  <Plus />
                  Add session
                </Btn>
              ) : null
            }
          />
        ) : filtered.length === 0 ? (
          <NoResults noun="sessions" icon={CalendarRange} onClear={clear} />
        ) : (
          <DataTable>
            <THead
              columns={[
                { label: "Session" },
                { label: "Owner" },
                { label: "Dates" },
                { label: "Status" },
                { label: "Sections" },
                { label: "Actions", align: "right" },
              ]}
            />
            <tbody>
              {filtered.map((s) => {
                const isEffective = s.id === effectiveId;
                const canSetCurrent =
                  can("academic-sessions", "Edit") &&
                  s.status !== "Closed" &&
                  !isEffective &&
                  // Campus users may "follow Main Campus" again, but only by picking Main Campus's current session.
                  (writable(s) || (s.campusId == null && !isHq && s.isCurrent));
                return (
                  <Tr key={s.id}>
                    <Td>
                      <div className="flex items-center gap-2 font-medium">
                        {s.name}
                        {isEffective ? <StatusPill tone="primary">Current</StatusPill> : null}
                        {!isEffective && s.isCurrent && s.campusId == null ? (
                          <span className="text-xs font-normal text-[var(--muted-foreground)]">Main Campus current</span>
                        ) : null}
                      </div>
                    </Td>
                    <Td>
                      <OwnerPill campusCode={s.campusCode} locked={!writable(s)} />
                    </Td>
                    <Td className="text-[var(--muted-foreground)]">
                      {formatDate(s.startDate)} – {formatDate(s.endDate)}
                    </Td>
                    <Td>
                      <StatusPill tone={statusTone[s.status]} dot>
                        {s.status}
                      </StatusPill>
                    </Td>
                    <Td className="tabular">{s.sectionCount}</Td>
                    <Td align="right">
                      <div className="flex items-center justify-end gap-1">
                        {canSetCurrent ? (
                          <Btn type="button" size="sm" variant="outline" disabled={busy} onClick={() => makeCurrent(s)}>
                            <CheckCircle2 />
                            {s.campusId == null && !isHq ? "Follow Main Campus" : "Set as current"}
                          </Btn>
                        ) : null}
                        {writable(s) && can("academic-sessions", "Edit") ? (
                          <IconButton label={`Edit ${s.name}`} onClick={() => openEdit(s)}>
                            <Pencil />
                          </IconButton>
                        ) : null}
                        {writable(s) && can("academic-sessions", "Delete") && !s.isCurrent ? (
                          <IconButton label={`Delete ${s.name}`} tone="danger" onClick={() => setPendingDelete(s)}>
                            <Trash2 />
                          </IconButton>
                        ) : null}
                      </div>
                    </Td>
                  </Tr>
                );
              })}
            </tbody>
          </DataTable>
        )}
      </Panel>

      <Modal
        open={open}
        title={editing ? "Edit session" : "Add session"}
        description="A closed session keeps its records but can no longer be changed."
        onClose={() => setOpen(false)}
        footer={
          <>
            <Btn type="button" variant="outline" onClick={() => setOpen(false)}>
              Cancel
            </Btn>
            <Btn type="submit" form="session-form" disabled={saving}>
              {saving ? "Saving…" : editing ? "Save changes" : "Create session"}
            </Btn>
          </>
        }
      >
        <form id="session-form" className="space-y-4" onSubmit={onSubmit}>
          <Field label="Name" required hint="For example 2027-28">
            <TextInput value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} required autoFocus />
          </Field>
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="Starts" required>
              <TextInput type="date" value={form.startDate} onChange={(e) => setForm({ ...form, startDate: e.target.value })} required />
            </Field>
            <Field label="Ends" required>
              <TextInput type="date" value={form.endDate} onChange={(e) => setForm({ ...form, endDate: e.target.value })} required />
            </Field>
          </div>
          <Field label="Status">
            <Select value={form.status} onChange={(e) => setForm({ ...form, status: e.target.value as SessionStatus })}>
              <option value="Planned">Planned</option>
              <option value="Active">Active</option>
              <option value="Closed">Closed</option>
            </Select>
          </Field>
          {!editing && onAllCampuses ? (
            <Field label="Owner" hint="Main Campus sessions apply to every campus.">
              <Select value={owner} onChange={(e) => setOwner(e.target.value)}>
                <option value="hq">Main Campus (all campuses)</option>
                {me?.campuses.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.name} only
                  </option>
                ))}
              </Select>
            </Field>
          ) : null}
        </form>
      </Modal>

      <ConfirmDialog
        open={!!pendingDelete}
        title="Delete session?"
        description={pendingDelete ? `${pendingDelete.name} will be removed. Sessions with sections cannot be deleted.` : ""}
        busy={busy}
        onConfirm={confirmDelete}
        onClose={() => setPendingDelete(null)}
      />
    </div>
  );
}
