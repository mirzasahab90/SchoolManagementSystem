import { useCallback, useEffect, useState, type ReactNode } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { AlertTriangle, ArrowLeft, CalendarPlus, Download, Pencil, Trash2, Upload, UserCheck, UserSearch } from "lucide-react";
import { toast } from "sonner";
import { Modal } from "@/components/modal";
import { Avatar, Btn, EmptyState, Field, IconButton, Panel, Select, Skeleton, StatusPill, TextArea, TextInput } from "@/components/sms-ui";
import { useAuth } from "@/lib/auth";
import { api, downloadBlob, saveBlob, unwrap } from "@/lib/api";
import { cn } from "@/lib/utils";
import {
  candidateStageLabel,
  designationLabel,
  employmentTypeLabel,
  sourceLabel,
  type CandidateProfile,
  type CandidateStage,
  type EmploymentType,
  type InterviewResult,
  type InterviewRound,
  type StaffDesignation,
  type StaffDetail,
  type UserPick,
  type Vacancy,
} from "@/lib/types";
import {
  ActionMenu,
  ReasonDialog,
  StagePill,
  Stars,
  RatingInput,
  errorText,
  formatDate,
  formatDateTime,
  formatMoney,
  offerTone,
  resultTone,
  todayIso,
  type MenuItemDef,
} from "./shared";

type Tab = "overview" | "interviews" | "offer" | "activity";
type Dialog =
  | { kind: "schedule" }
  | { kind: "result"; round: InterviewRound }
  | { kind: "offer" }
  | { kind: "hire" }
  | { kind: "reason"; stage: CandidateStage }
  | null;

const CV_TYPES = ".pdf,.doc,.docx,.jpg,.jpeg,.png";

