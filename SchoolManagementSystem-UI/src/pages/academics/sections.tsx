import { useEffect, useMemo, useState, type FormEvent } from "react";
import { Copy, LayoutGrid, Lock, Plus, Trash2 } from "lucide-react";
import { toast } from "sonner";
import { ConfirmDialog, Modal } from "@/components/modal";
import {
  Btn,
  EmptyState,
  Field,
  IconButton,
  PageHeader,
  Select,
  Skeleton,
  StatusPill,
  Switch,
  TextInput,
} from "@/components/sms-ui";
import { useAuth } from "@/lib/auth";
import { api, unwrap } from "@/lib/api";
import { cn } from "@/lib/utils";
import {
  shiftLabel,
  stageLabel,
  type AcademicSession,
  type AcademicSessionList,
  type CopySectionsResult,
  type SaveSectionRequest,
  type SchoolClass,
  type Section,
  type SectionShift,
} from "@/lib/types";

type Draft = SaveSectionRequest & { id?: number };

export function SectionsPage() {
  const { can, user: me } = useAuth();
  const activeCampusId = me?.activeCampusId ?? null;
  const onAllCampuses = activeCampusId == null;

  const [sessions, setSessions] = useState<AcademicSessionList | null>(null);
  const [sessionId, setSessionId] = useState<number | null>(null);
  const [classes, setClasses] = useState<SchoolClass[]>([]);
  const [classFilter, setClassFilter] = useState<string>("all");
  const [sections, setSections] = useState<Section[] | null>(null);
  const [draft, setDraft] = useState<Draft | null>(null);
  const [saving, setSaving] = useState(false);
  const [pendingDelete, setPendingDelete] = useState<Section | null>(null);
  const [copyOpen, setCopyOpen] = useState(false);
  const [copyFrom, setCopyFrom] = useState<number | null>(null);
  const [preview, setPreview] = useState<CopySectionsResult | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    (async () => {
      try {
        const [list, classList] = await Promise.all([
          unwrap(api.get("/academic-sessions")) as Promise<AcademicSessionList>,
          unwrap(api.get("/classes")) as Promise<SchoolClass[]>,
        ]);
        setSessions(list);
        setClasses(classList);
        setSessionId(list.effectiveCurrentId ?? list.items[0]?.id ?? null);
      } catch (err) {
        toast.error(err instanceof Error ? err.message : "Failed to load academic setup");
      }
    })();
  }, []);

  async function loadSections(id: number) {
    setSections(null);
    try {
      setSections((await unwrap(api.get(`/sections?sessionId=${id}`))) as Section[]);
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Failed to load sections");
      setSections([]);
    }
  }

  useEffect(() => {
    if (sessionId != null) void loadSections(sessionId);
  }, [sessionId]);

  const session: AcademicSession | undefined = sessions?.items.find((s) => s.id === sessionId);
  const closed = session?.status === "Closed";
  const canAdd = can("sections", "Add") && !closed;
  const canEdit = can("sections", "Edit") && !closed;

  // Group sections by class, in promotion order; show every active class so empty ones can be filled.
  const groups = useMemo(() => {
    const byClass = new Map<number, Section[]>();
    for (const s of sections ?? []) byClass.set(s.classId, [...(byClass.get(s.classId) ?? []), s]);
    return classes
      .filter((c) => (classFilter === "all" ? c.isActive || byClass.has(c.id) : String(c.id) === classFilter))
      .filter((c) => c.campusId == null || onAllCampuses || c.campusId === activeCampusId)
      .map((c) => ({ cls: c, items: byClass.get(c.id) ?? [] }));
  }, [classes, sections, classFilter, onAllCampuses, activeCampusId]);

  const total = sections?.length ?? 0;
  const otherSessions = (sessions?.items ?? []).filter((s) => s.id !== sessionId);

  function openNew(classId?: number) {
    setDraft({
      classId: classId ?? classes.find((c) => c.isActive)?.id ?? 0,
      academicSessionId: sessionId,
      name: "",
      shift: "Morning",
      capacity: 35,
      room: "",
      isActive: true,
      campusId: onAllCampuses ? (me?.campuses[0]?.id ?? null) : undefined,
    });
  }

  function openEdit(s: Section) {
    setDraft({
      id: s.id,
      classId: s.classId,
      academicSessionId: s.academicSessionId,
      name: s.name,
      shift: s.shift,
      capacity: s.capacity ?? null,
      room: s.room ?? "",
      isActive: s.isActive,
    });
  }

  async function onSave(e: FormEvent) {
    e.preventDefault();
    if (!draft || sessionId == null) return;
    setSaving(true);
    try {
      const body = { ...draft, room: draft.room?.trim() || null };
      if (draft.id) {
        await unwrap(api.put(`/sections/${draft.id}`, body));
        toast.success("Section updated");
      } else {
        await unwrap(api.post("/sections", body));
        toast.success("Section added");
      }
      setDraft(null);
      await loadSections(sessionId);
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Save failed");
    } finally {
      setSaving(false);
    }
  }

  async function confirmDelete() {
    if (!pendingDelete || sessionId == null) return;
    setBusy(true);
    try {
      await unwrap(api.delete(`/sections/${pendingDelete.id}`));
      toast.success("Section deleted");
      setPendingDelete(null);
      setDraft(null);
      await loadSections(sessionId);
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Delete failed");
    } finally {
      setBusy(false);
    }
  }

  function openCopy() {
    // Default source: the newest session that started before this one.
    const before = otherSessions
      .filter((s) => session && s.startDate < session.startDate)
      .sort((a, b) => b.startDate.localeCompare(a.startDate));
    setCopyFrom((before[0] ?? otherSessions[0])?.id ?? null);
    setPreview(null);
    setCopyOpen(true);
  }

  async function runCopy(dryRun: boolean) {
    if (copyFrom == null || sessionId == null) return;
    setBusy(true);
    try {
      const result = (await unwrap(
        api.post("/sections/copy", { fromSessionId: copyFrom, toSessionId: sessionId, dryRun }),
      )) as CopySectionsResult;
      if (dryRun) {
        setPreview(result);
      } else {
        toast.success(`${result.copied} section${result.copied === 1 ? "" : "s"} copied`);
        setCopyOpen(false);
        await loadSections(sessionId);
      }
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Copy failed");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div>
      <PageHeader
        title="Sections"
        description={
          onAllCampuses
            ? "Every campus's sections for the chosen session. Pick a campus in the top bar to focus on one."
            : "This campus's sections for the chosen session."
        }
        actions={
          <>
            {canAdd && otherSessions.length ? (
              <Btn
                type="button"
                variant="outline"
                onClick={openCopy}
                disabled={onAllCampuses}
                title={onAllCampuses ? "Pick a campus first" : undefined}
              >
                <Copy />
                Copy from last session
              </Btn>
            ) : null}
            {canAdd ? (
              <Btn type="button" onClick={() => openNew()}>
                <Plus />
                Add section
              </Btn>
            ) : null}
          </>
        }
      />

      <div className="mb-5 flex flex-wrap items-center gap-3">
        <Select
          aria-label="Session"
          value={sessionId ?? ""}
          onChange={(e) => setSessionId(Number(e.target.value))}
          className="w-auto min-w-[180px]"
        >
          {(sessions?.items ?? []).map((s) => (
            <option key={s.id} value={s.id}>
              {s.name}
              {s.campusCode ? ` (${s.campusCode})` : ""}
              {s.id === sessions?.effectiveCurrentId ? " · Current" : ""}
            </option>
          ))}
        </Select>
        <Select aria-label="Class" value={classFilter} onChange={(e) => setClassFilter(e.target.value)} className="w-auto min-w-[160px]">
          <option value="all">All classes</option>
          {classes.map((c) => (
            <option key={c.id} value={c.id}>
              {c.name}
            </option>
          ))}
        </Select>
        <span className="text-xs text-[var(--muted-foreground)]">
          <span className="tabular font-medium text-[var(--foreground)]">{total}</span> sections
        </span>
      </div>

      {closed ? (
        <div className="mb-5 flex items-start gap-3 rounded-xl border border-[var(--border)] bg-[var(--warning-soft)] px-4 py-3 text-sm text-[var(--warning)]">
          <Lock className="mt-0.5 size-4 shrink-0" />
          This session is closed. Sections are read-only.
        </div>
      ) : null}

      {sections == null ? (
        <div className="grid gap-4 md:grid-cols-2">
          {[1, 2, 3, 4].map((i) => (
            <div key={i} className="panel space-y-3 p-5">
              <Skeleton className="h-4 w-1/3" />
              <div className="flex gap-2">
                <Skeleton className="h-14 w-24 rounded-lg" />
                <Skeleton className="h-14 w-24 rounded-lg" />
              </div>
            </div>
          ))}
        </div>
      ) : total === 0 && classFilter === "all" ? (
        <div className="panel">
          <EmptyState
            icon={LayoutGrid}
            title="No sections in this session"
            description={canAdd ? "Copy last year's sections in one step, or add them class by class." : "Nothing has been set up yet."}
            action={
              canAdd && otherSessions.length && !onAllCampuses ? (
                <Btn type="button" onClick={openCopy}>
                  <Copy />
                  Copy from last session
                </Btn>
              ) : null
            }
          />
        </div>
      ) : (
        <div className="grid gap-4 md:grid-cols-2">
          {groups.map(({ cls, items }) => (
            <section key={cls.id} className={cn("panel flex flex-col", !cls.isActive && "opacity-70")}>
              <header className="flex items-center justify-between gap-3 border-b border-[var(--border)] px-5 py-3">
                <div className="flex min-w-0 items-center gap-2.5">
                  <span className="font-semibold">{cls.name}</span>
                  <StatusPill>{stageLabel[cls.stage]}</StatusPill>
                  {!cls.isActive ? <StatusPill tone="warning">Inactive</StatusPill> : null}
                </div>
                <div className="flex items-center gap-2">
                  <span className="tabular text-xs text-[var(--muted-foreground)]">{items.length}</span>
                  {canAdd && cls.isActive ? (
                    <IconButton label={`Add section to ${cls.name}`} onClick={() => openNew(cls.id)}>
                      <Plus />
                    </IconButton>
                  ) : null}
                </div>
              </header>
              <div className="flex flex-wrap gap-2 p-4">
                {items.length === 0 ? (
                  <span className="px-1 py-2 text-sm text-[var(--muted-foreground)] italic">No sections</span>
                ) : (
                  items.map((s) => (
                    <button
                      key={s.id}
                      type="button"
                      onClick={() => (canEdit ? openEdit(s) : undefined)}
                      className={cn(
                        "min-w-[96px] rounded-lg border border-[var(--border)] bg-[var(--card)] px-3 py-2 text-left transition outline-none focus-visible:ring-4 focus-visible:ring-[var(--ring)]",
                        canEdit && "hover:border-[var(--primary)]/50 hover:shadow-[var(--shadow-sm)]",
                        !s.isActive && "opacity-60",
                      )}
                    >
                      <div className="flex items-center gap-1.5">
                        <span className="text-sm font-semibold">{s.name}</span>
                        {onAllCampuses ? (
                          <span className="rounded border border-[var(--border)] px-1 font-mono text-[10px] text-[var(--muted-foreground)]">
                            {s.campusCode}
                          </span>
                        ) : null}
                      </div>
                      <div className="mt-0.5 text-xs text-[var(--muted-foreground)]">
                        {[shiftLabel[s.shift], s.capacity != null ? `${s.capacity} seats` : null, s.room].filter(Boolean).join(" · ")}
                      </div>
                    </button>
                  ))
                )}
              </div>
            </section>
          ))}
        </div>
      )}

      <Modal
        open={!!draft}
        title={draft?.id ? "Edit section" : "Add section"}
        description={session ? `Session ${session.name}` : undefined}
        onClose={() => setDraft(null)}
        footer={
          <div className="flex w-full items-center justify-between gap-2">
            {draft?.id && can("sections", "Delete") && !closed ? (
              <Btn
                type="button"
                variant="ghost"
                className="text-[var(--danger)] hover:text-[var(--danger)]"
                onClick={() => setPendingDelete(sections?.find((s) => s.id === draft.id) ?? null)}
              >
                <Trash2 />
                Delete
              </Btn>
            ) : (
              <span />
            )}
            <div className="flex gap-2">
              <Btn type="button" variant="outline" onClick={() => setDraft(null)}>
                Cancel
              </Btn>
              <Btn type="submit" form="section-form" disabled={saving}>
                {saving ? "Saving…" : draft?.id ? "Save changes" : "Add section"}
              </Btn>
            </div>
          </div>
        }
      >
        {draft ? (
          <form id="section-form" className="space-y-4" onSubmit={onSave}>
            {!draft.id && onAllCampuses ? (
              <Field label="Campus" required>
                <Select value={draft.campusId ?? ""} onChange={(e) => setDraft({ ...draft, campusId: Number(e.target.value) })} required>
                  {me?.campuses.map((c) => (
                    <option key={c.id} value={c.id}>
                      {c.name}
                    </option>
                  ))}
                </Select>
              </Field>
            ) : null}
            <Field label="Class" required>
              <Select value={draft.classId} onChange={(e) => setDraft({ ...draft, classId: Number(e.target.value) })} required>
                {classes
                  .filter((c) => c.isActive || c.id === draft.classId)
                  .filter((c) => c.campusId == null || c.campusId === (draft.campusId ?? activeCampusId))
                  .map((c) => (
                    <option key={c.id} value={c.id}>
                      {c.name}
                    </option>
                  ))}
              </Select>
            </Field>
            <div className="grid gap-4 sm:grid-cols-2">
              <Field label="Section name" required hint="e.g. A, B or Blue">
                <TextInput value={draft.name} onChange={(e) => setDraft({ ...draft, name: e.target.value })} required autoFocus />
              </Field>
              <Field label="Shift">
                <Select value={draft.shift} onChange={(e) => setDraft({ ...draft, shift: e.target.value as SectionShift })}>
                  {(Object.keys(shiftLabel) as SectionShift[]).map((s) => (
                    <option key={s} value={s}>
                      {shiftLabel[s]}
                    </option>
                  ))}
                </Select>
              </Field>
              <Field label="Capacity">
                <TextInput
                  type="number"
                  min={0}
                  value={draft.capacity ?? ""}
                  onChange={(e) => setDraft({ ...draft, capacity: e.target.value === "" ? null : Number(e.target.value) })}
                />
              </Field>
              <Field label="Room">
                <TextInput value={draft.room ?? ""} onChange={(e) => setDraft({ ...draft, room: e.target.value })} placeholder="Optional" />
              </Field>
            </div>
            <Switch checked={draft.isActive} onChange={(v) => setDraft({ ...draft, isActive: v })} label="Section active" />
          </form>
        ) : null}
      </Modal>

      <Modal
        open={copyOpen}
        title="Copy sections"
        description={session ? `Into ${session.name}. Existing sections are skipped.` : undefined}
        onClose={() => setCopyOpen(false)}
        footer={
          <>
            <Btn type="button" variant="outline" onClick={() => setCopyOpen(false)}>
              Cancel
            </Btn>
            {preview ? (
              <Btn type="button" onClick={() => runCopy(false)} disabled={busy || preview.copied === 0}>
                {busy ? "Copying…" : `Copy ${preview.copied} section${preview.copied === 1 ? "" : "s"}`}
              </Btn>
            ) : (
              <Btn type="button" onClick={() => runCopy(true)} disabled={busy || copyFrom == null}>
                {busy ? "Checking…" : "Preview"}
              </Btn>
            )}
          </>
        }
      >
        <div className="space-y-4">
          <Field label="Copy from">
            <Select
              value={copyFrom ?? ""}
              onChange={(e) => {
                setCopyFrom(Number(e.target.value));
                setPreview(null);
              }}
            >
              {otherSessions.map((s) => (
                <option key={s.id} value={s.id}>
                  {s.name}
                  {s.campusCode ? ` (${s.campusCode})` : ""}
                </option>
              ))}
            </Select>
          </Field>
          {preview ? (
            <div className="rounded-lg bg-[var(--muted)] px-4 py-3 text-sm">
              <span className="font-semibold">{preview.copied}</span> section{preview.copied === 1 ? "" : "s"} will be copied
              {preview.skipped ? (
                <>
                  , <span className="font-semibold">{preview.skipped}</span> already exist
                </>
              ) : null}
              .
            </div>
          ) : null}
        </div>
      </Modal>

      <ConfirmDialog
        open={!!pendingDelete}
        title="Delete section?"
        description={pendingDelete ? `${pendingDelete.className} ${pendingDelete.name} will be removed.` : ""}
        busy={busy}
        onConfirm={confirmDelete}
        onClose={() => setPendingDelete(null)}
      />
    </div>
  );
}
