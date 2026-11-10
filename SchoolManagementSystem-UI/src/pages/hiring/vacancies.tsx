import { useCallback, useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { CalendarClock, Megaphone, Plus, Users } from "lucide-react";
import { toast } from "sonner";
import { Modal } from "@/components/modal";
import {
  Btn,
  ChoiceCard,
  EmptyState,
  Field,
  FilterBar,
  NoResults,
  PageHeader,
  SearchField,
  Select,
  Skeleton,
  StatusPill,
  TextArea,
  TextInput,
} from "@/components/sms-ui";
import { useAuth } from "@/lib/auth";
import { api, unwrap } from "@/lib/api";
import { matches, useUrlFilters } from "@/lib/filters";
import { cn } from "@/lib/utils";
import {
  categoryLabel,
  designationLabel,
  teachingDesignations,
  vacancyStatusLabel,
  type SaveVacancyRequest,
  type SchoolClass,
  type StaffCategory,
  type StaffDesignation,
  type Subject,
  type Vacancy,
  type VacancyStatus,
} from "@/lib/types";
import { ActionMenu, errorText, formatDate, todayIso, vacancyTone, type MenuItemDef } from "./shared";

type Filter = VacancyStatus | "All";
const FILTERS: Filter[] = ["Open", "OnHold", "Closed", "Filled", "All"];

export function VacanciesPage() {
  const { can, user: me } = useAuth();
  const navigate = useNavigate();
  const onAllCampuses = me?.activeCampusId == null;
  const { values: f, set, clear, activeCount } = useUrlFilters({ tab: "Open", q: "", category: "" });
  const filter: Filter = (FILTERS as string[]).includes(f.tab) ? (f.tab as Filter) : "Open";
  const [items, setItems] = useState<Vacancy[] | null>(null);
  const [editing, setEditing] = useState<Vacancy | "new" | null>(null);

  const load = useCallback(async () => {
    setItems(null);
    try {
      const query = filter === "All" ? "" : `?status=${filter}`;
      setItems((await unwrap(api.get(`/vacancies${query}`))) as Vacancy[]);
    } catch (err) {
      toast.error(errorText(err, "Failed to load vacancies"));
      setItems([]);
    }
  }, [filter]);

  useEffect(() => {
    void load();
  }, [load]);

  const canAdd = can("vacancies", "Add");
  const canEdit = can("vacancies", "Edit");

  const shown = useMemo(
    () => (items ?? []).filter((v) => (!f.category || v.category === f.category) && matches(f.q, v.title, designationLabel[v.designation])),
    [items, f.q, f.category],
  );

  const setStatus = async (v: Vacancy, status: VacancyStatus) => {
    try {
      await unwrap(api.post(`/vacancies/${v.id}/status`, { status }));
      toast.success(`${v.title}: ${vacancyStatusLabel[status].toLowerCase()}`);
      await load();
    } catch (err) {
      toast.error(errorText(err, "Could not change the status"));
    }
  };

  return (
    <div>
      <PageHeader
        title="Vacancies"
        description={onAllCampuses ? "Open positions across every campus." : "Open positions on this campus. Click one to see its candidates."}
        actions={
          canAdd ? (
            <Btn type="button" onClick={() => setEditing("new")} disabled={onAllCampuses} title={onAllCampuses ? "Pick a campus first" : undefined}>
              <Plus /> New vacancy
            </Btn>
          ) : null
        }
      />

      <div className="mb-4 flex gap-1 overflow-x-auto border-b border-[var(--border)]">
        {FILTERS.map((t) => (
          <button
            key={t}
            type="button"
            aria-pressed={filter === t}
            onClick={() => set({ tab: t })}
            className={cn(
              "-mb-px shrink-0 border-b-2 px-3 py-2 text-sm font-medium transition-colors",
              filter === t
                ? "border-[var(--primary)] text-[var(--foreground)]"
                : "border-transparent text-[var(--muted-foreground)] hover:text-[var(--foreground)]",
            )}
          >
            {t === "All" ? "All" : vacancyStatusLabel[t]}
          </button>
        ))}
      </div>

      <div className="panel mb-4 overflow-hidden">
        <FilterBar
          className="border-b-0"
          shown={items ? shown.length : null}
          total={items?.length ?? null}
          noun={["vacancy", "vacancies"]}
          activeCount={activeCount}
          onClear={clear}
        >
          <SearchField value={f.q} onChange={(q) => set({ q })} debounce={200} placeholder="Search by title" className="sm:max-w-xs" />
          <Select aria-label="Category" value={f.category} onChange={(e) => set({ category: e.target.value })} className="w-auto">
            <option value="">All categories</option>
            {(Object.keys(categoryLabel) as StaffCategory[]).map((c) => (
              <option key={c} value={c}>
                {categoryLabel[c]}
              </option>
            ))}
          </Select>
        </FilterBar>
      </div>

      {items == null ? (
        <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
          {[0, 1, 2].map((i) => (
            <Skeleton key={i} className="h-44 rounded-xl" />
          ))}
        </div>
      ) : items.length === 0 ? (
        <div className="panel">
          <EmptyState
            icon={Megaphone}
            title={filter === "Open" ? "No open vacancies" : "Nothing here"}
            description={filter === "Open" ? "Post a vacancy to start collecting candidates." : "Try another tab."}
            action={
              canAdd && !onAllCampuses && filter === "Open" ? (
                <Btn type="button" onClick={() => setEditing("new")}>
                  <Plus /> New vacancy
                </Btn>
              ) : null
            }
          />
        </div>
      ) : shown.length === 0 ? (
        <div className="panel">
          <NoResults noun="vacancies" icon={Megaphone} onClear={clear} />
        </div>
      ) : (
        <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
          {shown.map((v) => {
            const pct = Math.min(100, Math.round((v.hired / Math.max(1, v.positions)) * 100));
            const overdue = v.closingDate != null && v.closingDate < todayIso() && v.status === "Open";
            const menu: MenuItemDef[] = canEdit
              ? [
                  { label: "Edit", onSelect: () => setEditing(v) },
                  ...(v.status === "Open"
                    ? [
                        { label: "Put on hold", onSelect: () => void setStatus(v, "OnHold") },
                        { label: "Close", onSelect: () => void setStatus(v, "Closed") },
                      ]
                    : [{ label: "Reopen", onSelect: () => void setStatus(v, "Open") }]),
                  ...(v.status === "OnHold" ? [{ label: "Close", onSelect: () => void setStatus(v, "Closed") }] : []),
                ]
              : [];
            return (
              <div
                key={v.id}
                role="link"
                tabIndex={0}
                onClick={() => navigate(`/admin/hiring/candidates?vacancyId=${v.id}`)}
                onKeyDown={(e) => e.key === "Enter" && navigate(`/admin/hiring/candidates?vacancyId=${v.id}`)}
                className="panel flex cursor-pointer flex-col gap-4 p-5 transition outline-none hover:border-[var(--border-strong)] focus-visible:ring-4 focus-visible:ring-[var(--ring)]"
              >
                <div className="flex items-start justify-between gap-3">
                  <div className="min-w-0">
                    <h3 className="truncate font-semibold">{v.title}</h3>
                    <div className="mt-1 flex flex-wrap items-center gap-1.5">
                      <StatusPill tone="primary">{designationLabel[v.designation]}</StatusPill>
                      <span className="text-xs text-[var(--muted-foreground)]">{categoryLabel[v.category]}</span>
                      {onAllCampuses ? (
                        <span className="rounded-md border border-[var(--border)] px-1.5 py-0.5 font-mono text-[11px]">{v.campusCode}</span>
                      ) : null}
                    </div>
                  </div>
                  <div className="flex items-center gap-1">
                    <StatusPill tone={vacancyTone[v.status]} dot>
                      {vacancyStatusLabel[v.status]}
                    </StatusPill>
                    <ActionMenu items={menu} />
                  </div>
                </div>

                <div>
                  <div className="mb-1.5 flex items-center justify-between text-xs">
                    <span className="flex items-center gap-1.5 text-[var(--muted-foreground)]">
                      <Users className="size-3.5" /> Positions filled
                    </span>
                    <span className="tabular font-medium">
                      {v.hired}/{v.positions}
                    </span>
                  </div>
                  <div className="h-1.5 overflow-hidden rounded-full bg-[var(--muted)]">
                    <div className="h-full rounded-full bg-[var(--primary)] transition-all" style={{ width: `${pct}%` }} />
                  </div>
                </div>

                <div className="flex flex-wrap gap-1.5 text-xs">
                  {(
                    [
                      ["Applied", v.counts.applied],
                      ["Shortlisted", v.counts.shortlisted],
                      ["Interview", v.counts.interview],
                      ["Offered", v.counts.offered],
                    ] as [string, number][]
                  ).map(([label, n]) => (
                    <span key={label} className="rounded-md bg-[var(--muted)] px-2 py-0.5 text-[var(--muted-foreground)]">
                      {label} <span className="tabular font-medium text-[var(--foreground)]">{n}</span>
                    </span>
                  ))}
                </div>

                <div
                  className={cn(
                    "mt-auto flex items-center gap-1.5 text-xs",
                    overdue ? "text-[var(--warning)]" : "text-[var(--muted-foreground)]",
                  )}
                >
                  <CalendarClock className="size-3.5" />
                  {v.closingDate ? `${overdue ? "Closed for applications on" : "Closes"} ${formatDate(v.closingDate)}` : "No closing date"}
                </div>
              </div>
            );
          })}
        </div>
      )}

      {editing ? (
        <VacancyDialog
          vacancy={editing === "new" ? null : editing}
          onClose={() => setEditing(null)}
          onSaved={() => {
            setEditing(null);
            void load();
          }}
        />
      ) : null}
    </div>
  );
}

function VacancyDialog({ vacancy, onClose, onSaved }: { vacancy: Vacancy | null; onClose: () => void; onSaved: () => void }) {
  const [form, setForm] = useState<SaveVacancyRequest>(() => ({
    title: vacancy?.title ?? "",
    designation: vacancy?.designation ?? "Teacher",
    category: vacancy?.category ?? "Teaching",
    positions: vacancy?.positions ?? 1,
    classIds: vacancy?.classIds ?? [],
    subjectIds: vacancy?.subjectIds ?? [],
    requirements: vacancy?.requirements ?? null,
    closingDate: vacancy?.closingDate ?? null,
  }));
  const [classes, setClasses] = useState<SchoolClass[]>([]);
  const [subjects, setSubjects] = useState<Subject[]>([]);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    void (async () => {
      try {
        const [c, s] = await Promise.all([
          unwrap(api.get("/classes")) as Promise<SchoolClass[]>,
          unwrap(api.get("/subjects")) as Promise<Subject[]>,
        ]);
        setClasses(c.filter((x) => x.isActive || form.classIds.includes(x.id)).sort((a, b) => a.displayOrder - b.displayOrder));
        setSubjects(s.filter((x) => x.isActive || form.subjectIds.includes(x.id)));
      } catch {
        /* pickers stay empty */
      }
    })();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const set = <K extends keyof SaveVacancyRequest>(key: K, value: SaveVacancyRequest[K]) => setForm((f) => ({ ...f, [key]: value }));
  const toggle = (key: "classIds" | "subjectIds", id: number) =>
    setForm((f) => ({ ...f, [key]: f[key].includes(id) ? f[key].filter((x) => x !== id) : [...f[key], id] }));
  const teaching = form.category === "Teaching";

  const save = async () => {
    setBusy(true);
    try {
      const body = { ...form, classIds: teaching ? form.classIds : [], subjectIds: teaching ? form.subjectIds : [] };
      if (vacancy) await unwrap(api.put(`/vacancies/${vacancy.id}`, body));
      else await unwrap(api.post("/vacancies", body));
      toast.success(vacancy ? "Vacancy updated" : "Vacancy posted");
      onSaved();
    } catch (err) {
      toast.error(errorText(err, "Could not save the vacancy"));
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal
      open
      wide
      title={vacancy ? "Edit vacancy" : "New vacancy"}
      description="Who you are hiring, how many, and for which classes."
      onClose={onClose}
      footer={
        <>
          <Btn type="button" variant="outline" onClick={onClose}>
            Cancel
          </Btn>
          <Btn type="button" onClick={() => void save()} disabled={busy || !form.title.trim() || form.positions < 1}>
            {busy ? "Saving…" : vacancy ? "Save changes" : "Post vacancy"}
          </Btn>
        </>
      }
    >
      <div className="grid gap-4 sm:grid-cols-2">
        <Field label="Title" required className="sm:col-span-2">
          <TextInput value={form.title} maxLength={150} placeholder="e.g. Maths teacher for Grade 6–8" onChange={(e) => set("title", e.target.value)} autoFocus />
        </Field>
        <Field label="Designation" required>
          <Select
            value={form.designation}
            onChange={(e) => {
              const d = e.target.value as StaffDesignation;
              setForm((f) => ({ ...f, designation: d, category: teachingDesignations.includes(d) ? "Teaching" : "NonTeaching" }));
            }}
          >
            {(Object.keys(designationLabel) as StaffDesignation[]).map((d) => (
              <option key={d} value={d}>
                {designationLabel[d]}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Category" required>
          <Select value={form.category} onChange={(e) => set("category", e.target.value as StaffCategory)}>
            <option value="Teaching">Teaching</option>
            <option value="NonTeaching">Non-teaching</option>
          </Select>
        </Field>
        <Field label="Positions" required hint={vacancy && vacancy.hired > 0 ? `${vacancy.hired} already hired` : undefined}>
          <TextInput
            type="number"
            min={Math.max(1, vacancy?.hired ?? 1)}
            max={500}
            value={form.positions}
            onChange={(e) => set("positions", Number(e.target.value))}
          />
        </Field>
        <Field label="Closing date">
          <TextInput type="date" value={form.closingDate ?? ""} onChange={(e) => set("closingDate", e.target.value || null)} />
        </Field>

        {teaching ? (
          <>
            <div className="sm:col-span-2">
              <div className="mb-1.5 text-[13px] font-medium">Classes</div>
              {classes.length === 0 ? (
                <p className="text-xs text-[var(--muted-foreground)]">No classes set up yet.</p>
              ) : (
                <div className="grid max-h-40 grid-cols-2 gap-2 overflow-y-auto sm:grid-cols-3">
                  {classes.map((c) => (
                    <ChoiceCard key={c.id} checked={form.classIds.includes(c.id)} onToggle={() => toggle("classIds", c.id)} title={c.name} />
                  ))}
                </div>
              )}
            </div>
            <div className="sm:col-span-2">
              <div className="mb-1.5 text-[13px] font-medium">Subjects</div>
              {subjects.length === 0 ? (
                <p className="text-xs text-[var(--muted-foreground)]">No subjects set up yet.</p>
              ) : (
                <div className="grid max-h-40 grid-cols-2 gap-2 overflow-y-auto sm:grid-cols-3">
                  {subjects.map((s) => (
                    <ChoiceCard key={s.id} checked={form.subjectIds.includes(s.id)} onToggle={() => toggle("subjectIds", s.id)} title={s.name} />
                  ))}
                </div>
              )}
            </div>
          </>
        ) : null}

        <Field label="Requirements" className="sm:col-span-2">
          <TextArea
            rows={3}
            maxLength={2000}
            value={form.requirements ?? ""}
            placeholder="Qualification, experience, skills…"
            onChange={(e) => set("requirements", e.target.value || null)}
          />
        </Field>
      </div>
    </Modal>
  );
}