export function CandidateProfilePage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const { can } = useAuth();
  const [profile, setProfile] = useState<CandidateProfile | null>(null);
  const [missing, setMissing] = useState(false);
  const [tab, setTab] = useState<Tab>("overview");
  const [dialog, setDialog] = useState<Dialog>(null);
  const [busy, setBusy] = useState(false);

  const load = useCallback(async () => {
    try {
      setProfile((await unwrap(api.get(`/candidates/${id}`))) as CandidateProfile);
    } catch {
      setMissing(true);
    }
  }, [id]);

  useEffect(() => {
    void load();
  }, [load]);

  if (missing) {
    return (
      <div className="panel">
        <EmptyState
          icon={UserSearch}
          title="Candidate not found"
          description="It may have been deleted or belong to another campus."
          action={
            <Btn type="button" variant="outline" onClick={() => navigate("/admin/hiring/candidates")}>
              <ArrowLeft /> Candidates
            </Btn>
          }
        />
      </div>
    );
  }
  if (!profile) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-28 rounded-xl" />
        <Skeleton className="h-64 rounded-xl" />
      </div>
    );
  }

  const c = profile.candidate;
  const canEdit = can("candidates", "Edit");
  const canHire = canEdit && can("staff", "Add");
  const offer = profile.offer;
  const hasPass = profile.interviews.some((r) => r.result === "Pass");

  const run = async (fn: () => Promise<unknown>, success: string) => {
    setBusy(true);
    try {
      const next = await fn();
      if (next && typeof next === "object" && "candidate" in next) setProfile(next as CandidateProfile);
      else await load();
      toast.success(success);
    } catch (err) {
      toast.error(errorText(err, "Something went wrong"));
    } finally {
      setBusy(false);
    }
  };

  const move = (stage: CandidateStage, reason?: string) =>
    run(() => unwrap(api.post(`/candidates/${c.id}/move`, { stage, reason: reason ?? null })), `Moved to ${candidateStageLabel[stage]}`);

  const respond = (action: "accept" | "decline" | "withdraw") =>
    run(() => unwrap(api.post(`/candidates/${c.id}/offer/${action}`)), action === "accept" ? "Offer accepted" : action === "decline" ? "Offer declined" : "Offer withdrawn");

  const downloadCv = async () => {
    try {
      const { blob, fileName } = await downloadBlob(`/candidates/${c.id}/cv`);
      saveBlob(blob, fileName ?? c.cvFileName ?? "cv");
    } catch (err) {
      toast.error(errorText(err, "Could not download the CV"));
    }
  };

  const uploadCv = async (file: File) => {
    const data = new FormData();
    data.append("file", file);
    await run(() => unwrap(api.post(`/candidates/${c.id}/cv`, data)), "CV uploaded");
  };

  // Primary action for the stage.
  let primary: ReactNode = null;
  if (canEdit) {
    if (c.stage === "Applied") {
      primary = (
        <Btn type="button" disabled={busy} onClick={() => void move("Shortlisted")}>
          <UserCheck /> Shortlist
        </Btn>
      );
    } else if (c.stage === "Shortlisted") {
      primary = (
        <Btn type="button" onClick={() => setDialog({ kind: "schedule" })}>
          <CalendarPlus /> Schedule interview
        </Btn>
      );
    } else if (c.stage === "Interview") {
      primary = (
        <span title={hasPass ? undefined : "Record a passed interview first."}>
          <Btn type="button" disabled={!hasPass} onClick={() => setDialog({ kind: "offer" })}>
            Make offer
          </Btn>
        </span>
      );
    } else if (c.stage === "Offered" && offer?.status === "Pending") {
      primary = (
        <>
          <Btn type="button" variant="outline" disabled={busy} onClick={() => void respond("decline")}>
            Decline
          </Btn>
          <Btn type="button" disabled={busy} onClick={() => void respond("accept")}>
            Accept
          </Btn>
        </>
      );
    } else if (c.stage === "Offered" && offer?.status === "Accepted" && canHire) {
      primary = (
        <Btn type="button" onClick={() => setDialog({ kind: "hire" })}>
          <UserCheck /> Hire
        </Btn>
      );
    } else if (c.stage === "Offered") {
      primary = (
        <Btn type="button" onClick={() => setDialog({ kind: "offer" })}>
          New offer
        </Btn>
      );
    }
  }

  const secondary: MenuItemDef[] = canEdit
    ? [
        ...profile.allowedMoves
          .filter((s) => !(c.stage === "Applied" && s === "Shortlisted"))
          .filter((s) => !(c.stage === "Shortlisted" && s === "Interview"))
          .map((s) => ({
            label: s === "Rejected" ? "Reject…" : s === "Withdrawn" ? "Mark withdrawn…" : s === "Applied" ? "Reopen…" : `Move to ${candidateStageLabel[s]}`,
            danger: s === "Rejected",
            onSelect: () => (s === "Rejected" || s === "Withdrawn" || s === "Applied" ? setDialog({ kind: "reason", stage: s }) : void move(s)),
          })),
        ...(c.stage === "Offered" && offer?.status === "Pending" ? [{ label: "Withdraw offer", onSelect: () => void respond("withdraw") }] : []),
      ]
    : [];

  return (
    <div className="space-y-5">
      <Link
        to={`/admin/hiring/candidates?vacancyId=${c.vacancyId}`}
        className="inline-flex items-center gap-1 text-xs font-medium text-[var(--muted-foreground)] hover:text-[var(--foreground)]"
      >
        <ArrowLeft className="size-3.5" /> Candidates
      </Link>

      <div className="panel flex flex-wrap items-center gap-5 p-6">
        <Avatar name={c.fullName} size="lg" />
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-2">
            <h1 className="text-xl font-semibold">{c.fullName}</h1>
            <StagePill stage={c.stage} />
          </div>
          <div className="mt-1 flex flex-wrap items-center gap-2 text-sm text-[var(--muted-foreground)]">
            <span>{c.vacancyTitle}</span>
            <span className="font-mono text-xs">{c.campusCode}</span>
            {c.stageReason && (c.stage === "Rejected" || c.stage === "Withdrawn") ? <span>· {c.stageReason}</span> : null}
          </div>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          {c.hasCv ? (
            <Btn type="button" variant="outline" onClick={() => void downloadCv()}>
              <Download /> CV
            </Btn>
          ) : null}
          {c.stage === "Hired" && c.staffId ? (
            <Btn type="button" variant="outline" onClick={() => navigate(`/admin/staff/${c.staffId}`)}>
              View staff
            </Btn>
          ) : null}
          {primary}
          <ActionMenu items={secondary} />
        </div>
      </div>

      {c.existingStaffWarning ? (
        <div className="flex items-start gap-2 rounded-xl border border-[var(--warning)]/40 bg-[var(--warning-soft)] px-4 py-3 text-sm text-[var(--warning)]">
          <AlertTriangle className="mt-0.5 size-4 shrink-0" />
          <span>{c.existingStaffWarning}. Hiring this CNIC again is not allowed while that staff member is active.</span>
        </div>
      ) : null}

      <div className="flex gap-1 overflow-x-auto border-b border-[var(--border)]">
        {(
          [
            ["overview", "Overview"],
            ["interviews", `Interviews (${profile.interviews.length})`],
            ["offer", "Offer"],
            ["activity", "Activity"],
          ] as [Tab, string][]
        ).map(([key, label]) => (
          <button
            key={key}
            type="button"
            onClick={() => setTab(key)}
            className={cn(
              "-mb-px shrink-0 border-b-2 px-3 py-2 text-sm font-medium transition-colors",
              tab === key ? "border-[var(--primary)] text-[var(--foreground)]" : "border-transparent text-[var(--muted-foreground)] hover:text-[var(--foreground)]",
            )}
          >
            {label}
          </button>
        ))}
      </div>

      {tab === "overview" ? (
        <Panel
          title="Details"
          action={
            canEdit ? (
              <label className="inline-flex cursor-pointer items-center gap-1.5 text-xs font-medium text-[var(--primary)] hover:underline">
                <Upload className="size-3.5" /> {c.hasCv ? "Replace CV" : "Upload CV"}
                <input
                  type="file"
                  accept={CV_TYPES}
                  className="hidden"
                  onChange={(e) => {
                    const f = e.target.files?.[0];
                    e.target.value = "";
                    if (f) void uploadCv(f);
                  }}
                />
              </label>
            ) : null
          }
        >
          <dl className="grid gap-x-6 gap-y-4 sm:grid-cols-2 lg:grid-cols-3">
            <Info label="Phone" value={c.phone} />
            <Info label="Email" value={c.email} />
            <Info label="CNIC" value={c.cnic ? <span className="font-mono">{c.cnic}</span> : null} />
            <Info label="Qualification" value={c.qualification} />
            <Info label="Experience" value={c.experienceYears != null ? `${c.experienceYears} years` : null} />
            <Info label="Source" value={sourceLabel[c.source]} />
            <Info label="Current salary" value={c.currentSalary != null ? formatMoney(c.currentSalary) : null} />
            <Info label="Expected salary" value={c.expectedSalary != null ? formatMoney(c.expectedSalary) : null} />
            <Info label="CV" value={c.hasCv ? c.cvFileName : null} />
          </dl>
        </Panel>
      ) : null}

      {tab === "interviews" ? (
        <Panel
          title="Interview rounds"
          action={
            canEdit && (c.stage === "Shortlisted" || c.stage === "Interview") ? (
              <Btn type="button" size="sm" variant="outline" onClick={() => setDialog({ kind: "schedule" })}>
                <CalendarPlus /> Schedule round
              </Btn>
            ) : null
          }
        >
          {profile.interviews.length === 0 ? (
            <p className="text-sm text-[var(--muted-foreground)]">No rounds yet.</p>
          ) : (
            <ol className="relative space-y-4 border-l border-[var(--border)] pl-5">
              {profile.interviews.map((r) => (
                <li key={r.id} className="relative">
                  <span className="absolute top-1.5 -left-[25px] size-2.5 rounded-full border-2 border-[var(--card)] bg-[var(--primary)]" />
                  <div className="flex flex-wrap items-start justify-between gap-2">
                    <div className="min-w-0">
                      <div className="flex flex-wrap items-center gap-2">
                        <span className="font-medium">{r.name}</span>
                        <StatusPill tone={resultTone[r.result]}>{r.result}</StatusPill>
                        <Stars value={r.rating} />
                      </div>
                      <div className="mt-0.5 text-xs text-[var(--muted-foreground)]">
                        {formatDateTime(r.scheduledAt)} · {r.interviewerName}
                      </div>
                      {r.notes ? <p className="mt-1.5 text-sm whitespace-pre-line">{r.notes}</p> : null}
                    </div>
                    {canEdit && (c.stage === "Interview" || c.stage === "Offered") ? (
                      <div className="flex gap-1">
                        <Btn type="button" size="sm" variant="ghost" onClick={() => setDialog({ kind: "result", round: r })}>
                          <Pencil /> {r.result === "Scheduled" ? "Record result" : "Edit"}
                        </Btn>
                        {r.result === "Scheduled" ? (
                          <IconButton
                            label="Delete round"
                            tone="danger"
                            disabled={busy}
                            onClick={() => void run(() => unwrap(api.delete(`/candidates/${c.id}/interviews/${r.id}`)), "Round deleted")}
                          >
                            <Trash2 />
                          </IconButton>
                        ) : null}
                      </div>
                    ) : null}
                  </div>
                </li>
              ))}
            </ol>
          )}
        </Panel>
      ) : null}

      {tab === "offer" ? (
        <Panel
          title="Offer"
          action={
            canEdit && (c.stage === "Interview" || (c.stage === "Offered" && offer?.status !== "Accepted")) ? (
              <span title={hasPass ? undefined : "Record a passed interview first."}>
                <Btn type="button" size="sm" variant="outline" disabled={!hasPass} onClick={() => setDialog({ kind: "offer" })}>
                  {offer ? "New offer" : "Make offer"}
                </Btn>
              </span>
            ) : null
          }
        >
          {offer ? (
            <div className="space-y-4">
              <div className="flex flex-wrap items-center gap-2">
                <StatusPill tone={offerTone[offer.status]} dot>
                  {offer.status}
                </StatusPill>
                {offer.respondedOn ? <span className="text-xs text-[var(--muted-foreground)]">on {formatDate(offer.respondedOn)}</span> : null}
              </div>
              <dl className="grid gap-x-6 gap-y-4 sm:grid-cols-2 lg:grid-cols-4">
                <Info label="Designation" value={designationLabel[offer.designation]} />
                <Info label="Employment" value={employmentTypeLabel[offer.employmentType]} />
                <Info label="Salary" value={formatMoney(offer.offeredSalary)} />
                <Info label="Proposed joining" value={formatDate(offer.proposedJoiningDate)} />
              </dl>
            </div>
          ) : (
            <p className="text-sm text-[var(--muted-foreground)]">
              {hasPass ? "No offer yet." : "Record a passed interview first, then make an offer."}
            </p>
          )}
        </Panel>
      ) : null}

      {tab === "activity" ? (
        <Panel title="Stage history">
          <ol className="relative space-y-4 border-l border-[var(--border)] pl-5">
            {[...profile.history].reverse().map((h, i) => (
              <li key={i} className="relative">
                <span className="absolute top-1.5 -left-[25px] size-2.5 rounded-full border-2 border-[var(--card)] bg-[var(--border-strong)]" />
                <div className="flex flex-wrap items-center gap-2 text-sm">
                  {h.fromStage ? (
                    <>
                      <StagePill stage={h.fromStage} /> <span className="text-[var(--muted-foreground)]">→</span>
                    </>
                  ) : null}
                  <StagePill stage={h.toStage} />
                </div>
                <div className="mt-0.5 text-xs text-[var(--muted-foreground)]">
                  {formatDateTime(h.occurredOn)}
                  {h.byUserName ? ` · ${h.byUserName}` : ""}
                </div>
                {h.reason ? <p className="mt-1 text-sm">{h.reason}</p> : null}
              </li>
            ))}
          </ol>
        </Panel>
      ) : null}

      {dialog?.kind === "schedule" ? (
        <RoundDialog candidateId={c.id} onClose={() => setDialog(null)} onSaved={(p) => (setProfile(p), setDialog(null))} />
      ) : null}
      {dialog?.kind === "result" ? (
        <RoundDialog candidateId={c.id} round={dialog.round} onClose={() => setDialog(null)} onSaved={(p) => (setProfile(p), setDialog(null))} />
      ) : null}
      {dialog?.kind === "offer" ? (
        <OfferDialog profile={profile} onClose={() => setDialog(null)} onSaved={(p) => (setProfile(p), setDialog(null), setTab("offer"))} />
      ) : null}
      {dialog?.kind === "hire" ? (
        <HireDialog
          profile={profile}
          onClose={() => setDialog(null)}
          onHired={(staff) => {
            setDialog(null);
            toast.success(`Hired · ${staff.employeeNo}`, { action: { label: "Staff list", onClick: () => navigate("/admin/staff") } });
            void load();
          }}
        />
      ) : null}
      {dialog?.kind === "reason" ? (
        <ReasonDialog
          title={dialog.stage === "Rejected" ? `Reject ${c.fullName}?` : dialog.stage === "Withdrawn" ? `Mark ${c.fullName} withdrawn?` : `Reopen ${c.fullName}?`}
          confirmLabel={dialog.stage === "Rejected" ? "Reject" : dialog.stage === "Withdrawn" ? "Mark withdrawn" : "Reopen"}
          danger={dialog.stage === "Rejected"}
          onClose={() => setDialog(null)}
          onConfirm={async (reason) => {
            await move(dialog.stage, reason);
            setDialog(null);
          }}
        />
      ) : null}
    </div>
  );
}

