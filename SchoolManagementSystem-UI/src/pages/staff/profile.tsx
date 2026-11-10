import { useCallback, useEffect, useRef, useState, type ReactNode } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { ArrowLeft, IdCard, Pencil, Upload } from "lucide-react";
import { toast } from "sonner";
import { ConfirmDialog, Modal } from "@/components/modal";
import { StudentPhoto } from "@/components/student-photo";
import { Btn, EmptyState, Field, Panel, Skeleton, StatusPill, TextArea, TextInput } from "@/components/sms-ui";
import { useAuth } from "@/lib/auth";
import { api, unwrap } from "@/lib/api";
import { cn } from "@/lib/utils";
import {
  categoryLabel,
  designationLabel,
  employmentTypeLabel,
  genderLabel,
  staffStatusLabel,
  type StaffProfile,
  type StaffStatus,
} from "@/lib/types";
import { ActionMenu, errorText, formatDate, formatMoney, staffTone, todayIso, type MenuItemDef } from "@/pages/hiring/shared";
import { StaffFormDialog } from "./staff-form";
import { AccountTab, ChecklistTab, DocumentsTab, TeachingTab } from "./tabs";

type Tab = "overview" | "documents" | "checklist" | "teaching" | "account";

export function StaffProfilePage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const { can } = useAuth();
  const [profile, setProfile] = useState<StaffProfile | null>(null);
  const [missing, setMissing] = useState(false);
  const [tab, setTab] = useState<Tab>("overview");
  const [dialog, setDialog] = useState<"edit" | "leave" | "delete" | null>(null);
  const [leaving, setLeaving] = useState<StaffStatus>("Resigned");
  const [photoVersion, setPhotoVersion] = useState(0);
  const photoInput = useRef<HTMLInputElement>(null);

  const load = useCallback(async () => {
    try {
      setProfile((await unwrap(api.get(`/staff/${id}`))) as StaffProfile);
    } catch (err) {
      if ((err as { status?: number })?.status === 404 || /not found/i.test(errorText(err, ""))) setMissing(true);
      else toast.error(errorText(err, "Could not load the staff member"));
    }
  }, [id]);

  useEffect(() => {
    void load();
  }, [load]);

  if (missing) {
    return (
      <div className="panel">
        <EmptyState
          icon={IdCard}
          title="Staff member not found"
          description="They may have been deleted or belong to another campus."
          action={
            <Btn type="button" variant="outline" onClick={() => navigate("/admin/staff")}>
              <ArrowLeft /> Staff
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

  const s = profile.staff;
  const left = s.status === "Resigned" || s.status === "Terminated";
  const canEdit = can("staff", "Edit") && !left;
  const canDelete = can("staff", "Delete") && s.status === "Onboarding" && !s.sourceCandidateId && !profile.account && profile.assignments.length === 0;

  const setStatus = async (status: StaffStatus) => {
    try {
      setProfile((await unwrap(api.post(`/staff/${s.id}/status`, { status, date: null, reason: null }))) as StaffProfile);
      toast.success(status === "OnLeave" ? "Marked on leave" : "Back from leave");
    } catch (err) {
      toast.error(errorText(err, "Could not change the status"));
    }
  };

  const uploadPhoto = async (file: File) => {
    const data = new FormData();
    data.append("file", file);
    try {
      await unwrap(api.post(`/staff/${s.id}/photo`, data));
      setPhotoVersion((v) => v + 1);
      await load();
      toast.success("Photo updated");
    } catch (err) {
      toast.error(errorText(err, "Could not upload the photo"));
    }
  };

  const menu: MenuItemDef[] = canEdit
    ? [
        ...(profile.allowedStatuses.includes("OnLeave") ? [{ label: "Mark on leave", onSelect: () => void setStatus("OnLeave") }] : []),
        ...(profile.allowedStatuses.includes("Active") ? [{ label: "Back from leave", onSelect: () => void setStatus("Active") }] : []),
        { label: "Resign…", onSelect: () => (setLeaving("Resigned"), setDialog("leave")) },
        { label: "Terminate…", danger: true, onSelect: () => (setLeaving("Terminated"), setDialog("leave")) },
        ...(canDelete ? [{ label: "Delete", danger: true, onSelect: () => setDialog("delete") }] : []),
      ]
    : [];

  const tabs: [Tab, string][] = [
    ["overview", "Overview"],
    ["documents", `Documents (${profile.documents.length})`],
    ["checklist", `Checklist ${profile.checklist.doneCount}/4`],
    ...(s.category === "Teaching" ? ([["teaching", `Teaching (${profile.assignments.length})`]] as [Tab, string][]) : []),
    ["account", "Account"],
  ];

  return (
    <div className="space-y-5">
      <Link to="/admin/staff" className="inline-flex items-center gap-1 text-xs font-medium text-[var(--muted-foreground)] hover:text-[var(--foreground)]">
        <ArrowLeft className="size-3.5" /> Staff
      </Link>

      <div className="panel flex flex-wrap items-center gap-5 p-6">
        <button
          type="button"
          disabled={!canEdit}
          onClick={() => photoInput.current?.click()}
          title={canEdit ? "Change photo" : undefined}
          className="group relative rounded-2xl outline-none focus-visible:ring-4 focus-visible:ring-[var(--ring)]"
        >
          <StudentPhoto studentId={s.id} path={`/staff/${s.id}/photo`} name={s.fullName} hasPhoto={profile.personal.hasPhoto} size="xl" version={photoVersion} />
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
            <h1 className="text-xl font-semibold">{s.fullName}</h1>
            <StatusPill tone={staffTone[s.status]} dot>
              {staffStatusLabel[s.status]}
            </StatusPill>
          </div>
          <div className="mt-1 flex flex-wrap items-center gap-2 text-sm text-[var(--muted-foreground)]">
            <span className="rounded-md border border-[var(--border)] px-1.5 py-0.5 font-mono text-xs">{s.employeeNo}</span>
            <span>{designationLabel[s.designation]}</span>
            <span>· {categoryLabel[s.category]}</span>
            <span className="font-mono text-xs">{s.campusCode}</span>
          </div>
          {left ? (
            <p className="mt-2 text-sm text-[var(--muted-foreground)]">
              Left on {formatDate(profile.personal.leftOn)} — {profile.personal.leaveReason}
            </p>
          ) : null}
        </div>
        <div className="flex flex-wrap items-center gap-2">
          {canEdit ? (
            <Btn type="button" variant="outline" onClick={() => setDialog("edit")}>
              <Pencil /> Edit
            </Btn>
          ) : null}
          <ActionMenu items={menu} />
        </div>
      </div>

      <div className="flex gap-1 overflow-x-auto border-b border-[var(--border)]">
        {tabs.map(([key, label]) => (
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

      {tab === "overview" ? <Overview profile={profile} /> : null}
      {tab === "documents" ? <DocumentsTab profile={profile} canEdit={canEdit} onChange={load} /> : null}
      {tab === "checklist" ? <ChecklistTab profile={profile} canEdit={canEdit} onChange={setProfile} goTo={setTab} /> : null}
      {tab === "teaching" ? <TeachingTab profile={profile} canEdit={canEdit} onChange={setProfile} /> : null}
      {tab === "account" ? <AccountTab profile={profile} canEdit={canEdit} onChange={setProfile} /> : null}

      {dialog === "edit" ? (
        <StaffFormDialog profile={profile} onClose={() => setDialog(null)} onSaved={(p) => (setProfile(p), setDialog(null))} />
      ) : null}
      {dialog === "leave" ? (
        <LeaveDialog
          profile={profile}
          status={leaving}
          onClose={() => setDialog(null)}
          onSaved={(p) => {
            setProfile(p);
            setDialog(null);
          }}
        />
      ) : null}
      <ConfirmDialog
        open={dialog === "delete"}
        title={`Delete ${s.fullName}?`}
        description="This removes a staff member added by mistake. Use Resign for someone who worked here."
        onClose={() => setDialog(null)}
        onConfirm={async () => {
          try {
            await unwrap(api.delete(`/staff/${s.id}`));
            toast.success("Staff member deleted");
            navigate("/admin/staff");
          } catch (err) {
            toast.error(errorText(err, "Could not delete"));
          }
        }}
      />
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

function Overview({ profile }: { profile: StaffProfile }) {
  const s = profile.staff;
  const p = profile.personal;
  return (
    <div className="grid gap-4 lg:grid-cols-2">
      <Panel title="Job">
        <dl className="grid gap-x-6 gap-y-4 sm:grid-cols-2">
          <Info label="Designation" value={designationLabel[s.designation]} />
          <Info label="Category" value={categoryLabel[s.category]} />
          <Info label="Employment" value={employmentTypeLabel[s.employmentType]} />
          <Info label="Joining date" value={formatDate(s.joiningDate)} />
          <Info label="Basic salary" value={formatMoney(s.basicSalary)} />
          <Info label="Hired from" value={s.sourceCandidateId ? <Link className="text-[var(--primary)] hover:underline" to={`/admin/hiring/candidates/${s.sourceCandidateId}`}>Candidate profile</Link> : "Added directly"} />
        </dl>
      </Panel>
      <Panel title="Personal">
        <dl className="grid gap-x-6 gap-y-4 sm:grid-cols-2">
          <Info label="CNIC" value={s.cnic ? <span className="font-mono">{s.cnic}</span> : null} />
          <Info label="Phone" value={s.phone} />
          <Info label="Email" value={s.email} />
          <Info label="Gender" value={p.gender ? genderLabel[p.gender] : null} />
          <Info label="Date of birth" value={p.dateOfBirth ? formatDate(p.dateOfBirth) : null} />
          <Info label="Address" value={p.address} />
        </dl>
      </Panel>
      <Panel title="Emergency contact">
        <dl className="grid gap-x-6 gap-y-4 sm:grid-cols-3">
          <Info label="Name" value={p.emergencyName} />
          <Info label="Relation" value={p.emergencyRelation} />
          <Info label="Phone" value={p.emergencyPhone} />
        </dl>
      </Panel>
      <Panel title="Qualification & bank">
        <dl className="grid gap-x-6 gap-y-4 sm:grid-cols-2">
          <Info label="Qualification" value={p.qualification} />
          <Info label="Experience" value={p.experienceYears != null ? `${p.experienceYears} years` : null} />
          <Info label="Previous employer" value={p.previousEmployer} />
          <Info label="Bank" value={p.bankName} />
          <Info label="Account" value={p.bankAccountMasked ? <span className="font-mono">{p.bankAccountMasked}</span> : null} />
        </dl>
      </Panel>
    </div>
  );
}

function LeaveDialog({
  profile,
  status,
  onClose,
  onSaved,
}: {
  profile: StaffProfile;
  status: StaffStatus;
  onClose: () => void;
  onSaved: (p: StaffProfile) => void;
}) {
  const [date, setDate] = useState(todayIso());
  const [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false);
  const verb = status === "Terminated" ? "Terminate" : "Resign";
  return (
    <Modal
      open
      title={`${verb} ${profile.staff.fullName}?`}
      description="Their login is turned off, their classes become free and class-teacher slots are cleared. This cannot be undone."
      onClose={onClose}
      footer={
        <>
          <Btn type="button" variant="outline" onClick={onClose}>
            Cancel
          </Btn>
          <Btn
            type="button"
            variant="danger"
            disabled={busy || !reason.trim() || !date}
            onClick={async () => {
              setBusy(true);
              try {
                const p = await unwrap(api.post(`/staff/${profile.staff.id}/status`, { status, date, reason: reason.trim() }));
                toast.success(`${profile.staff.fullName}: ${staffStatusLabel[status].toLowerCase()}`);
                onSaved(p as StaffProfile);
              } catch (err) {
                toast.error(errorText(err, "Could not save"));
              } finally {
                setBusy(false);
              }
            }}
          >
            {busy ? "Saving…" : verb}
          </Btn>
        </>
      }
    >
      <div className="space-y-4">
        <Field label="Leaving date" required>
          <TextInput type="date" value={date} min={profile.staff.joiningDate} onChange={(e) => setDate(e.target.value)} />
        </Field>
        <Field label="Reason" required>
          <TextArea rows={3} maxLength={500} value={reason} onChange={(e) => setReason(e.target.value)} />
        </Field>
      </div>
    </Modal>
  );
}
