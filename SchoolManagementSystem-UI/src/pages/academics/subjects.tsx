import { useEffect, useMemo, useState, type FormEvent } from "react";
import { BookOpen, Pencil, Plus, Trash2 } from "lucide-react";
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
  Switch,
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
import type { SaveSubjectRequest, Subject, SubjectType } from "@/lib/types";

export const subjectTone: Record<SubjectType, StatusTone> = { Theory: "info", Practical: "warning", Activity: "success" };

const emptyForm: SaveSubjectRequest = { code: "", name: "", type: "Theory", isActive: true };

export function SubjectsPage() {
  const { can, user: me } = useAuth();
  const [items, setItems] = useState<Subject[]>([]);
  const [loading, setLoading] = useState(true);
  const { values: f, set, clear, activeCount: filterCount } = useUrlFilters({ q: "", status: "", type: "", owner: "" });
  const [open, setOpen] = useState(false);
  const [editing, setEditing] = useState<Subject | null>(null);
  const [form, setForm] = useState<SaveSubjectRequest>(emptyForm);
  const [owner, setOwner] = useState("hq");
  const [saving, setSaving] = useState(false);
  const [pendingDelete, setPendingDelete] = useState<Subject | null>(null);
  const [deleting, setDeleting] = useState(false);

  const isHq = !!me?.isHq;
  const onAllCampuses = isHq && me?.activeCampusId == null;
  const canAdd = can("subjects", "Add") && (isHq || me?.activeCampusId != null);

  async function load() {
    setLoading(true);
    try {
      setItems((await unwrap(api.get("/subjects"))) as Subject[]);
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Failed to load subjects");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void load();
  }, []);

  const filtered = useMemo(
    () =>
      items.filter((s) => {
        if (f.status === "active" && !s.isActive) return false;
        if (f.status === "inactive" && s.isActive) return false;
        if (f.type && s.type !== f.type) return false;
        if (f.owner === "main" && s.campusId != null) return false;
        if (f.owner === "campus" && s.campusId == null) return false;
        return matches(f.q, s.code, s.name);
      }),
    [items, f.status, f.type, f.owner, f.q],
  );

  function openCreate() {
    setEditing(null);
    setForm(emptyForm);
    setOwner("hq");
    setOpen(true);
  }

  function openEdit(s: Subject) {
    setEditing(s);
    setForm({ code: s.code, name: s.name, type: s.type, isActive: s.isActive });
    setOpen(true);
  }

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setSaving(true);
    try {
      if (editing) {
        await unwrap(api.put(`/subjects/${editing.id}`, form));
        toast.success("Subject updated");
      } else {
        const campusId = onAllCampuses && owner !== "hq" ? Number(owner) : undefined;
        await unwrap(api.post("/subjects", { ...form, campusId }));
        toast.success("Subject created");
      }
      setOpen(false);
      await load();
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Save failed");
    } finally {
      setSaving(false);
    }
  }

  async function confirmDelete() {
    if (!pendingDelete) return;
    setDeleting(true);
    try {
      await unwrap(api.delete(`/subjects/${pendingDelete.id}`));
      toast.success("Subject deleted");
      setPendingDelete(null);
      await load();
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Delete failed");
    } finally {
      setDeleting(false);
    }
  }

  return (
    <div>
      <PageHeader
        title="Subjects"
        description="Everything taught across your classes. Main Campus subjects are shared; a campus can add its own."
        actions={
          canAdd ? (
            <Btn type="button" onClick={openCreate}>
              <Plus />
              Add subject
            </Btn>
          ) : null
        }
      />

      <Panel flush>
        <FilterBar
          shown={loading ? null : filtered.length}
          total={items.length}
          noun={["subject", "subjects"]}
          activeCount={filterCount}
          onClear={clear}
        >
          <SearchField value={f.q} onChange={(q) => set({ q })} debounce={200} placeholder="Search code or name" className="sm:max-w-xs" />
          <Select value={f.status} onChange={(e) => set({ status: e.target.value })} aria-label="Filter by status" className="w-auto">
            <option value="">All statuses</option>
            <option value="active">Active</option>
            <option value="inactive">Inactive</option>
          </Select>
          <Select value={f.type} onChange={(e) => set({ type: e.target.value })} aria-label="Filter by type" className="w-auto">
            <option value="">All types</option>
            <option value="Theory">Theory</option>
            <option value="Practical">Practical</option>
            <option value="Activity">Activity</option>
          </Select>
          <Select value={f.owner} onChange={(e) => set({ owner: e.target.value })} aria-label="Filter by owner" className="w-auto">
            <option value="">All owners</option>
            <option value="main">Main Campus</option>
            <option value="campus">Campus subjects</option>
          </Select>
        </FilterBar>

        {loading ? (
          <TableSkeleton cols={5} />
        ) : items.length === 0 ? (
          <EmptyState
            icon={BookOpen}
            title="No subjects yet"
            description="Add the subjects your classes teach."
            action={
              canAdd ? (
                <Btn type="button" onClick={openCreate}>
                  <Plus />
                  Add subject
                </Btn>
              ) : null
            }
          />
        ) : filtered.length === 0 ? (
          <NoResults noun="subjects" icon={BookOpen} onClear={clear} />
        ) : (
          <DataTable>
            <THead
              columns={[
                { label: "Subject" },
                { label: "Owner" },
                { label: "Type" },
                { label: "Used in" },
                { label: "Status" },
                { label: "Actions", align: "right" },
              ]}
            />
            <tbody>
              {filtered.map((s) => {
                const writable = canWriteOwned(me, s.campusId);
                return (
                  <Tr key={s.id}>
                    <Td>
                      <div className="flex items-center gap-3">
                        <span className="w-14 shrink-0 rounded-md border border-[var(--border)] px-1.5 py-0.5 text-center font-mono text-[11px] text-[var(--muted-foreground)]">
                          {s.code}
                        </span>
                        <span className="font-medium">{s.name}</span>
                      </div>
                    </Td>
                    <Td>
                      <OwnerPill campusCode={s.campusCode} locked={!writable} />
                    </Td>
                    <Td>
                      <StatusPill tone={subjectTone[s.type]}>{s.type}</StatusPill>
                    </Td>
                    <Td className="text-[var(--muted-foreground)]">
                      <span className="tabular">{s.classCount}</span> {s.classCount === 1 ? "class" : "classes"}
                    </Td>
                    <Td>
                      <StatusPill tone={s.isActive ? "success" : "neutral"} dot>
                        {s.isActive ? "Active" : "Inactive"}
                      </StatusPill>
                    </Td>
                    <Td align="right">
                      <div className="flex justify-end gap-0.5">
                        {writable && can("subjects", "Edit") ? (
                          <IconButton label={`Edit ${s.name}`} onClick={() => openEdit(s)}>
                            <Pencil />
                          </IconButton>
                        ) : null}
                        {writable && can("subjects", "Delete") ? (
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
        title={editing ? "Edit subject" : "Add subject"}
        onClose={() => setOpen(false)}
        footer={
          <>
            <Btn type="button" variant="outline" onClick={() => setOpen(false)}>
              Cancel
            </Btn>
            <Btn type="submit" form="subject-form" disabled={saving}>
              {saving ? "Saving…" : editing ? "Save changes" : "Create subject"}
            </Btn>
          </>
        }
      >
        <form id="subject-form" className="space-y-4" onSubmit={onSubmit}>
          <div className="grid gap-4 sm:grid-cols-[120px_1fr]">
            <Field label="Code" required>
              <TextInput
                value={form.code}
                onChange={(e) => setForm({ ...form, code: e.target.value.toUpperCase() })}
                required
                autoFocus
                className="font-mono uppercase"
              />
            </Field>
            <Field label="Name" required>
              <TextInput value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} required />
            </Field>
          </div>
          <Field label="Type" hint="Exams use this later: practicals and activities are marked differently.">
            <Select value={form.type} onChange={(e) => setForm({ ...form, type: e.target.value as SubjectType })}>
              <option value="Theory">Theory</option>
              <option value="Practical">Practical</option>
              <option value="Activity">Activity</option>
            </Select>
          </Field>
          {!editing && onAllCampuses ? (
            <Field label="Owner" hint="Main Campus subjects are available to every campus.">
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
          <Switch
            checked={form.isActive}
            onChange={(v) => setForm({ ...form, isActive: v })}
            label="Subject active"
            description="Inactive subjects are hidden when picking a class's subjects."
          />
        </form>
      </Modal>

      <ConfirmDialog
        open={!!pendingDelete}
        title="Delete subject?"
        description={
          pendingDelete ? `${pendingDelete.name} will be removed. Subjects used by a class cannot be deleted — deactivate them instead.` : ""
        }
        busy={deleting}
        onConfirm={confirmDelete}
        onClose={() => setPendingDelete(null)}
      />
    </div>
  );
}
