import { useCallback, useEffect, useRef, useState, type ReactNode } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import {
  ArrowLeft,
  ArrowRightLeft,
  Download,
  FileText,
  GraduationCap,
  LogOut,
  Pencil,
  Phone,
  Plus,
  RotateCcw,
  Trash2,
  Upload,
  UserPlus,
} from "lucide-react";
import { toast } from "sonner";
import { ConfirmDialog, Modal } from "@/components/modal";
import { StudentPhoto } from "@/components/student-photo";
import { StudentAttendanceTab } from "./attendance-tab";
import { StudentResultsTab } from "./results-tab";
import {
  Btn,
  EmptyState,
  Field,
  IconButton,
  Panel,
  Select,
  Skeleton,
  StatusPill,
  Switch,
  TextArea,
  TextInput,
} from "@/components/sms-ui";
import { useAuth } from "@/lib/auth";
import { api, downloadBlob, saveBlob, unwrap } from "@/lib/api";
import { cn } from "@/lib/utils";
import {
  documentTypeLabel,
  genderLabel,
  relationLabel,
  type DocumentType,
  type EnrollmentAction,
  type Gender,
  type GuardianLookup,
  type GuardianRelation,
  type AcademicSessionList,
  type SaveStudentRequest,
  type Section,
  type StudentProfile,
} from "@/lib/types";

type Tab = "overview" | "guardians" | "documents" | "attendance" | "results" | "history";

const formatDate = (v?: string | null) =>
  v ? new Date(v.length === 10 ? `${v}T00:00:00` : v).toLocaleDateString(undefined, { day: "numeric", month: "short", year: "numeric" }) : "—";
const formatSize = (n: number) => (n < 1024 * 1024 ? `${Math.max(1, Math.round(n / 1024))} KB` : `${(n / 1024 / 1024).toFixed(1)} MB`);
const digits = (v: string) => v.replace(/\D/g, "");

const actionText: Record<EnrollmentAction, string> = {
  Admitted: "Admitted",
  SectionChanged: "Section changed",
  Left: "Left school",
  Rejoined: "Rejoined",
};

