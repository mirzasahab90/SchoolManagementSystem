import { useEffect, useMemo, useState, type FormEvent } from "react";
import { Layers, Lock, Plus, Trash2 } from "lucide-react";
import { toast } from "sonner";
import { ConfirmDialog, Modal } from "@/components/modal";
import { canWriteOwned, OwnerPill } from "@/components/owner-pill";
import {
  Btn,
  ChoiceCard,
  DataTable,
  EmptyState,
  Field,
  FormSection,
  IconButton,
  PageHeader,
  Panel,
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
import { stageLabel, type ClassDetail, type ClassStage, type SaveClassRequest, type SchoolClass, type Subject } from "@/lib/types";

const stageTone: Record<ClassStage, StatusTone> = {
  PrePrimary: "warning",
  Primary: "success",
  Middle: "info",
  Secondary: "primary",
  HigherSecondary: "neutral",
};

type Picked = Map<number, boolean>; // subjectId -> isOptional, in pick order

export function ClassesPage() {
  const { can, user: me } = useAuth();
  const [items, setItems] = useState<SchoolClass[]>([]);
  const [subjects, setSubjects] = useState<Subject[]>([]);
  const [loading, setLoading] = useState(true);
  const [open, setOpen] = useState(false);
  const [editing, setEditing] = useState<SchoolClass | null>(null);
  const [form, setForm] = useState<SaveClassRequest>({ code: "", name: "", stage: "Primary", displayOrder: 1, isActive: true });
  const [owner, setOwner] = useState("hq");
  const [picked, setPicked] = useState<Picked>(new Map());
  const [saving, setSaving] = useState(false);
  const [pendingDelete, setPendingDelete] = useState<SchoolClass | null>(null);
  const [deleting, setDeleting] = useState(false);

  const isHq = !!me?.isHq;
  const onAllCampuses = isHq && me?.activeCampusId == null;
  const canAdd = can("classes", "Add") && (isHq || me?.activeCampusId != null);
  const writable = editing ? canWriteOwned(me, editing.campusId) && can("classes", "Edit") : canAdd;

  async function load() {
    setLoading(true);
    try {
      const [classes, subjectList] = await Promise.all([
        unwrap(api.get("/classes")) as Promise<SchoolClass[]>,
        unwrap(api.get("/subjects")).catch(() => []) as Promise<Subject[]>,
      ]);
      setItems(classes);
      setSubjects(subjectList);
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Failed to load classes");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void load();
  }, []);

  // A class may use HQ subjects plus subjects of its own campus.
  const classCampusId = editing ? (editing.campusId ?? null) : onAllCampuses && owner !== "hq" ? Number(owner) : isHq ? null : (me?.activeCampusId ?? null);
  const usableSubjects = useMemo(
    () => subjects.filter((s) => (s.isActive || picked.has(s.id)) && (s.campusId == null || s.campusId === classCampusId)),
    [subjects, picked, classCampusId],
  );

  function openCreate() {
    setEditing(null);
    const nextOrder = Math.max(0, ...items.map((c) => c.displayOrder)) + 1;
    setForm({ code: "", name: "", stage: "Primary", displayOrder: nextOrder, isActive: true });
    setOwner("hq");
    setPicked(new Map());
    setOpen(true);
  }

  async function openEdit(c: SchoolClass) {
    try {
      const detail = (await unwrap(api.get(`/classes/${c.id}`))) as ClassDetail;
      setEditing(detail.class);
      setForm({
        code: detail.class.code,
        name: detail.class.name,
        stage: detail.class.stage,
        displayOrder: detail.class.displayOrder,
        isActive: detail.class.isActive,
      });
      setPicked(new Map(detail.subjects.map((s) => [s.subjectId, s.isOptional])));
      setOpen(true);
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Failed to load class");
    }
  }

  function toggleSubject(id: number) {
    setPicked((prev) => {
      const next = new Map(prev);
      if (next.has(id)) next.delete(id);
      else next.set(id, false);
      return next;
    });
  }

  function setOptional(id: number, optional: boolean) {
    setPicked((prev) => new Map(prev).set(id, optional));
  }

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setSaving(true);
    try {
      let id = editing?.id;
      if (editing) {
        await unwrap(api.put(`/classes/${editing.id}`, form));
      } else {
        const campusId = onAllCampuses && owner !== "hq" ? Number(owner) : undefined;
        const created = (await unwrap(api.post("/classes", { ...form, campusId }))) as ClassDetail;
        id = created.class.id;
      }
      // Keep the class's existing subject order; newly ticked subjects go to the end.
      const usableIds = new Set(usableSubjects.map((s) => s.id));
      const items = [...picked.entries()]
        .filter(([subjectId]) => usableIds.has(subjectId))
        .map(([subjectId, isOptional]) => ({ subjectId, isOptional }));
      await unwrap(api.put(`/classes/${id}/subjects`, items));
      toast.success(editing ? "Class updated" : "Class created");
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
      await unwrap(api.delete(`/classes/${pendingDelete.id}`));
      toast.success("Class deleted");
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
        title="Classes"
        description="Grades in promotion order and the subjects each one studies. Head-office classes are shared; a campus can add its own."
        actions={
          canAdd ? (
            <Btn type="button" onClick={openCreate}>
              <Plus />
              Add class
            </Btn>
          ) : null
        }
      />

      <Panel flush>
        {loading ? (
          <TableSkeleton cols={6} />
        ) : items.length === 0 ? (
          <EmptyState icon={Layers} title="No classes yet" description="Add the grades your school teaches." />
        ) : (
          <DataTable>
            <THead
              columns={[
                { label: "#", className: "w-12" },
                { label: "Class" },
                { label: "Owner" },
                { label: "Stage" },
                { label: "Subjects" },
                { label: "Sections" },
                { label: "Status" },
                { label: "", align: "right" },
              ]}
            />
            <tbody>
              {items.map((c) => (
                <Tr key={c.id} onClick={() => openEdit(c)}>
                  <Td className="tabular text-[var(--muted-foreground)]">{c.displayOrder}</Td>
                  <Td>
                    <div className="flex items-center gap-3">
                      <span className="w-12 shrink-0 rounded-md border border-[var(--border)] px-1.5 py-0.5 text-center font-mono text-[11px] text-[var(--muted-foreground)]">
                        {c.code}
                      </span>
                      <span className="font-medium">{c.name}</span>
                    </div>
                  </Td>
                  <Td>
                    <OwnerPill campusCode={c.campusCode} locked={!canWriteOwned(me, c.campusId)} />
                  </Td>
                  <Td>
                    <StatusPill tone={stageTone[c.stage]}>{stageLabel[c.stage]}</StatusPill>
                  </Td>
                  <Td className="tabular">{c.subjectCount}</Td>
                  <Td className="tabular">{c.sectionCount}</Td>
                  <Td>
                    <StatusPill tone={c.isActive ? "success" : "neutral"} dot>
                      {c.isActive ? "Active" : "Inactive"}
                    </StatusPill>
                  </Td>
                  <Td align="right">
                    <div onClick={(e) => e.stopPropagation()}>
                      {canWriteOwned(me, c.campusId) && can("classes", "Delete") ? (
                        <IconButton label={`Delete ${c.name}`} tone="danger" onClick={() => setPendingDelete(c)}>
                          <Trash2 />
                        </IconButton>
                      ) : null}
                    </div>
                  </Td>
                </Tr>
              ))}
            </tbody>
          </DataTable>
        )}
      </Panel>

      <Modal
        open={open}
        wide
        title={editing ? editing.name : "Add class"}
        description={editing ? `${picked.size} subject${picked.size === 1 ? "" : "s"}` : "Details first, then pick its subjects."}
        onClose={() => setOpen(false)}
        footer={
          <>
            <Btn type="button" variant="outline" onClick={() => setOpen(false)}>
              {writable ? "Cancel" : "Close"}
            </Btn>
            {writable ? (
              <Btn type="submit" form="class-form" disabled={saving}>
                {saving ? "Saving…" : editing ? "Save changes" : "Create class"}
              </Btn>
            ) : null}
          </>
        }
      >
        <form id="class-form" className="space-y-6" onSubmit={onSubmit}>
          {editing && !canWriteOwned(me, editing.campusId) ? (
            <div className="flex items-start gap-2.5 rounded-lg bg-[var(--info-soft)] px-3 py-2.5 text-sm text-[var(--info)]">
              <Lock className="mt-0.5 size-4 shrink-0" />
              Head-office class. Create your own class to change its subjects.
            </div>
          ) : null}

          <FormSection title="Details">
            <div className="grid gap-4 sm:grid-cols-[120px_1fr]">
              <Field label="Code" required>
                <TextInput
                  value={form.code}
                  onChange={(e) => setForm({ ...form, code: e.target.value.toUpperCase() })}
                  required
                  disabled={!writable}
                  className="font-mono uppercase"
                />
              </Field>
              <Field label="Name" required>
                <TextInput value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} required disabled={!writable} />
              </Field>
            </div>
            <div className="grid gap-4 sm:grid-cols-2">
              <Field label="Stage">
                <Select value={form.stage} onChange={(e) => setForm({ ...form, stage: e.target.value as ClassStage })} disabled={!writable}>
                  {(Object.keys(stageLabel) as ClassStage[]).map((s) => (
                    <option key={s} value={s}>
                      {stageLabel[s]}
                    </option>
                  ))}
                </Select>
              </Field>
              <Field label="Order" hint="Promotion order: lower comes first.">
                <TextInput
                  type="number"
                  min={0}
                  value={form.displayOrder}
                  onChange={(e) => setForm({ ...form, displayOrder: Number(e.target.value) })}
                  disabled={!writable}
                />
              </Field>
            </div>
            {!editing && onAllCampuses ? (
              <Field label="Owner" hint="Head-office classes are available to every campus.">
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
              label="Class active"
              description="New sections can only be added to active classes."
              disabled={!writable}
            />
          </FormSection>

          <FormSection title="Subjects" description="Tick the subjects this class studies; mark electives as optional.">
            {usableSubjects.length === 0 ? (
              <p className="text-sm text-[var(--muted-foreground)]">No subjects yet. Add some on the Subjects page.</p>
            ) : (
              <div className="grid gap-2 sm:grid-cols-2">
                {usableSubjects.map((s) => {
                  const checked = picked.has(s.id);
                  return (
                    <div key={s.id} className="space-y-1.5">
                      <ChoiceCard
                        checked={checked}
                        onToggle={() => toggleSubject(s.id)}
                        title={s.name}
                        description={`${s.code} · ${s.type}`}
                        disabled={!writable}
                        badge={<OwnerPill campusCode={s.campusCode} />}
                      />
                      {checked ? (
                        <div className="pl-7">
                          <Switch
                            checked={picked.get(s.id) ?? false}
                            onChange={(v) => setOptional(s.id, v)}
                            label="Optional"
                            disabled={!writable}
                          />
                        </div>
                      ) : null}
                    </div>
                  );
                })}
              </div>
            )}
          </FormSection>
        </form>
      </Modal>

      <ConfirmDialog
        open={!!pendingDelete}
        title="Delete class?"
        description={pendingDelete ? `${pendingDelete.name} will be removed. Classes with sections cannot be deleted — deactivate them instead.` : ""}
        busy={deleting}
        onConfirm={confirmDelete}
        onClose={() => setPendingDelete(null)}
      />
    </div>
  );
}
