import { useCallback, useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { CalendarRange, FileText, Plus } from "lucide-react";
import { toast } from "sonner";
import { Modal } from "@/components/modal";
import { OwnerPill } from "@/components/owner-pill";
import {
  Btn,
  CheckMark,
  EmptyState,
  Field,
  FilterBar,
  NoResults,
  PageHeader,
  SearchField,
  Select,
  Skeleton,
  StatusPill,
  Switch,
  TextInput,
} from "@/components/sms-ui";
import { api, unwrap } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import { matches, useUrlFilters } from "@/lib/filters";
import { cn } from "@/lib/utils";
import type { Exam, ExamDetail, ExamStatus, ExamType, SchoolClass } from "@/lib/types";
import { examTypeLabel } from "@/lib/types";
import { errorText, formatDate } from "@/pages/hiring/shared";
import { ExamTypePill, examStatusTone, useSessions } from "./shared";

const TYPES: ExamType[] = ["Monthly", "MidTerm", "Final", "Other"];
const STATUSES: ExamStatus[] = ["Draft", "Open", "Closed"];

export function ExamsPage() {
  const { user, can } = useAuth();
  const navigate = useNavigate();
  const sessions = useSessions();
  const { values: f, set, activeCount } = useUrlFilters({ sessionId: "", q: "", type: "", status: "" });
  const [exams, setExams] = useState<Exam[] | null>(null);
  const [creating, setCreating] = useState(false);

  // The URL picks the session; without one (or with a stale one) fall back to the current session.
  const sessionId = useMemo(() => {
    if (!sessions) return 0;
    const picked = sessions.items.find((s) => String(s.id) === f.sessionId);
    return picked?.id ?? sessions.effectiveCurrentId ?? sessions.items[0]?.id ?? 0;
  }, [sessions, f.sessionId]);

  const shown = useMemo(
    () => (exams ?? []).filter((e) => (!f.type || e.type === f.type) && (!f.status || e.status === f.status) && matches(f.q, e.name)),
    [exams, f.q, f.type, f.status],
  );
  // Choosing a session picks which list exists; only search, type and status count as filters.
  const filterCount = activeCount - (f.sessionId ? 1 : 0);
  const clearFilters = () => set({ q: "", type: "", status: "" });

  const load = useCallback(async () => {
    if (!sessionId) return;
    setExams(null);
    try {
      setExams((await unwrap(api.get(`/exams?sessionId=${sessionId}`))) as Exam[]);
    } catch (err) {
      toast.error(errorText(err, "Could not load exams"));
      setExams([]);
    }
  }, [sessionId]);

  useEffect(() => {
    void load();
  }, [load]);

  return (
    <div>
      <PageHeader
        title="Exams"
        description="Monthly tests, mid-terms and finals. Open an exam to let teachers enter marks; publish each section's result when it is ready."
        actions={
          can("exams", "Add") && user?.activeCampusId != null ? (
            <Btn onClick={() => setCreating(true)} disabled={!sessionId}>
              <Plus /> New exam
            </Btn>
          ) : null
        }
      />

      <div className="panel mb-4 overflow-hidden">
        <FilterBar
          className="border-b-0"
          shown={exams ? shown.length : null}
          total={exams?.length ?? null}
          noun={["exam", "exams"]}
          activeCount={filterCount}
          onClear={clearFilters}
        >
          <SearchField value={f.q} onChange={(q) => set({ q })} debounce={200} placeholder="Search exams" className="sm:max-w-xs" />
          <Select
            aria-label="Session"
            value={sessionId || ""}
            onChange={(e) => set({ sessionId: Number(e.target.value) === sessions?.effectiveCurrentId ? "" : e.target.value })}
            className="w-auto"
          >
            {sessions?.items.map((s) => (
              <option key={s.id} value={s.id}>
                {s.name}
                {s.id === sessions.effectiveCurrentId ? " · Current" : ""}
              </option>
            ))}
          </Select>
          <Select aria-label="Type" value={f.type} onChange={(e) => set({ type: e.target.value })} className="w-auto">
            <option value="">All types</option>
            {TYPES.map((t) => (
              <option key={t} value={t}>
                {examTypeLabel[t]}
              </option>
            ))}
          </Select>
          <Select aria-label="Status" value={f.status} onChange={(e) => set({ status: e.target.value })} className="w-auto">
            <option value="">All statuses</option>
            {STATUSES.map((s) => (
              <option key={s} value={s}>
                {s}
              </option>
            ))}
          </Select>
        </FilterBar>
      </div>

      {exams == null ? (
        <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
          {[0, 1, 2].map((i) => (
            <Skeleton key={i} className="h-36 rounded-xl" />
          ))}
        </div>
      ) : exams.length === 0 ? (
        <div className="panel">
          <EmptyState icon={FileText} title="No exams yet" description="Create the first exam of this session. Papers are added from each class's subjects." />
        </div>
      ) : shown.length === 0 ? (
        <div className="panel">
          <NoResults noun="exams" icon={FileText} onClear={clearFilters} />
        </div>
      ) : (
        <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
          {shown.map((e) => {
            const pct = e.sectionsTotal ? Math.round((e.sectionsSubmitted * 100) / e.sectionsTotal) : 0;
            return (
              <button
                key={e.id}
                type="button"
                onClick={() => navigate(`/admin/exams/${e.id}`)}
                className="panel flex flex-col gap-3 p-4 text-left transition hover:border-[var(--border-strong)] focus-visible:ring-4 focus-visible:ring-[var(--ring)] outline-none"
              >
                <div className="flex items-start justify-between gap-2">
                  <div className="min-w-0">
                    <div className="truncate font-semibold">{e.name}</div>
                    <div className="mt-1 flex items-center gap-1.5 text-xs text-[var(--muted-foreground)]">
                      <CalendarRange className="size-3.5" />
                      {e.startDate ? `${formatDate(e.startDate)} – ${formatDate(e.endDate)}` : "Dates not set"}
                    </div>
                  </div>
                  <StatusPill tone={examStatusTone[e.status]} dot>
                    {e.status}
                  </StatusPill>
                </div>
                <div className="flex flex-wrap items-center gap-1.5">
                  <ExamTypePill type={e.type} />
                  <OwnerPill campusCode={e.campusCode} />
                  {e.sectionsPublished ? <StatusPill tone="success">{e.sectionsPublished} published</StatusPill> : null}
                </div>
                <div>
                  <div className="mb-1 flex justify-between text-xs text-[var(--muted-foreground)]">
                    <span>Papers submitted</span>
                    <span className="tabular">
                      {e.sectionsSubmitted}/{e.sectionsTotal}
                    </span>
                  </div>
                  <div className="h-1.5 overflow-hidden rounded-full bg-[var(--muted)]">
                    <div className="h-full rounded-full bg-[var(--success)] transition-all" style={{ width: `${pct}%` }} />
                  </div>
                </div>
              </button>
            );
          })}
        </div>
      )}

      {creating ? (
        <NewExamDialog
          sessionId={sessionId}
          isHq={!!user?.isHq}
          onClose={() => setCreating(false)}
          onCreated={(id) => navigate(`/admin/exams/${id}`)}
        />
      ) : null}
    </div>
  );
}

function NewExamDialog({
  sessionId,
  isHq,
  onClose,
  onCreated,
}: {
  sessionId: number;
  isHq: boolean;
  onClose: () => void;
  onCreated: (id: number) => void;
}) {
  const [name, setName] = useState("");
  const [type, setType] = useState<ExamType>("MidTerm");
  const [start, setStart] = useState("");
  const [end, setEnd] = useState("");
  const [classes, setClasses] = useState<SchoolClass[] | null>(null);
  const [picked, setPicked] = useState<number[]>([]);
  const [mainCampus, setMainCampus] = useState(false);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    unwrap(api.get("/classes"))
      .then((c) => {
        const list = (c as SchoolClass[]).filter((x) => x.isActive).sort((a, b) => a.displayOrder - b.displayOrder);
        setClasses(list);
        setPicked(list.map((x) => x.id));
      })
      .catch((err) => toast.error(errorText(err, "Could not load classes")));
  }, []);

  const toggle = (id: number) => setPicked((p) => (p.includes(id) ? p.filter((x) => x !== id) : [...p, id]));

  const save = async () => {
    setBusy(true);
    try {
      const d = (await unwrap(
        api.post("/exams", {
          name: name.trim(),
          type,
          sessionId,
          startDate: start || null,
          endDate: end || null,
          classIds: picked,
          mainCampus,
        }),
      )) as ExamDetail;
      toast.success(`${d.exam.name} created`);
      onCreated(d.exam.id);
    } catch (err) {
      toast.error(errorText(err, "Could not create the exam"));
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal
      open
      wide
      title="New exam"
      description="Papers are created from each class's subjects. You can change marks and dates afterwards."
      onClose={onClose}
      footer={
        <>
          <Btn variant="outline" onClick={onClose}>
            Cancel
          </Btn>
          <Btn onClick={() => void save()} disabled={busy || !name.trim() || picked.length === 0}>
            {busy ? "Creating…" : "Create exam"}
          </Btn>
        </>
      }
    >
      <div className="grid gap-4 sm:grid-cols-2">
        <Field label="Name" required className="sm:col-span-2">
          <TextInput value={name} maxLength={100} placeholder="e.g. Mid-term 2026" onChange={(e) => setName(e.target.value)} autoFocus />
        </Field>
        <Field label="Type" required>
          <Select value={type} onChange={(e) => setType(e.target.value as ExamType)}>
            {TYPES.map((t) => (
              <option key={t} value={t}>
                {examTypeLabel[t]}
              </option>
            ))}
          </Select>
        </Field>
        <div className="grid grid-cols-2 gap-3">
          <Field label="Starts">
            <TextInput type="date" value={start} onChange={(e) => setStart(e.target.value)} />
          </Field>
          <Field label="Ends">
            <TextInput type="date" value={end} min={start || undefined} onChange={(e) => setEnd(e.target.value)} />
          </Field>
        </div>
        <Field label={`Classes (${picked.length})`} className="sm:col-span-2">
          {classes == null ? (
            <Skeleton className="h-20 rounded-lg" />
          ) : (
            <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
              {classes.map((c) => {
                const on = picked.includes(c.id);
                return (
                  <button
                    key={c.id}
                    type="button"
                    aria-pressed={on}
                    onClick={() => toggle(c.id)}
                    className={cn(
                      "flex items-center gap-2 rounded-lg border px-2.5 py-2 text-left text-sm transition outline-none focus-visible:ring-4 focus-visible:ring-[var(--ring)]",
                      on ? "border-[var(--primary)] bg-[var(--primary-soft)]" : "border-[var(--border)] hover:border-[var(--border-strong)]",
                    )}
                  >
                    <CheckMark checked={on} />
                    <span className="truncate">{c.name}</span>
                  </button>
                );
              })}
            </div>
          )}
        </Field>
        {isHq ? (
          <div className="sm:col-span-2">
            <Switch
              checked={mainCampus}
              onChange={setMainCampus}
              label="Main Campus exam"
              description="Shared by every campus; each campus enters marks for its own sections."
            />
          </div>
        ) : null}
      </div>
    </Modal>
  );
}
