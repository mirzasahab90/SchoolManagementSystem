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
  IconButton,
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
  Toolbar,
  Tr,
  type StatusTone,
} from "@/components/sms-ui";
import { useAuth } from "@/lib/auth";
import { api, unwrap } from "@/lib/api";
import type { SaveSubjectRequest, Subject, SubjectType } from "@/lib/types";

export const subjectTone: Record<SubjectType, StatusTone> = { Theory: "info", Practical: "warning", Activity: "success" };

const emptyForm: SaveSubjectRequest = { code: "", name: "", type: "Theory", isActive: true };

type StatusFilter = "all" | "active" | "inactive";

export function SubjectsPage() {
  const { can, user: me } = useAuth();
  const [items, setItems] = useState<Subject[]>([]);
  const [loading, setLoading] = useState(true);
  const [query, setQuery] = useState("");
  const [status, setStatus] = useState<StatusFilter>("all");
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

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    return items.filter((s) => {
      if (status === "active" && !s.isActive) return false;
      if (status === "inactive" && s.isActive) return false;
      return !q || `${s.code} ${s.name}`.toLowerCase().includes(q);
    });
  }, [items, query, status]);

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
        description="Everything taught across your classes. Head-office subjects are shared; a campus can add its own."
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
        <Toolbar>
          <div className="flex w-full flex-wrap items-center gap-2 sm:w-auto">
            <SearchField value={query} onChange={setQuery} placeholder="Search code or name" />
            <Select value={status} onChange={(e) => setStatus(e.target.value as StatusFilter)} aria-label="Filter by status" className="w-auto">
              <option value="all">All statuses</option>
              <option value="active">Active</option>
              <option value="inactive">Inactive</option>
            </Select>
          </div>
          <div className="text-xs text-[var(--muted-foreground)]">
            <span className="tabular font-medium text-[var(--foreground)]">{items.length}</span> subjects
          </div>
        </Toolbar>

        {loading ? (
          <TableSkeleton cols={5} />
        ) : filtered.length === 0 ? (
          <EmptyState
            icon={BookOpen}
            title={items.length === 0 ? "No subjects yet" : "No matching subjects"}
            description={items.length === 0 ? "Add the subjects your classes teach." : "Try a different search or filter."}
          />
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
            <Field label="Owner" hint="Head-office subjects are available to every campus.">
              <Select value={owner} onChange={(e) => setOwner(e.target.value)}>
                <option value="hq">Head office (all campuses)</option>
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