export function StudentProfilePage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const { can } = useAuth();
  const [profile, setProfile] = useState<StudentProfile | null>(null);
  const [missing, setMissing] = useState(false);
  const [tab, setTab] = useState<Tab>("overview");
  const [photoVersion, setPhotoVersion] = useState(0);
  const [dialog, setDialog] = useState<null | "edit" | "section" | "leave" | "rejoin" | "guardian">(null);
  const photoInput = useRef<HTMLInputElement>(null);

  const canEdit = can("students", "Edit");
  const canSeeAttendance = can("students", "View") || can("attendance-reports", "View");
  const canSeeResults = can("students", "View") || can("results", "View");

  const load = useCallback(async () => {
    try {
      setProfile((await unwrap(api.get(`/students/${id}`))) as StudentProfile);
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
        <EmptyState icon={GraduationCap} title="Student not found" description="They may be on another campus or have been removed." />
      </div>
    );
  }

  if (!profile) {
    return (
      <div className="space-y-4">
        <div className="panel flex items-center gap-4 p-6">
          <Skeleton className="size-20 rounded-2xl" />
          <div className="flex-1 space-y-2">
            <Skeleton className="h-5 w-48" />
            <Skeleton className="h-3 w-32" />
          </div>
        </div>
        <Skeleton className="h-64 w-full rounded-xl" />
      </div>
    );
  }

  const { student, enrollment } = profile;
  const isLeft = student.status === "Left";
  const set = (p: StudentProfile) => setProfile(p);

  async function uploadPhoto(file: File) {
    if (file.size > 2_097_152 || !/^image\/(jpeg|png|webp)$/.test(file.type)) {
      toast.error("Only JPG, PNG or WEBP images up to 2 MB.");
      return;
    }
    const form = new FormData();
    form.append("file", file);
    try {
      await unwrap(api.post(`/students/${student.id}/photo`, form));
      toast.success("Photo updated");
      setPhotoVersion((v) => v + 1);
      await load();
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Upload failed");
    }
  }

  return (
    <div className="space-y-5">
      <button
        type="button"
        onClick={() => navigate("/admin/students")}
        className="inline-flex items-center gap-1 text-xs font-medium text-[var(--muted-foreground)] hover:text-[var(--foreground)]"
      >
        <ArrowLeft className="size-3.5" /> Students
      </button>

      {/* Header */}
      <div className="panel flex flex-wrap items-center gap-5 p-6">
        <button
          type="button"
          disabled={!canEdit}
          onClick={() => photoInput.current?.click()}
          title={canEdit ? "Change photo" : undefined}
          className="group relative rounded-2xl outline-none focus-visible:ring-4 focus-visible:ring-[var(--ring)]"
        >
          <StudentPhoto studentId={student.id} name={student.fullName} hasPhoto={student.hasPhoto} size="xl" version={photoVersion} />
          {canEdit ? (
            <span className="absolute inset-0 hidden items-center justify-center rounded-2xl bg-black/40 text-white group-hover:flex">
              <Upload className="size-5" />
            </span>
          ) : null}
        </button>
        <input
          ref={photoInput}
          type="file"
          accept="image/jpeg,image/png,image/webp"
          className="hidden"
          onChange={(e) => {
            const f = e.target.files?.[0];
            e.target.value = "";
            if (f) void uploadPhoto(f);
          }}
        />
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-2">
            <h1 className="text-xl font-semibold">{student.fullName}</h1>
            <StatusPill tone={isLeft ? "neutral" : "success"} dot>
              {student.status}
            </StatusPill>
          </div>
          <div className="mt-1 flex flex-wrap items-center gap-2 text-sm text-[var(--muted-foreground)]">
            <span className="rounded-md border border-[var(--border)] px-1.5 py-0.5 font-mono text-xs">{student.admissionNo}</span>
            {enrollment ? (
              <StatusPill tone="primary">
                {enrollment.className} – {enrollment.sectionName} · Roll {enrollment.rollNo}
              </StatusPill>
            ) : null}
            <span>{enrollment?.sessionName}</span>
            <span className="font-mono text-xs">{student.campusCode}</span>
          </div>
          {isLeft ? (
            <p className="mt-2 text-sm text-[var(--muted-foreground)]">
              Left on {formatDate(student.leftOn)} — {student.leaveReason}
            </p>
          ) : null}
        </div>
        {canEdit ? (
          <div className="flex flex-wrap gap-2">
            <Btn type="button" variant="outline" onClick={() => setDialog("edit")}>
              <Pencil /> Edit
            </Btn>
            {isLeft ? (
              <Btn type="button" onClick={() => setDialog("rejoin")}>
                <RotateCcw /> Rejoin
              </Btn>
            ) : (
              <>
                <Btn type="button" variant="outline" onClick={() => setDialog("section")}>
                  <ArrowRightLeft /> Change section
                </Btn>
                <Btn type="button" variant="outline" className="text-[var(--danger)]" onClick={() => setDialog("leave")}>
                  <LogOut /> Leave school
                </Btn>
              </>
            )}
          </div>
        ) : null}
      </div>

      {/* Tabs */}
      <div className="flex gap-1 border-b border-[var(--border)]">
        {(
          [
            ["overview", "Overview"],
            ["guardians", `Guardians (${profile.guardians.length})`],
            ["documents", `Documents (${profile.documents.length})`],
            ...(canSeeAttendance ? ([["attendance", "Attendance"]] as [Tab, string][]) : []),
            ...(canSeeResults ? ([["results", "Results"]] as [Tab, string][]) : []),
            ["history", "History"],
          ] as [Tab, string][]
        ).map(([key, label]) => (
          <button
            key={key}
            type="button"
            onClick={() => setTab(key)}
            className={cn(
              "-mb-px border-b-2 px-3 py-2 text-sm font-medium transition-colors",
              tab === key ? "border-[var(--primary)] text-[var(--foreground)]" : "border-transparent text-[var(--muted-foreground)] hover:text-[var(--foreground)]",
            )}
          >
            {label}
          </button>
        ))}
      </div>

      {tab === "overview" ? <Overview profile={profile} /> : null}
      {tab === "guardians" ? <GuardiansTab profile={profile} canEdit={canEdit} onChange={set} onAdd={() => setDialog("guardian")} /> : null}
      {tab === "documents" ? <DocumentsTab profile={profile} canEdit={canEdit} onChange={load} /> : null}
      {tab === "attendance" ? <StudentAttendanceTab studentId={student.id} /> : null}
      {tab === "results" ? <StudentResultsTab studentId={student.id} /> : null}
      {tab === "history" ? <HistoryTab profile={profile} /> : null}

      {dialog === "edit" ? <EditDialog profile={profile} onClose={() => setDialog(null)} onSaved={set} /> : null}
      {dialog === "section" ? <SectionDialog profile={profile} mode="change" onClose={() => setDialog(null)} onSaved={set} /> : null}
      {dialog === "rejoin" ? <SectionDialog profile={profile} mode="rejoin" onClose={() => setDialog(null)} onSaved={set} /> : null}
      {dialog === "leave" ? <LeaveDialog profile={profile} onClose={() => setDialog(null)} onSaved={set} /> : null}
      {dialog === "guardian" ? <AddGuardianDialog profile={profile} onClose={() => setDialog(null)} onSaved={set} /> : null}
    </div>
  );
}