function Info({ label, value }: { label: string; value?: ReactNode }) {
  return (
    <div className="min-w-0">
      <dt className="text-xs text-[var(--muted-foreground)]">{label}</dt>
      <dd className="mt-0.5 truncate text-sm">{value ?? <span className="text-[var(--muted-foreground)]">—</span>}</dd>
    </div>
  );
}

const ROUND_PRESETS = ["Screening", "Demo class", "Principal", "Final"];

/** Local "YYYY-MM-DDTHH:mm" for a datetime-local input. */
function toLocalInput(iso?: string) {
  const d = iso ? new Date(iso) : new Date(Date.now() + 24 * 3600 * 1000);
  if (!iso) d.setHours(10, 0, 0, 0);
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

function RoundDialog({
  candidateId,
  round,
  onClose,
  onSaved,
}: {
  candidateId: number;
  round?: InterviewRound;
  onClose: () => void;
  onSaved: (p: CandidateProfile) => void;
}) {
  const recording = round != null;
  const [preset, setPreset] = useState(round ? (ROUND_PRESETS.includes(round.name) ? round.name : "Other") : "Screening");
  const [customName, setCustomName] = useState(round && !ROUND_PRESETS.includes(round.name) ? round.name : "");
  const [when, setWhen] = useState(toLocalInput(round?.scheduledAt));
  const [interviewer, setInterviewer] = useState<number>(round?.interviewerUserId ?? 0);
  const [result, setResult] = useState<InterviewResult>(round && round.result !== "Scheduled" ? round.result : recording ? "Pass" : "Scheduled");
  const [rating, setRating] = useState<number | null>(round?.rating ?? null);
  const [notes, setNotes] = useState(round?.notes ?? "");
  const [users, setUsers] = useState<UserPick[]>([]);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    unwrap(api.get(`/users?pick=true&candidateId=${candidateId}`))
      .then((u) => {
        const list = u as UserPick[];
        setUsers(list);
        if (!round && list.length > 0) setInterviewer((cur) => cur || list[0].id);
      })
      .catch(() => setUsers([]));
  }, [round, candidateId]);

  const name = preset === "Other" ? customName.trim() : preset;
  const needsRating = result !== "Scheduled";
  const valid = name.length > 0 && interviewer > 0 && when && (!needsRating || rating != null);

  const save = async () => {
    setBusy(true);
    try {
      const scheduledAt = new Date(when).toISOString();
      const p = recording
        ? await unwrap(
            api.put(`/candidates/${candidateId}/interviews/${round.id}`, {
              name,
              scheduledAt,
              interviewerUserId: interviewer,
              result,
              rating: needsRating ? rating : rating ?? null,
              notes: notes.trim() || null,
            }),
          )
        : await unwrap(api.post(`/candidates/${candidateId}/interviews`, { name, scheduledAt, interviewerUserId: interviewer }));
      toast.success(recording ? "Result saved" : "Round scheduled");
      onSaved(p as CandidateProfile);
    } catch (err) {
      toast.error(errorText(err, "Could not save the round"));
    } finally {
      setBusy(false);
    }
  };

  const interviewerMissing = round && !users.some((u) => u.id === round.interviewerUserId);

  return (
    <Modal
      open
      title={recording ? "Record result" : "Schedule round"}
      onClose={onClose}
      footer={
        <>
          <Btn type="button" variant="outline" onClick={onClose}>
            Cancel
          </Btn>
          <Btn type="button" disabled={busy || !valid} onClick={() => void save()}>
            {busy ? "Saving…" : recording ? "Save result" : "Schedule"}
          </Btn>
        </>
      }
    >
      <div className="space-y-4">
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Round" required>
            <Select value={preset} onChange={(e) => setPreset(e.target.value)}>
              {[...ROUND_PRESETS, "Other"].map((p) => (
                <option key={p} value={p}>
                  {p}
                </option>
              ))}
            </Select>
          </Field>
          {preset === "Other" ? (
            <Field label="Round name" required>
              <TextInput value={customName} maxLength={60} onChange={(e) => setCustomName(e.target.value)} />
            </Field>
          ) : null}
          <Field label="Date and time" required>
            <TextInput type="datetime-local" value={when} onChange={(e) => setWhen(e.target.value)} />
          </Field>
          <Field label="Interviewer" required>
            <Select value={interviewer} onChange={(e) => setInterviewer(Number(e.target.value))}>
              {users.length === 0 && !round ? <option value={0}>No users available</option> : null}
              {interviewerMissing ? <option value={round.interviewerUserId}>{round.interviewerName}</option> : null}
              {users.map((u) => (
                <option key={u.id} value={u.id}>
                  {u.fullName}
                </option>
              ))}
            </Select>
          </Field>
        </div>
        {recording ? (
          <>
            <Field label="Result" required>
              <Select value={result} onChange={(e) => setResult(e.target.value as InterviewResult)}>
                <option value="Pass">Pass</option>
                <option value="Fail">Fail</option>
                <option value="Hold">Hold</option>
                <option value="Scheduled">Still scheduled</option>
              </Select>
            </Field>
            <div>
              <div className="mb-1.5 text-[13px] font-medium">
                Rating{needsRating ? <span className="ml-0.5 text-[var(--danger)]">*</span> : null}
              </div>
              <RatingInput value={rating} onChange={setRating} />
            </div>
            <Field label="Notes">
              <TextArea rows={3} maxLength={2000} value={notes} onChange={(e) => setNotes(e.target.value)} />
            </Field>
          </>
        ) : null}
      </div>
    </Modal>
  );
}

