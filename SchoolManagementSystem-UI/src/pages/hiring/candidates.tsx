import { useCallback, useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { FileText, Kanban, List, Plus, UserSearch } from "lucide-react";
import { toast } from "sonner";
import { Modal } from "@/components/modal";
import {
  Avatar,
  Btn,
  DataTable,
  EmptyState,
  Field,
  FilterBar,
  NoResults,
  Panel,
  PageHeader,
  SearchField,
  Select,
  Skeleton,
  TableSkeleton,
  Td,
  TextInput,
  THead,
  Tr,
} from "@/components/sms-ui";
import { useUrlFilters } from "@/lib/filters";
import { useAuth } from "@/lib/auth";
import { api, unwrap } from "@/lib/api";
import { cn } from "@/lib/utils";
import {
  allowedMoves,
  candidateStageLabel,
  candidateStageOrder,
  sourceLabel,
  type CandidateListItem,
  type CandidateProfile,
  type CandidateSource,
  type CandidateStage,
  type PagedResult,
  type SaveCandidateRequest,
  type Vacancy,
} from "@/lib/types";
import { ActionMenu, ReasonDialog, StagePill, Stars, errorText, type MenuItemDef } from "./shared";

type View = "board" | "list" | "closed";
const VIEW_KEY = "schoolms.candidatesView";
const CV_LIMIT = 10_485_760;
const CV_TYPES = [".pdf", ".doc", ".docx", ".jpg", ".jpeg", ".png"];

function readView(): View {
  try {
    const v = localStorage.getItem(VIEW_KEY);
    return v === "list" ? "list" : "board";
  } catch {
    return "board";
  }
}

function writeView(v: View) {
  try {
    if (v !== "closed") localStorage.setItem(VIEW_KEY, v);
  } catch {
    /* per-viewer convenience only */
  }
}

export function CandidatesPage() {
  const { can, user: me } = useAuth();
  const navigate = useNavigate();
  const { values: f, set: setFilter, clear, activeCount } = useUrlFilters({ q: "", vacancyId: "", source: "", from: "", to: "" });
  const vacancyId = f.vacancyId;
  const debounced = f.q.trim();
  const onAllCampuses = me?.activeCampusId == null;

  const [view, setView] = useState<View>(readView);
  const [vacancies, setVacancies] = useState<Vacancy[]>([]);
  const [result, setResult] = useState<PagedResult<CandidateListItem> | null>(null);
  // Pages of 100 loaded for the main list (active or closed); "Load more" fetches the next one.
  const [loadedPages, setLoadedPages] = useState(1);
  const [loadingMore, setLoadingMore] = useState(false);
  const [adding, setAdding] = useState(false);
  const [reasonFor, setReasonFor] = useState<{ c: CandidateListItem; stage: CandidateStage } | null>(null);

  useEffect(() => {
    unwrap(api.get("/vacancies"))
      .then((v) => setVacancies(v as Vacancy[]))
      .catch(() => setVacancies([]));
  }, []);

  const fetchStage = useCallback(
    (stage: string, size: number, page = 1) => {
      const qs = new URLSearchParams({ pageSize: String(size), stage, page: String(page) });
      if (vacancyId) qs.set("vacancyId", vacancyId);
      if (debounced) qs.set("q", debounced);
      if (f.source) qs.set("source", f.source);
      if (f.from) qs.set("appliedFrom", f.from);
      if (f.to) qs.set("appliedTo", f.to);
      return unwrap(api.get(`/candidates?${qs}`)) as Promise<PagedResult<CandidateListItem>>;
    },
    [vacancyId, debounced, f.source, f.from, f.to],
  );
  const mainStage = view === "closed" ? "closed" : "active";

  // Reloads every page already shown, so a move or add keeps the list where it was.
  const reload = useCallback(
    async (pages: number) => {
      try {
        const main = await Promise.all(Array.from({ length: pages }, (_, i) => fetchStage(mainStage, 100, i + 1)));
        // In-progress candidates first (never crowded out by history); the Hired column shows the latest 20.
        const hired = view === "board" ? await fetchStage("Hired", 20) : null;
        setResult({ items: [...main.flatMap((m) => m.items), ...(hired?.items ?? [])], total: main[0]?.total ?? 0 });
      } catch (err) {
        toast.error(errorText(err, "Failed to load candidates"));
        setResult({ items: [], total: 0 });
      }
    },
    [fetchStage, mainStage, view],
  );
  const load = () => reload(loadedPages);

  // A new view or filter starts again from the first page.
  useEffect(() => {
    setLoadedPages(1);
    setResult(null);
    void reload(1);
  }, [reload]);

  const loadMore = async () => {
    setLoadingMore(true);
    try {
      const next = await fetchStage(mainStage, 100, loadedPages + 1);
      setLoadedPages((n) => n + 1);
      setResult((r) => (r ? { ...r, items: [...r.items, ...next.items] } : r));
    } catch (err) {
      toast.error(errorText(err, "Failed to load more candidates"));
    } finally {
      setLoadingMore(false);
    }
  };

  const changeView = (v: View) => {
    setView(v);
    writeView(v);
  };

  const move = async (c: CandidateListItem, stage: CandidateStage, reason?: string) => {
    try {
      await unwrap(api.post(`/candidates/${c.id}/move`, { stage, reason: reason ?? null }));
      toast.success(`${c.fullName} → ${candidateStageLabel[stage]}`);
      await load();
    } catch (err) {
      toast.error(errorText(err, "Could not move the candidate"));
    }
  };

  const menuFor = (c: CandidateListItem): MenuItemDef[] =>
    can("candidates", "Edit")
      ? allowedMoves[c.stage].map((stage) => ({
          label:
            stage === "Rejected" ? "Reject…" : stage === "Withdrawn" ? "Mark withdrawn…" : stage === "Applied" ? "Reopen…" : `Move to ${candidateStageLabel[stage]}`,
          danger: stage === "Rejected",
          onSelect: () =>
            stage === "Rejected" || stage === "Withdrawn" || stage === "Applied" ? setReasonFor({ c, stage }) : void move(c, stage),
        }))
      : [];

  const items = useMemo(() => result?.items ?? [], [result]);
  const active = items.filter((c) => c.stage !== "Rejected" && c.stage !== "Withdrawn");
  const rows = view === "closed" ? items : active;
  const mainLoaded = view === "closed" ? items.length : items.filter((c) => c.stage !== "Hired" || view !== "board").length;
  const hasMore = result != null && mainLoaded < result.total;
  const selectedVacancy = vacancies.find((v) => String(v.id) === vacancyId);
  const canAdd = can("candidates", "Add") && !onAllCampuses;

  return (
    <div>
      <PageHeader
        title="Candidates"
        description={selectedVacancy ? `Applicants for ${selectedVacancy.title}.` : "Everyone who applied, stage by stage."}
        actions={
          canAdd ? (
            <Btn type="button" onClick={() => setAdding(true)}>
              <Plus /> Add candidate
            </Btn>
          ) : null
        }
      />

      <Panel flush className="mb-4">
        <FilterBar shown={result ? result.total : null} noun={["candidate", "candidates"]} activeCount={activeCount} onClear={clear}>
          <SearchField value={f.q} onChange={(q) => setFilter({ q })} debounce={300} placeholder="Name, phone, CNIC or email" className="sm:max-w-xs" />
          <Select aria-label="Vacancy" value={vacancyId} onChange={(e) => setFilter({ vacancyId: e.target.value })} className="w-auto max-w-[16rem]">
            <option value="">All vacancies</option>
            {vacancies.map((v) => (
              <option key={v.id} value={v.id}>
                {v.title}
                {onAllCampuses ? ` · ${v.campusCode}` : ""}
              </option>
            ))}
          </Select>
          <Select aria-label="Source" value={f.source} onChange={(e) => setFilter({ source: e.target.value })} className="w-auto">
            <option value="">All sources</option>
            {(Object.keys(sourceLabel) as CandidateSource[]).map((src) => (
              <option key={src} value={src}>
                {sourceLabel[src]}
              </option>
            ))}
          </Select>
          <div className="flex items-center gap-1.5 text-xs text-[var(--muted-foreground)]">
            <span>Applied</span>
            <TextInput type="date" aria-label="Applied from" value={f.from} max={f.to || undefined} onChange={(e) => setFilter({ from: e.target.value })} className="w-auto" />
            <span>to</span>
            <TextInput type="date" aria-label="Applied to" value={f.to} min={f.from || undefined} onChange={(e) => setFilter({ to: e.target.value })} className="w-auto" />
          </div>
          <div className="inline-flex rounded-lg border border-[var(--border)] p-0.5" role="tablist" aria-label="View">
            {(
              [
                ["board", "Board", Kanban],
                ["list", "List", List],
                ["closed", "Closed", FileText],
              ] as const
            ).map(([key, label, Icon]) => (
              <button
                key={key}
                type="button"
                role="tab"
                aria-selected={view === key}
                onClick={() => changeView(key)}
                className={cn(
                  "inline-flex h-7 items-center gap-1.5 rounded-md px-2.5 text-xs font-medium transition-colors [&_svg]:size-3.5",
                  view === key ? "bg-[var(--muted)] text-[var(--foreground)]" : "text-[var(--muted-foreground)] hover:text-[var(--foreground)]",
                )}
              >
                <Icon /> {label}
              </button>
            ))}
          </div>
        </FilterBar>
      </Panel>

      {result == null ? (
        view === "board" ? (
          <div className="grid gap-3 md:grid-cols-5">
            {candidateStageOrder.map((s) => (
              <Skeleton key={s} className="h-40 rounded-xl" />
            ))}
          </div>
        ) : (
          <Panel flush>
            <TableSkeleton cols={5} />
          </Panel>
        )
      ) : view === "board" ? (
        <div className="-mx-4 overflow-x-auto px-4 pb-2 sm:mx-0 sm:px-0">
          <div className="grid min-w-[60rem] grid-cols-5 gap-3">
            {candidateStageOrder.map((stage) => {
              const col = active.filter((c) => c.stage === stage);
              return (
                <div key={stage} className="flex min-w-0 flex-col rounded-xl bg-[var(--muted)]/60 p-2">
                  <div className="flex items-center justify-between px-1.5 pt-0.5 pb-2 text-xs font-semibold">
                    <span>{candidateStageLabel[stage]}</span>
                    <span className="tabular rounded-md bg-[var(--card)] px-1.5 py-0.5 text-[var(--muted-foreground)]">{col.length}</span>
                  </div>
                  <div className="flex flex-col gap-2">
                    {col.length === 0 ? (
                      <div className="rounded-lg border border-dashed border-[var(--border)] px-3 py-6 text-center text-xs text-[var(--muted-foreground)]">
                        No one here
                      </div>
                    ) : (
                      col.map((c) => (
                        <div
                          key={c.id}
                          role="link"
                          tabIndex={0}
                          onClick={() => navigate(`/admin/hiring/candidates/${c.id}`)}
                          onKeyDown={(e) => e.key === "Enter" && navigate(`/admin/hiring/candidates/${c.id}`)}
                          className="cursor-pointer rounded-lg border border-[var(--border)] bg-[var(--card)] p-3 shadow-[var(--shadow-xs)] transition outline-none hover:border-[var(--border-strong)] focus-visible:ring-4 focus-visible:ring-[var(--ring)]"
                        >
                          <div className="flex items-start justify-between gap-1">
                            <div className="min-w-0">
                              <div className="truncate text-sm font-medium">{c.fullName}</div>
                              {!vacancyId ? <div className="truncate text-xs text-[var(--muted-foreground)]">{c.vacancyTitle}</div> : null}
                            </div>
                            <ActionMenu items={menuFor(c)} label="Move to…" />
                          </div>
                          <div className="mt-2 space-y-1 text-xs text-[var(--muted-foreground)]">
                            {c.qualification ? <div className="truncate">{c.qualification}</div> : null}
                            <div className="flex items-center justify-between gap-2">
                              <span>{c.experienceYears != null ? `${c.experienceYears} yrs exp.` : "—"}</span>
                              <span className="flex items-center gap-1.5">
                                <Stars value={c.latestRating} />
                                {c.hasCv ? <FileText className="size-3.5" aria-label="CV attached" /> : null}
                              </span>
                            </div>
                          </div>
                        </div>
                      ))
                    )}
                  </div>
                </div>
              );
            })}
          </div>
        </div>
      ) : rows.length === 0 && activeCount > 0 ? (
        <div className="panel">
          <NoResults noun="candidates" icon={UserSearch} onClear={clear} />
        </div>
      ) : rows.length === 0 ? (
        <div className="panel">
          <EmptyState
            icon={UserSearch}
            title={view === "closed" ? "No closed candidates" : "No candidates yet"}
            description={view === "closed" ? "Rejected and withdrawn candidates appear here." : "Add the first applicant for a vacancy."}
          />
        </div>
      ) : (
        <Panel flush>
          <DataTable>
            <THead
              columns={[
                { label: "Candidate" },
                { label: "Vacancy" },
                { label: "Stage" },
                ...(view === "closed" ? [{ label: "Reason" }] : [{ label: "Rating" }, { label: "Phone" }]),
                { label: "", className: "w-10" },
              ]}
            />
            <tbody>
              {rows.map((c) => (
                <Tr key={c.id} onClick={() => navigate(`/admin/hiring/candidates/${c.id}`)}>
                  <Td>
                    <div className="flex items-center gap-3">
                      <Avatar name={c.fullName} size="sm" />
                      <div className="min-w-0">
                        <div className="truncate font-medium">{c.fullName}</div>
                        <div className="truncate text-xs text-[var(--muted-foreground)]">{c.qualification ?? "—"}</div>
                      </div>
                    </div>
                  </Td>
                  <Td>{c.vacancyTitle}</Td>
                  <Td>
                    <StagePill stage={c.stage} />
                  </Td>
                  {view === "closed" ? (
                    <Td>
                      <ClosedReason id={c.id} />
                    </Td>
                  ) : (
                    <>
                      <Td>{c.latestRating ? <Stars value={c.latestRating} /> : <span className="text-[var(--muted-foreground)]">—</span>}</Td>
                      <Td className="tabular">{c.phone}</Td>
                    </>
                  )}
                  <Td>
                    <ActionMenu items={menuFor(c)} label="Move to…" />
                  </Td>
                </Tr>
              ))}
            </tbody>
          </DataTable>
        </Panel>
      )}

      {hasMore ? (
        <div className="mt-3 flex flex-col items-center gap-1.5 text-xs text-[var(--muted-foreground)]">
          <span>
            Showing <span className="tabular">{mainLoaded}</span> of <span className="tabular">{result?.total}</span>
          </span>
          <Btn type="button" variant="outline" size="sm" disabled={loadingMore} onClick={() => void loadMore()}>
            {loadingMore ? "Loading…" : "Load more"}
          </Btn>
        </div>
      ) : null}

      {adding ? (
        <AddCandidateDialog
          vacancies={vacancies.filter((v) => v.status === "Open")}
          defaultVacancyId={vacancyId}
          onClose={() => setAdding(false)}
          onSaved={(p) => {
            setAdding(false);
            if (p.candidate.existingStaffWarning) toast.warning(p.candidate.existingStaffWarning);
            void load();
          }}
        />
      ) : null}

      {reasonFor ? (
        <ReasonDialog
          title={
            reasonFor.stage === "Rejected" ? `Reject ${reasonFor.c.fullName}?` : reasonFor.stage === "Withdrawn" ? `Mark ${reasonFor.c.fullName} withdrawn?` : `Reopen ${reasonFor.c.fullName}?`
          }
          confirmLabel={reasonFor.stage === "Rejected" ? "Reject" : reasonFor.stage === "Withdrawn" ? "Mark withdrawn" : "Reopen"}
          danger={reasonFor.stage === "Rejected"}
          onClose={() => setReasonFor(null)}
          onConfirm={async (reason) => {
            await move(reasonFor.c, reasonFor.stage, reason);
            setReasonFor(null);
          }}
        />
      ) : null}
    </div>
  );
}

/** The closing reason lives on the profile; fetched lazily for the Closed tab. */
function ClosedReason({ id }: { id: number }) {
  const [reason, setReason] = useState<string | null | undefined>(undefined);
  useEffect(() => {
    let cancelled = false;
    unwrap(api.get(`/candidates/${id}`))
      .then((p) => !cancelled && setReason((p as CandidateProfile).candidate.stageReason ?? null))
      .catch(() => !cancelled && setReason(null));
    return () => {
      cancelled = true;
    };
  }, [id]);
  if (reason === undefined) return <Skeleton className="h-4 w-24" />;
  return <span className="line-clamp-2 text-sm text-[var(--muted-foreground)]">{reason ?? "—"}</span>;
}

function AddCandidateDialog({
  vacancies,
  defaultVacancyId,
  onClose,
  onSaved,
}: {
  vacancies: Vacancy[];
  defaultVacancyId: string;
  onClose: () => void;
  onSaved: (p: CandidateProfile) => void;
}) {
  const initialVacancy = vacancies.some((v) => String(v.id) === defaultVacancyId) ? Number(defaultVacancyId) : vacancies[0]?.id ?? 0;
  const [form, setForm] = useState<SaveCandidateRequest>({
    vacancyId: initialVacancy,
    fullName: "",
    cnic: null,
    phone: "",
    email: null,
    qualification: null,
    experienceYears: null,
    currentSalary: null,
    expectedSalary: null,
    source: "WalkIn",
  });
  const [cv, setCv] = useState<File | null>(null);
  const [busy, setBusy] = useState(false);
  const set = <K extends keyof SaveCandidateRequest>(key: K, value: SaveCandidateRequest[K]) => setForm((f) => ({ ...f, [key]: value }));
  const num = (v: string) => (v.trim() === "" ? null : Number(v));
  const cnicDigits = (form.cnic ?? "").replace(/\D/g, "");

  const pickCv = (file: File | undefined) => {
    if (!file) return;
    const ext = file.name.slice(file.name.lastIndexOf(".")).toLowerCase();
    if (!CV_TYPES.includes(ext)) return toast.error("CV must be PDF, DOC, DOCX, JPG or PNG.");
    if (file.size > CV_LIMIT) return toast.error("CV must be 10 MB or smaller.");
    setCv(file);
  };

  const save = async () => {
    setBusy(true);
    try {
      const profile = (await unwrap(api.post("/candidates", { ...form, cnic: cnicDigits || null }))) as CandidateProfile;
      if (cv) {
        const data = new FormData();
        data.append("file", cv);
        try {
          await unwrap(api.post(`/candidates/${profile.candidate.id}/cv`, data));
        } catch (err) {
          toast.error(errorText(err, "Candidate saved, but the CV upload failed"));
        }
      }
      toast.success(`${profile.candidate.fullName} added`);
      onSaved(profile);
    } catch (err) {
      toast.error(errorText(err, "Could not add the candidate"));
    } finally {
      setBusy(false);
    }
  };

  const valid = form.vacancyId > 0 && form.fullName.trim() && form.phone.trim() && (cnicDigits.length === 0 || cnicDigits.length === 13);

  return (
    <Modal
      open
      wide
      title="Add candidate"
      description="Record an applicant and attach their CV."
      onClose={onClose}
      footer={
        <>
          <Btn type="button" variant="outline" onClick={onClose}>
            Cancel
          </Btn>
          <Btn type="button" disabled={busy || !valid} onClick={() => void save()}>
            {busy ? "Saving…" : "Add candidate"}
          </Btn>
        </>
      }
    >
      {vacancies.length === 0 ? (
        <p className="text-sm text-[var(--muted-foreground)]">There is no open vacancy. Post or reopen one first.</p>
      ) : (
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Vacancy" required className="sm:col-span-2">
            <Select value={form.vacancyId} onChange={(e) => set("vacancyId", Number(e.target.value))}>
              {vacancies.map((v) => (
                <option key={v.id} value={v.id}>
                  {v.title}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Full name" required>
            <TextInput value={form.fullName} maxLength={100} onChange={(e) => set("fullName", e.target.value)} autoFocus />
          </Field>
          <Field label="CNIC" hint={cnicDigits.length > 0 && cnicDigits.length !== 13 ? "13 digits" : undefined}>
            <TextInput value={form.cnic ?? ""} maxLength={20} inputMode="numeric" placeholder="35202-1234567-1" onChange={(e) => set("cnic", e.target.value || null)} />
          </Field>
          <Field label="Phone" required>
            <TextInput value={form.phone} maxLength={50} inputMode="tel" onChange={(e) => set("phone", e.target.value)} />
          </Field>
          <Field label="Email">
            <TextInput type="email" value={form.email ?? ""} maxLength={256} onChange={(e) => set("email", e.target.value || null)} />
          </Field>
          <Field label="Qualification">
            <TextInput value={form.qualification ?? ""} maxLength={200} placeholder="e.g. M.Sc Mathematics, B.Ed" onChange={(e) => set("qualification", e.target.value || null)} />
          </Field>
          <Field label="Experience (years)">
            <TextInput type="number" min={0} max={99} step={0.5} value={form.experienceYears ?? ""} onChange={(e) => set("experienceYears", num(e.target.value))} />
          </Field>
          <Field label="Current salary">
            <TextInput type="number" min={0} value={form.currentSalary ?? ""} onChange={(e) => set("currentSalary", num(e.target.value))} />
          </Field>
          <Field label="Expected salary">
            <TextInput type="number" min={0} value={form.expectedSalary ?? ""} onChange={(e) => set("expectedSalary", num(e.target.value))} />
          </Field>
          <Field label="Source">
            <Select value={form.source} onChange={(e) => set("source", e.target.value as CandidateSource)}>
              {(Object.keys(sourceLabel) as CandidateSource[]).map((s) => (
                <option key={s} value={s}>
                  {sourceLabel[s]}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="CV" hint={cv ? `${cv.name}` : "PDF, DOC, DOCX, JPG or PNG · up to 10 MB"}>
            <TextInput type="file" accept={CV_TYPES.join(",")} className="py-1.5" onChange={(e) => pickCv(e.target.files?.[0])} />
          </Field>
        </div>
      )}
    </Modal>
  );
}