/* ------------------------------------------------------------------ */

function Info({ label, value }: { label: string; value?: ReactNode }) {
  return (
    <div>
      <dt className="text-xs text-[var(--muted-foreground)]">{label}</dt>
      <dd className={cn("mt-0.5 break-words", value ? "font-medium" : "text-[var(--muted-foreground)]")}>{value || "—"}</dd>
    </div>
  );
}

function Overview({ profile }: { profile: StudentProfile }) {
  const s = profile.student;
  return (
    <div className="grid gap-5 lg:grid-cols-2">
      <Panel title="Personal">
        <dl className="grid gap-x-6 gap-y-4 text-sm sm:grid-cols-2">
          <Info label="Gender" value={genderLabel[s.gender]} />
          <Info label="Date of birth" value={formatDate(s.dateOfBirth)} />
          <Info label="B-Form" value={s.bFormNo} />
          <Info label="Blood group" value={s.bloodGroup} />
          <Info label="Religion" value={s.religion} />
          <Info label="Nationality" value={s.nationality} />
        </dl>
      </Panel>
      <Panel title="Contact & admission">
        <dl className="grid gap-x-6 gap-y-4 text-sm sm:grid-cols-2">
          <Info label="Address" value={s.address} />
          <Info label="Phone" value={s.phone} />
          <Info label="Admission date" value={formatDate(s.admissionDate)} />
          <Info label="Previous school" value={s.previousSchool} />
          <div className="sm:col-span-2">
            <Info label="Notes" value={s.notes} />
          </div>
        </dl>
      </Panel>
    </div>
  );
}