function OfferDialog({ profile, onClose, onSaved }: { profile: CandidateProfile; onClose: () => void; onSaved: (p: CandidateProfile) => void }) {
  const c = profile.candidate;
  const [designation, setDesignation] = useState<StaffDesignation>(profile.offer?.designation ?? "Teacher");
  const [employmentType, setEmploymentType] = useState<EmploymentType>(profile.offer?.employmentType ?? "Permanent");
  const [salary, setSalary] = useState<string>(String(profile.offer?.offeredSalary ?? c.expectedSalary ?? ""));
  const [joining, setJoining] = useState(profile.offer?.proposedJoiningDate ?? todayIso());
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (profile.offer) return;
    unwrap(api.get(`/vacancies/${c.vacancyId}`))
      .then((v) => setDesignation((v as Vacancy).designation))
      .catch(() => undefined);
  }, [c.vacancyId, profile.offer]);

  const save = async () => {
    setBusy(true);
    try {
      const p = await unwrap(
        api.post(`/candidates/${c.id}/offer`, { designation, employmentType, offeredSalary: Number(salary), proposedJoiningDate: joining }),
      );
      toast.success("Offer recorded");
      onSaved(p as CandidateProfile);
    } catch (err) {
      toast.error(errorText(err, "Could not make the offer"));
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal
      open
      title={profile.offer ? "New offer" : "Make offer"}
      description={profile.offer?.status === "Pending" ? "The pending offer will be withdrawn." : undefined}
      onClose={onClose}
      footer={
        <>
          <Btn type="button" variant="outline" onClick={onClose}>
            Cancel
          </Btn>
          <Btn type="button" disabled={busy || !(Number(salary) > 0) || !joining} onClick={() => void save()}>
            {busy ? "Saving…" : "Record offer"}
          </Btn>
        </>
      }
    >
      <div className="grid gap-4 sm:grid-cols-2">
        <Field label="Designation" required>
          <Select value={designation} onChange={(e) => setDesignation(e.target.value as StaffDesignation)}>
            {(Object.keys(designationLabel) as StaffDesignation[]).map((d) => (
              <option key={d} value={d}>
                {designationLabel[d]}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Employment" required>
          <Select value={employmentType} onChange={(e) => setEmploymentType(e.target.value as EmploymentType)}>
            {(Object.keys(employmentTypeLabel) as EmploymentType[]).map((t) => (
              <option key={t} value={t}>
                {employmentTypeLabel[t]}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Monthly salary" required hint={c.expectedSalary ? `Expected ${formatMoney(c.expectedSalary)}` : undefined}>
          <TextInput type="number" min={1} value={salary} onChange={(e) => setSalary(e.target.value)} />
        </Field>
        <Field label="Proposed joining" required>
          <TextInput type="date" value={joining} onChange={(e) => setJoining(e.target.value)} />
        </Field>
      </div>
    </Modal>
  );
}

function HireDialog({ profile, onClose, onHired }: { profile: CandidateProfile; onClose: () => void; onHired: (s: StaffDetail) => void }) {
  const offer = profile.offer!;
  const [joining, setJoining] = useState(offer.proposedJoiningDate);
  const [employmentType, setEmploymentType] = useState<EmploymentType>(offer.employmentType);
  const [salary, setSalary] = useState(String(offer.offeredSalary));
  const [busy, setBusy] = useState(false);

  const save = async () => {
    setBusy(true);
    try {
      const staff = (await unwrap(
        api.post(`/candidates/${profile.candidate.id}/hire`, { joiningDate: joining, employmentType, basicSalary: Number(salary) }),
      )) as StaffDetail;
      onHired(staff);
    } catch (err) {
      toast.error(errorText(err, "Could not hire"));
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal
      open
      title={`Hire ${profile.candidate.fullName}`}
      description="Creates a staff record with the next employee number. Onboarding details come later."
      onClose={onClose}
      footer={
        <>
          <Btn type="button" variant="outline" onClick={onClose}>
            Cancel
          </Btn>
          <Btn type="button" disabled={busy || !(Number(salary) > 0) || !joining} onClick={() => void save()}>
            {busy ? "Hiring…" : "Hire"}
          </Btn>
        </>
      }
    >
      <div className="grid gap-4 sm:grid-cols-2">
        <Field label="Joining date" required>
          <TextInput type="date" value={joining} onChange={(e) => setJoining(e.target.value)} />
        </Field>
        <Field label="Employment" required>
          <Select value={employmentType} onChange={(e) => setEmploymentType(e.target.value as EmploymentType)}>
            {(Object.keys(employmentTypeLabel) as EmploymentType[]).map((t) => (
              <option key={t} value={t}>
                {employmentTypeLabel[t]}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Basic salary" required className="sm:col-span-2">
          <TextInput type="number" min={1} value={salary} onChange={(e) => setSalary(e.target.value)} />
        </Field>
      </div>
    </Modal>
  );
}