function GuardiansTab({
  profile,
  canEdit,
  onChange,
  onAdd,
}: {
  profile: StudentProfile;
  canEdit: boolean;
  onChange: (p: StudentProfile) => void;
  onAdd: () => void;
}) {
  const [removing, setRemoving] = useState<number | null>(null);
  const [busy, setBusy] = useState(false);

  async function makePrimary(guardianId: number, isEmergencyContact: boolean) {
    try {
      onChange(
        (await unwrap(
          api.patch(`/students/${profile.student.id}/guardians/${guardianId}`, { isPrimary: true, isEmergencyContact }),
        )) as StudentProfile,
      );
      toast.success("Primary guardian changed");
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Could not change the primary guardian");
    }
  }

  async function remove() {
    if (removing == null) return;
    setBusy(true);
    try {
      onChange((await unwrap(api.delete(`/students/${profile.student.id}/guardians/${removing}`))) as StudentProfile);
      toast.success("Guardian removed");
      setRemoving(null);
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Could not remove guardian");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="space-y-5">
      <div className="grid gap-4 md:grid-cols-2">
        {profile.guardians.map((g) => (
          <div key={g.guardianId} className="panel p-5">
            <div className="flex items-start justify-between gap-3">
              <div>
                <Link to={`/admin/guardians/${g.guardianId}`} className="font-semibold hover:text-[var(--primary)]">
                  {g.fullName}
                </Link>
                <div className="mt-1 flex flex-wrap gap-1.5">
                  <StatusPill>{relationLabel[g.relation]}</StatusPill>
                  {g.isPrimary ? <StatusPill tone="primary">Primary</StatusPill> : null}
                  {g.isEmergencyContact ? <StatusPill tone="warning">Emergency</StatusPill> : null}
                </div>
              </div>
              {canEdit && !g.isPrimary ? (
                <div className="flex items-center gap-1">
                  <Btn type="button" size="sm" variant="outline" onClick={() => makePrimary(g.guardianId, g.isEmergencyContact)}>
                    Make primary
                  </Btn>
                  <IconButton label={`Remove ${g.fullName}`} tone="danger" onClick={() => setRemoving(g.guardianId)}>
                    <Trash2 />
                  </IconButton>
                </div>
              ) : null}
            </div>
            <dl className="mt-4 grid gap-3 text-sm sm:grid-cols-2">
              <Info
                label="Phone"
                value={
                  <a href={`tel:${g.phone}`} className="inline-flex items-center gap-1.5 hover:text-[var(--primary)]">
                    <Phone className="size-3.5" />
                    {g.phone}
                  </a>
                }
              />
              <Info label="CNIC" value={g.cnic} />
              <Info label="Email" value={g.email} />
              <Info label="Occupation" value={g.occupation} />
            </dl>
          </div>
        ))}
      </div>
      {canEdit ? (
        <Btn type="button" variant="outline" onClick={onAdd}>
          <UserPlus /> Add guardian
        </Btn>
      ) : null}

      <Panel title="Siblings" description="Students who share a guardian">
        {profile.siblings.length === 0 ? (
          <p className="text-sm text-[var(--muted-foreground)]">No siblings in the school.</p>
        ) : (
          <ul className="divide-y divide-[var(--border)] text-sm">
            {profile.siblings.map((s, i) => (
              <li key={`${s.studentId ?? "x"}-${i}`} className="flex items-center justify-between gap-3 py-2.5">
                {s.studentId ? (
                  <Link to={`/admin/students/${s.studentId}`} className="font-medium hover:text-[var(--primary)]">
                    {s.fullName}
                  </Link>
                ) : (
                  <span className="font-medium">{s.fullName}</span>
                )}
                <span className="flex items-center gap-2 text-xs text-[var(--muted-foreground)]">
                  {s.className ? `${s.className} – ${s.sectionName}` : null}
                  <span className="rounded border border-[var(--border)] px-1 font-mono">{s.campusCode}</span>
                </span>
              </li>
            ))}
          </ul>
        )}
      </Panel>

      <ConfirmDialog
        open={removing != null}
        title="Remove guardian?"
        description="They stay in the school's records but are no longer linked to this student."
        confirmLabel="Remove"
        busy={busy}
        onConfirm={remove}
        onClose={() => setRemoving(null)}
      />
    </div>
  );
}

function DocumentsTab({ profile, canEdit, onChange }: { profile: StudentProfile; canEdit: boolean; onChange: () => Promise<void> }) {
  const [type, setType] = useState<DocumentType>("BForm");
  const [dragging, setDragging] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [deleting, setDeleting] = useState<number | null>(null);
  const input = useRef<HTMLInputElement>(null);

  async function upload(file: File) {
    if (file.size > 10_485_760 || !/^(image\/(jpeg|png|webp)|application\/pdf)$/.test(file.type)) {
      toast.error("Only JPG, PNG, WEBP or PDF files up to 10 MB.");
      return;
    }
    const form = new FormData();
    form.append("type", type);
    form.append("file", file);
    setUploading(true);
    try {
      await unwrap(api.post(`/students/${profile.student.id}/documents`, form));
      toast.success("Document uploaded");
      await onChange();
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Upload failed");
    } finally {
      setUploading(false);
    }
  }

  async function download(docId: number, name: string) {
    try {
      const { blob, fileName } = await downloadBlob(`/students/${profile.student.id}/documents/${docId}`);
      saveBlob(blob, fileName ?? name);
    } catch {
      toast.error("Download failed");
    }
  }

  async function remove() {
    if (deleting == null) return;
    try {
      await unwrap(api.delete(`/students/${profile.student.id}/documents/${deleting}`));
      toast.success("Document deleted");
      setDeleting(null);
      await onChange();
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Delete failed");
    }
  }

  return (
    <div className="space-y-5">
      {canEdit ? (
        <div
          onDragOver={(e) => {
            e.preventDefault();
            setDragging(true);
          }}
          onDragLeave={() => setDragging(false)}
          onDrop={(e) => {
            e.preventDefault();
            setDragging(false);
            const f = e.dataTransfer.files?.[0];
            if (f) void upload(f);
          }}
          className={cn(
            "flex flex-wrap items-center gap-4 rounded-xl border-2 border-dashed p-5 transition",
            dragging ? "border-[var(--primary)] bg-[var(--primary-soft)]" : "border-[var(--border-strong)]",
          )}
        >
          <span className="inline-flex size-10 items-center justify-center rounded-lg bg-[var(--muted)] text-[var(--muted-foreground)]">
            <Upload className="size-5" />
          </span>
          <div className="min-w-0 flex-1 text-sm">
            <div className="font-medium">Drop a file here, or choose one</div>
            <div className="text-xs text-[var(--muted-foreground)]">JPG, PNG, WEBP or PDF up to 10 MB</div>
          </div>
          <Select value={type} onChange={(e) => setType(e.target.value as DocumentType)} className="w-auto" aria-label="Document type">
            {(Object.keys(documentTypeLabel) as DocumentType[]).map((t) => (
              <option key={t} value={t}>
                {documentTypeLabel[t]}
              </option>
            ))}
          </Select>
          <Btn type="button" onClick={() => input.current?.click()} disabled={uploading}>
            {uploading ? "Uploading…" : "Choose file"}
          </Btn>
          <input
            ref={input}
            type="file"
            accept="image/jpeg,image/png,image/webp,application/pdf"
            className="hidden"
            onChange={(e) => {
              const f = e.target.files?.[0];
              e.target.value = "";
              if (f) void upload(f);
            }}
          />
        </div>
      ) : null}

      <Panel flush>
        {profile.documents.length === 0 ? (
          <EmptyState icon={FileText} title="No documents yet" description="Upload the B-Form, birth certificate or previous results." />
        ) : (
          <ul className="divide-y divide-[var(--border)]">
            {profile.documents.map((d) => (
              <li key={d.id} className="flex items-center gap-4 px-5 py-3 text-sm">
                <span className="inline-flex size-9 items-center justify-center rounded-lg bg-[var(--muted)] text-[var(--muted-foreground)]">
                  <FileText className="size-4" />
                </span>
                <div className="min-w-0 flex-1">
                  <div className="truncate font-medium">{d.fileName}</div>
                  <div className="text-xs text-[var(--muted-foreground)]">
                    {formatSize(d.sizeBytes)} · {formatDate(d.uploadedOn)}
                  </div>
                </div>
                <StatusPill tone="info">{documentTypeLabel[d.type]}</StatusPill>
                <IconButton label={`Download ${d.fileName}`} onClick={() => download(d.id, d.fileName)}>
                  <Download />
                </IconButton>
                {canEdit ? (
                  <IconButton label={`Delete ${d.fileName}`} tone="danger" onClick={() => setDeleting(d.id)}>
                    <Trash2 />
                  </IconButton>
                ) : null}
              </li>
            ))}
          </ul>
        )}
      </Panel>

      <ConfirmDialog
        open={deleting != null}
        title="Delete document?"
        description="The document is removed from this student's profile."
        onConfirm={remove}
        onClose={() => setDeleting(null)}
      />
    </div>
  );
}

function HistoryTab({ profile }: { profile: StudentProfile }) {
  const items = [...profile.history].reverse();
  const tone: Record<EnrollmentAction, string> = {
    Admitted: "bg-[var(--success)]",
    SectionChanged: "bg-[var(--info)]",
    Left: "bg-[var(--danger)]",
    Rejoined: "bg-[var(--primary)]",
  };
  return (
    <Panel>
      {items.length === 0 ? (
        <p className="text-sm text-[var(--muted-foreground)]">No history yet.</p>
      ) : (
        <ol className="relative space-y-5 border-l border-[var(--border)] pl-6">
          {items.map((h) => (
            <li key={h.id} className="relative">
              <span className={cn("absolute top-1.5 -left-[29px] size-2.5 rounded-full ring-4 ring-[var(--card)]", tone[h.action])} />
              <div className="text-sm font-medium">
                {actionText[h.action]}
                {h.fromSection && h.toSection ? (
                  <span className="font-normal text-[var(--muted-foreground)]">
                    {" "}
                    · {h.fromSection} → {h.toSection}
                  </span>
                ) : h.toSection ? (
                  <span className="font-normal text-[var(--muted-foreground)]"> · {h.toSection}</span>
                ) : null}
              </div>
              {h.reason ? <div className="mt-0.5 text-sm text-[var(--muted-foreground)]">“{h.reason}”</div> : null}
              <div className="mt-0.5 text-xs text-[var(--muted-foreground)]">
                {new Date(h.occurredOn.endsWith("Z") ? h.occurredOn : `${h.occurredOn}Z`).toLocaleString()}
                {h.byUserName ? ` · ${h.byUserName}` : ""}
              </div>
            </li>
          ))}
        </ol>
      )}
    </Panel>
  );
}

/* ------------------------------------------------------------------ dialogs */

function EditDialog({ profile, onClose, onSaved }: { profile: StudentProfile; onClose: () => void; onSaved: (p: StudentProfile) => void }) {
  const s = profile.student;
  const [form, setForm] = useState<SaveStudentRequest>({
    firstName: s.firstName,
    lastName: s.lastName,
    gender: s.gender,
    dateOfBirth: s.dateOfBirth,
    bFormNo: s.bFormNo ?? "",
    bloodGroup: s.bloodGroup ?? "",
    religion: s.religion ?? "",
    nationality: s.nationality ?? "",
    address: s.address ?? "",
    phone: s.phone ?? "",
    admissionDate: s.admissionDate,
    previousSchool: s.previousSchool ?? "",
    notes: s.notes ?? "",
  });
  const [saving, setSaving] = useState(false);
  const set = (p: Partial<SaveStudentRequest>) => setForm((f) => ({ ...f, ...p }));

  async function save() {
    setSaving(true);
    try {
      onSaved((await unwrap(api.put(`/students/${s.id}`, form))) as StudentProfile);
      toast.success("Profile updated");
      onClose();
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Save failed");
    } finally {
      setSaving(false);
    }
  }

  return (
    <Modal
      open
      wide
      title="Edit student"
      onClose={onClose}
      footer={
        <>
          <Btn type="button" variant="outline" onClick={onClose}>
            Cancel
          </Btn>
          <Btn type="button" onClick={save} disabled={saving}>
            {saving ? "Saving…" : "Save changes"}
          </Btn>
        </>
      }
    >
      <div className="grid gap-4 sm:grid-cols-2">
        <Field label="First name" required>
          <TextInput value={form.firstName} onChange={(e) => set({ firstName: e.target.value })} />
        </Field>
        <Field label="Last name" required>
          <TextInput value={form.lastName} onChange={(e) => set({ lastName: e.target.value })} />
        </Field>
        <Field label="Gender">
          <Select value={form.gender} onChange={(e) => set({ gender: e.target.value as Gender })}>
            {(Object.keys(genderLabel) as Gender[]).map((g) => (
              <option key={g} value={g}>
                {genderLabel[g]}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Date of birth">
          <TextInput type="date" value={form.dateOfBirth} onChange={(e) => set({ dateOfBirth: e.target.value })} />
        </Field>
        <Field label="B-Form number">
          <TextInput value={form.bFormNo ?? ""} onChange={(e) => set({ bFormNo: e.target.value })} />
        </Field>
        <Field label="Blood group">
          <TextInput value={form.bloodGroup ?? ""} onChange={(e) => set({ bloodGroup: e.target.value })} />
        </Field>
        <Field label="Religion">
          <TextInput value={form.religion ?? ""} onChange={(e) => set({ religion: e.target.value })} />
        </Field>
        <Field label="Nationality">
          <TextInput value={form.nationality ?? ""} onChange={(e) => set({ nationality: e.target.value })} />
        </Field>
        <Field label="Address" className="sm:col-span-2">
          <TextInput value={form.address ?? ""} onChange={(e) => set({ address: e.target.value })} />
        </Field>
        <Field label="Phone">
          <TextInput value={form.phone ?? ""} onChange={(e) => set({ phone: e.target.value })} />
        </Field>
        <Field label="Admission date">
          <TextInput type="date" value={form.admissionDate} onChange={(e) => set({ admissionDate: e.target.value })} />
        </Field>
        <Field label="Previous school" className="sm:col-span-2">
          <TextInput value={form.previousSchool ?? ""} onChange={(e) => set({ previousSchool: e.target.value })} />
        </Field>
        <Field label="Notes" className="sm:col-span-2">
          <TextArea rows={2} value={form.notes ?? ""} onChange={(e) => set({ notes: e.target.value })} />
        </Field>
      </div>
    </Modal>
  );
}

function SectionDialog({
  profile,
  mode,
  onClose,
  onSaved,
}: {
  profile: StudentProfile;
  mode: "change" | "rejoin";
  onClose: () => void;
  onSaved: (p: StudentProfile) => void;
}) {
  const current = profile.enrollment;
  const [sections, setSections] = useState<Section[]>([]);
  const [sectionId, setSectionId] = useState<number | null>(null);
  const [rollNo, setRollNo] = useState("");
  const [reason, setReason] = useState("");
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    (async () => {
      try {
        // Change: same session as now. Rejoin: the campus's current session (the server refuses others).
        let sessionId = current?.sessionId ?? null;
        if (mode === "rejoin") {
          const list = (await unwrap(api.get("/academic-sessions"))) as AcademicSessionList;
          sessionId = list.effectiveCurrentId ?? null;
        }
        const rows = (await unwrap(api.get(sessionId ? `/sections?sessionId=${sessionId}` : "/sections"))) as Section[];
        setSections(rows.filter((s) => s.isActive && s.campusId === profile.student.campusId));
      } catch {
        setSections([]);
      }
    })();
  }, [mode, current, profile.student.campusId]);

  // Same class first, then the rest; the current section is not offered for a change.
  const options = sections
    .filter((s) => mode === "rejoin" || s.id !== current?.sectionId)
    .sort((a, b) => Number(b.classId === current?.classId) - Number(a.classId === current?.classId) || a.classOrder - b.classOrder || a.name.localeCompare(b.name));

  async function save() {
    setSaving(true);
    try {
      const body =
        mode === "change"
          ? { sectionId, rollNo: rollNo ? Number(rollNo) : null, reason: reason || null }
          : { sectionId, rollNo: rollNo ? Number(rollNo) : null };
      onSaved((await unwrap(api.post(`/students/${profile.student.id}/${mode === "change" ? "change-section" : "rejoin"}`, body))) as StudentProfile);
      toast.success(mode === "change" ? "Section changed" : "Student rejoined");
      onClose();
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Save failed");
    } finally {
      setSaving(false);
    }
  }

  return (
    <Modal
      open
      title={mode === "change" ? "Change section" : "Rejoin student"}
      description={current ? `Currently ${current.className} – ${current.sectionName}` : undefined}
      onClose={onClose}
      footer={
        <>
          <Btn type="button" variant="outline" onClick={onClose}>
            Cancel
          </Btn>
          <Btn type="button" onClick={save} disabled={saving || (mode === "change" && !sectionId)}>
            {saving ? "Saving…" : mode === "change" ? "Move student" : "Rejoin"}
          </Btn>
        </>
      }
    >
      <div className="space-y-4">
        <Field label="Section" required={mode === "change"}>
          <Select value={sectionId ?? ""} onChange={(e) => setSectionId(e.target.value ? Number(e.target.value) : null)}>
            <option value="">{mode === "rejoin" ? "Previous section" : "Choose a section"}</option>
            {options.map((s) => (
              <option key={s.id} value={s.id}>
                {s.className} – {s.name} · {s.sessionName} ({s.activeStudents ?? 0}
                {s.capacity != null ? `/${s.capacity}` : ""})
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Roll number" hint="Leave empty to use the next free number.">
          <TextInput type="number" min={1} value={rollNo} onChange={(e) => setRollNo(e.target.value)} className="max-w-[160px]" />
        </Field>
        {mode === "change" ? (
          <Field label="Reason">
            <TextInput value={reason} onChange={(e) => setReason(e.target.value)} placeholder="Optional" />
          </Field>
        ) : null}
      </div>
    </Modal>
  );
}

function LeaveDialog({ profile, onClose, onSaved }: { profile: StudentProfile; onClose: () => void; onSaved: (p: StudentProfile) => void }) {
  const [leftOn, setLeftOn] = useState(new Date().toISOString().slice(0, 10));
  const [reason, setReason] = useState("");
  const [saving, setSaving] = useState(false);

  async function save() {
    setSaving(true);
    try {
      onSaved((await unwrap(api.post(`/students/${profile.student.id}/leave`, { leftOn, reason }))) as StudentProfile);
      toast.success("Marked as left");
      onClose();
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Save failed");
    } finally {
      setSaving(false);
    }
  }

  return (
    <Modal
      open
      title="Leave school"
      description={`${profile.student.fullName} will be marked as left. Their records stay, and they can rejoin later.`}
      onClose={onClose}
      footer={
        <>
          <Btn type="button" variant="outline" onClick={onClose}>
            Cancel
          </Btn>
          <Btn type="button" variant="danger" onClick={save} disabled={saving || !reason.trim()}>
            {saving ? "Saving…" : "Mark as left"}
          </Btn>
        </>
      }
    >
      <div className="space-y-4">
        <Field label="Leaving date" required>
          <TextInput type="date" value={leftOn} onChange={(e) => setLeftOn(e.target.value)} />
        </Field>
        <Field label="Reason" required>
          <TextArea rows={2} value={reason} onChange={(e) => setReason(e.target.value)} placeholder="e.g. Family moved to another city" />
        </Field>
      </div>
    </Modal>
  );
}

function AddGuardianDialog({ profile, onClose, onSaved }: { profile: StudentProfile; onClose: () => void; onSaved: (p: StudentProfile) => void }) {
  const [relation, setRelation] = useState<GuardianRelation>("Mother");
  const [cnic, setCnic] = useState("");
  const [phone, setPhone] = useState("");
  const [fullName, setFullName] = useState("");
  const [occupation, setOccupation] = useState("");
  const [isPrimary, setIsPrimary] = useState(false);
  const [isEmergency, setIsEmergency] = useState(false);
  const [match, setMatch] = useState<GuardianLookup | null>(null);
  const [linked, setLinked] = useState<GuardianLookup | null>(null);
  const [saving, setSaving] = useState(false);

  async function lookup() {
    if (linked) return;
    const c = digits(cnic);
    const p = digits(phone);
    if (c.length < 13 && p.length < 10) return;
    try {
      const rows = (await unwrap(api.get(`/guardians/lookup?${c.length >= 13 ? `cnic=${c}` : `phone=${p}`}`))) as GuardianLookup[];
      setMatch(rows[0] ?? null);
    } catch {
      /* convenience only */
    }
  }

  async function save() {
    setSaving(true);
    try {
      const body = {
        guardianId: linked?.id ?? null,
        newGuardian: { fullName: linked ? linked.fullName : fullName, cnic: cnic || null, phone, altPhone: null, email: null, occupation: occupation || null, address: null },
        relation,
        isPrimary,
        isEmergencyContact: isEmergency,
      };
      onSaved((await unwrap(api.post(`/students/${profile.student.id}/guardians`, body))) as StudentProfile);
      toast.success("Guardian added");
      onClose();
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Could not add guardian");
    } finally {
      setSaving(false);
    }
  }

  return (
    <Modal
      open
      title="Add guardian"
      onClose={onClose}
      footer={
        <>
          <Btn type="button" variant="outline" onClick={onClose}>
            Cancel
          </Btn>
          <Btn type="button" onClick={save} disabled={saving || (!linked && (!fullName.trim() || digits(phone).length < 10))}>
            <Plus /> {saving ? "Adding…" : "Add guardian"}
          </Btn>
        </>
      }
    >
      <div className="space-y-4">
        <div className="grid gap-4 sm:grid-cols-3">
          <Field label="Relation">
            <Select value={relation} onChange={(e) => setRelation(e.target.value as GuardianRelation)}>
              {(Object.keys(relationLabel) as GuardianRelation[]).map((r) => (
                <option key={r} value={r}>
                  {relationLabel[r]}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="CNIC">
            <TextInput value={cnic} disabled={!!linked} onChange={(e) => { setCnic(e.target.value); setMatch(null); }} onBlur={lookup} />
          </Field>
          <Field label="Phone" required>
            <TextInput value={phone} disabled={!!linked} onChange={(e) => { setPhone(e.target.value); setMatch(null); }} onBlur={lookup} />
          </Field>
        </div>
        {match && !linked ? (
          <div className="flex items-center justify-between gap-3 rounded-lg bg-[var(--info-soft)] px-3 py-2.5 text-sm text-[var(--info)]">
            <span>
              Link existing: <strong>{match.fullName}</strong> ({match.childrenCount} {match.childrenCount === 1 ? "child" : "children"})
            </span>
            <Btn type="button" size="sm" onClick={() => setLinked(match)}>
              Link
            </Btn>
          </div>
        ) : null}
        {linked ? (
          <div className="rounded-lg border border-[var(--border)] bg-[var(--muted)] px-3 py-2.5 text-sm">
            Linking <strong>{linked.fullName}</strong>
          </div>
        ) : (
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="Full name" required>
              <TextInput value={fullName} onChange={(e) => setFullName(e.target.value)} />
            </Field>
            <Field label="Occupation">
              <TextInput value={occupation} onChange={(e) => setOccupation(e.target.value)} />
            </Field>
          </div>
        )}
        <Switch checked={isPrimary} onChange={setIsPrimary} label="Make primary guardian" description="The current primary guardian will be replaced." />
        <Switch checked={isEmergency} onChange={setIsEmergency} label="Emergency contact" />
      </div>
    </Modal>
  );
}
