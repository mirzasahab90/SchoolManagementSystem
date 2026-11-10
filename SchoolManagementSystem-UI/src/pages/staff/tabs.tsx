import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { CheckCircle2, Circle, Download, KeyRound, Link2, Plus, RefreshCw, Trash2, Upload } from "lucide-react";
import { toast } from "sonner";
import { Modal } from "@/components/modal";
import { Btn, ChoiceCard, EmptyState, Field, IconButton, Panel, Select, StatusPill, Switch, TextInput } from "@/components/sms-ui";
import { useAuth } from "@/lib/auth";
import { api, downloadBlob, saveBlob, unwrap } from "@/lib/api";
import { cn } from "@/lib/utils";
import {
  roleForDesignation,
  staffDocumentTypeLabel,
  type AssignmentOptions,
  type RoleListItem,
  type StaffDocumentType,
  type StaffProfile,
  type UserPick,
} from "@/lib/types";
import { errorText, formatDate } from "@/pages/hiring/shared";

const DOC_TYPES = ".pdf,.jpg,.jpeg,.png,.webp";
const DOC_LIMIT = 10_485_760;
const formatSize = (n: number) => (n < 1024 * 1024 ? `${Math.max(1, Math.round(n / 1024))} KB` : `${(n / 1024 / 1024).toFixed(1)} MB`);

/* ------------------------------------------------------------------ Documents */

export function DocumentsTab({ profile, canEdit, onChange }: { profile: StaffProfile; canEdit: boolean; onChange: () => Promise<void> }) {
  const [type, setType] = useState<StaffDocumentType>(profile.checklist.missingDocuments[0] ?? "Other");
  const [busy, setBusy] = useState(false);
  const id = profile.staff.id;
  const missing = profile.checklist.missingDocuments;

  const upload = async (file: File) => {
    if (file.size > DOC_LIMIT) return toast.error("Files must be 10 MB or smaller.");
    const data = new FormData();
    data.append("type", type);
    data.append("file", file);
    setBusy(true);
    try {
      await unwrap(api.post(`/staff/${id}/documents`, data));
      toast.success(`${staffDocumentTypeLabel[type]} uploaded`);
      await onChange();
    } catch (err) {
      toast.error(errorText(err, "Upload failed"));
    } finally {
      setBusy(false);
    }
  };

  return (
    <Panel
      title="Documents"
      description={missing.length ? `Missing for the checklist: ${missing.map((m) => staffDocumentTypeLabel[m]).join(", ")}` : "Required documents received."}
      action={
        canEdit ? (
          <div className="flex items-center gap-2">
            <Select aria-label="Document type" value={type} onChange={(e) => setType(e.target.value as StaffDocumentType)} className="h-8 w-auto text-xs">
              {(Object.keys(staffDocumentTypeLabel) as StaffDocumentType[]).map((t) => (
                <option key={t} value={t}>
                  {staffDocumentTypeLabel[t]}
                </option>
              ))}
            </Select>
            <label className={cn("inline-flex h-8 cursor-pointer items-center gap-1.5 rounded-lg bg-[var(--primary)] px-2.5 text-xs font-medium text-[var(--primary-foreground)] [&_svg]:size-4", busy && "pointer-events-none opacity-50")}>
              <Upload /> Upload
              <input
                type="file"
                accept={DOC_TYPES}
                className="hidden"
                onChange={(e) => {
                  const f = e.target.files?.[0];
                  e.target.value = "";
                  if (f) void upload(f);
                }}
              />
            </label>
          </div>
        ) : null
      }
    >
      {profile.documents.length === 0 ? (
        <p className="text-sm text-[var(--muted-foreground)]">No documents yet.</p>
      ) : (
        <ul className="divide-y divide-[var(--border)]">
          {profile.documents.map((d) => (
            <li key={d.id} className="flex items-center gap-3 py-2.5">
              <StatusPill tone={d.type === "Cnic" || d.type === "Degree" ? "primary" : "neutral"}>{staffDocumentTypeLabel[d.type]}</StatusPill>
              <div className="min-w-0 flex-1">
                <div className="truncate text-sm">{d.fileName}</div>
                <div className="text-xs text-[var(--muted-foreground)]">
                  {formatSize(d.sizeBytes)} · {formatDate(d.uploadedOn)}
                </div>
              </div>
              <IconButton
                label="Download"
                onClick={async () => {
                  try {
                    const { blob, fileName } = await downloadBlob(`/staff/${id}/documents/${d.id}`);
                    saveBlob(blob, fileName ?? d.fileName);
                  } catch (err) {
                    toast.error(errorText(err, "Download failed"));
                  }
                }}
              >
                <Download />
              </IconButton>
              {canEdit ? (
                <IconButton
                  label="Delete"
                  tone="danger"
                  onClick={async () => {
                    try {
                      await unwrap(api.delete(`/staff/${id}/documents/${d.id}`));
                      await onChange();
                    } catch (err) {
                      toast.error(errorText(err, "Could not delete"));
                    }
                  }}
                >
                  <Trash2 />
                </IconButton>
              ) : null}
            </li>
          ))}
        </ul>
      )}
    </Panel>
  );
}

/* ------------------------------------------------------------------ Checklist */

export function ChecklistTab({
  profile,
  canEdit,
  onChange,
  goTo,
}: {
  profile: StaffProfile;
  canEdit: boolean;
  onChange: (p: StaffProfile) => void;
  goTo: (tab: "documents" | "account") => void;
}) {
  const c = profile.checklist;
  const [busy, setBusy] = useState(false);
  const toggle = async (item: "contract" | "orientation", done: boolean) => {
    setBusy(true);
    try {
      const p = (await unwrap(api.post(`/staff/${profile.staff.id}/checklist/${item}`, { done }))) as StaffProfile;
      onChange(p);
      if (p.staff.status === "Active" && profile.staff.status === "Onboarding") toast.success(`${p.staff.fullName} is now Active`);
    } catch (err) {
      toast.error(errorText(err, "Could not update"));
    } finally {
      setBusy(false);
    }
  };

  const rows = [
    {
      key: "documents",
      title: "Documents received",
      done: c.documentsReceived,
      note: c.documentsReceived ? "Required documents are uploaded." : `Upload ${c.missingDocuments.map((m) => staffDocumentTypeLabel[m]).join(" and ")}.`,
      action: !c.documentsReceived ? (
        <Btn type="button" size="sm" variant="outline" onClick={() => goTo("documents")}>
          Open documents
        </Btn>
      ) : null,
    },
    {
      key: "contract",
      title: "Contract signed",
      done: c.contractSigned,
      note: c.contractSigned ? `Marked on ${formatDate(c.contractSignedOn)}` : "Tick when the signed contract is received.",
      action: <Switch checked={c.contractSigned} disabled={!canEdit || busy} onChange={(v: boolean) => void toggle("contract", v)} label="Done" />,
    },
    {
      key: "login",
      title: "Login created",
      done: c.loginCreated,
      note: c.loginCreated ? "A login is linked." : "Create a login on the Account tab.",
      action: !c.loginCreated ? (
        <Btn type="button" size="sm" variant="outline" onClick={() => goTo("account")}>
          Open account
        </Btn>
      ) : null,
    },
    {
      key: "orientation",
      title: "Orientation done",
      done: c.orientationDone,
      note: c.orientationDone ? `Marked on ${formatDate(c.orientationDoneOn)}` : "Tick after the induction / orientation.",
      action: <Switch checked={c.orientationDone} disabled={!canEdit || busy} onChange={(v: boolean) => void toggle("orientation", v)} label="Done" />,
    },
  ];

  return (
    <Panel title="Onboarding checklist" description="When all four are done, an onboarding staff member becomes Active.">
      <div className="mb-4 flex items-center gap-3">
        <div className="h-2 flex-1 overflow-hidden rounded-full bg-[var(--muted)]">
          <div className="h-full rounded-full bg-[var(--success)] transition-all" style={{ width: `${(c.doneCount / 4) * 100}%` }} />
        </div>
        <span className="tabular text-sm font-medium">{c.doneCount}/4</span>
      </div>
      <ul className="divide-y divide-[var(--border)]">
        {rows.map((r) => (
          <li key={r.key} className="flex flex-wrap items-center gap-3 py-3">
            {r.done ? <CheckCircle2 className="size-5 text-[var(--success)]" /> : <Circle className="size-5 text-[var(--muted-foreground)]" />}
            <div className="min-w-0 flex-1">
              <div className="text-sm font-medium">{r.title}</div>
              <div className="text-xs text-[var(--muted-foreground)]">{r.note}</div>
            </div>
            {r.action}
          </li>
        ))}
      </ul>
    </Panel>
  );
}

/* ------------------------------------------------------------------ Teaching */

export function TeachingTab({ profile, canEdit, onChange }: { profile: StaffProfile; canEdit: boolean; onChange: (p: StaffProfile) => void }) {
  const [assigning, setAssigning] = useState(false);
  const grouped = useMemo(() => {
    const map = new Map<number, { label: string; items: StaffProfile["assignments"] }>();
    for (const a of profile.assignments) {
      const g = map.get(a.sectionId) ?? { label: `${a.className} – ${a.sectionName}`, items: [] };
      g.items.push(a);
      map.set(a.sectionId, g);
    }
    return [...map.values()];
  }, [profile.assignments]);

  const end = async (assignmentId: number) => {
    try {
      onChange((await unwrap(api.delete(`/staff/${profile.staff.id}/assignments/${assignmentId}`))) as StaffProfile);
    } catch (err) {
      toast.error(errorText(err, "Could not remove"));
    }
  };

  return (
    <div className="grid gap-4 lg:grid-cols-3">
      <Panel
        className="lg:col-span-2"
        title="Classes and subjects"
        description="Current session"
        action={
          canEdit ? (
            <Btn type="button" size="sm" onClick={() => setAssigning(true)}>
              <Plus /> Assign classes
            </Btn>
          ) : null
        }
      >
        {grouped.length === 0 ? (
          <p className="text-sm text-[var(--muted-foreground)]">No classes assigned yet.</p>
        ) : (
          <ul className="space-y-3">
            {grouped.map((g) => (
              <li key={g.label}>
                <div className="mb-1.5 text-sm font-medium">{g.label}</div>
                <div className="flex flex-wrap gap-1.5">
                  {g.items.map((a) => (
                    <span key={a.id} className="inline-flex items-center gap-1 rounded-md bg-[var(--primary-soft)] py-0.5 pr-1 pl-2 text-xs text-[var(--primary-soft-foreground)]">
                      {a.subjectName}
                      {canEdit ? (
                        <button type="button" aria-label={`Remove ${a.subjectName}`} className="rounded px-1 hover:bg-black/10" onClick={() => void end(a.id)}>
                          ×
                        </button>
                      ) : null}
                    </span>
                  ))}
                </div>
              </li>
            ))}
          </ul>
        )}
      </Panel>
      <Panel title="Class teacher of" description="Set on the Sections page">
        {profile.classTeacherOf.length === 0 ? (
          <p className="text-sm text-[var(--muted-foreground)]">None.</p>
        ) : (
          <div className="flex flex-wrap gap-1.5">
            {profile.classTeacherOf.map((c) => (
              <StatusPill key={c.sectionId} tone="success">
                {c.className} – {c.sectionName}
              </StatusPill>
            ))}
          </div>
        )}
        <Link to="/admin/academics/sections" className="mt-3 inline-block text-xs font-medium text-[var(--primary)] hover:underline">
          Open sections
        </Link>
      </Panel>
      {assigning ? <AssignDialog profile={profile} onClose={() => setAssigning(false)} onSaved={(p) => (onChange(p), setAssigning(false))} /> : null}
    </div>
  );
}

function AssignDialog({ profile, onClose, onSaved }: { profile: StaffProfile; onClose: () => void; onSaved: (p: StaffProfile) => void }) {
  const [options, setOptions] = useState<AssignmentOptions | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [sectionId, setSectionId] = useState(0);
  const [picked, setPicked] = useState<number[]>([]);
  const [busy, setBusy] = useState(false);
  const me = profile.staff.id;

  useEffect(() => {
    unwrap(api.get(`/staff/${me}/assignments/options`))
      .then((o) => {
        const opts = o as AssignmentOptions;
        setOptions(opts);
        setSectionId(opts.sections[0]?.sectionId ?? 0);
      })
      .catch((err) => setError(errorText(err, "Could not load sections")));
  }, [me]);

  const section = options?.sections.find((s) => s.sectionId === sectionId);
  useEffect(() => setPicked([]), [sectionId]);

  return (
    <Modal
      open
      title="Assign classes"
      description={options ? `Session ${options.sessionName}` : undefined}
      onClose={onClose}
      footer={
        <>
          <Btn type="button" variant="outline" onClick={onClose}>
            Cancel
          </Btn>
          <Btn
            type="button"
            disabled={busy || picked.length === 0}
            onClick={async () => {
              setBusy(true);
              try {
                const p = await unwrap(api.post(`/staff/${me}/assignments`, { sectionId, subjectIds: picked }));
                toast.success("Classes assigned");
                onSaved(p as StaffProfile);
              } catch (err) {
                toast.error(errorText(err, "Could not assign"));
              } finally {
                setBusy(false);
              }
            }}
          >
            {busy ? "Saving…" : `Assign ${picked.length || ""}`.trim()}
          </Btn>
        </>
      }
    >
      {error ? (
        <p className="text-sm text-[var(--danger)]">{error}</p>
      ) : !options ? (
        <p className="text-sm text-[var(--muted-foreground)]">Loading…</p>
      ) : options.sections.length === 0 ? (
        <EmptyState icon={Plus} title="No sections" description="Add sections for the current session first." />
      ) : (
        <div className="space-y-4">
          <Field label="Section">
            <Select value={sectionId} onChange={(e) => setSectionId(Number(e.target.value))}>
              {options.sections.map((s) => (
                <option key={s.sectionId} value={s.sectionId}>
                  {s.className} – {s.sectionName}
                </option>
              ))}
            </Select>
          </Field>
          <div>
            <div className="mb-1.5 text-[13px] font-medium">Subjects</div>
            {section && section.subjects.length === 0 ? (
              <p className="text-xs text-[var(--muted-foreground)]">This class has no subjects yet.</p>
            ) : (
              <div className="grid gap-2 sm:grid-cols-2">
                {section?.subjects.map((sub) => {
                  const mine = sub.teacherStaffId === me;
                  const taken = sub.teacherStaffId != null && !mine;
                  return (
                    <ChoiceCard
                      key={sub.subjectId}
                      checked={mine || picked.includes(sub.subjectId)}
                      disabled={taken || mine}
                      onToggle={() => setPicked((p) => (p.includes(sub.subjectId) ? p.filter((x) => x !== sub.subjectId) : [...p, sub.subjectId]))}
                      title={sub.subjectName}
                      description={mine ? "Already yours" : taken ? `Taught by ${sub.teacherName}` : undefined}
                    />
                  );
                })}
              </div>
            )}
          </div>
        </div>
      )}
    </Modal>
  );
}

/* ------------------------------------------------------------------ Account */

function generatePassword() {
  const chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";
  const bytes = new Uint32Array(12);
  crypto.getRandomValues(bytes);
  return "Sms#" + Array.from(bytes, (b) => chars[b % chars.length]).join("");
}

export function AccountTab({ profile, canEdit, onChange }: { profile: StaffProfile; canEdit: boolean; onChange: (p: StaffProfile) => void }) {
  const { can } = useAuth();
  const canCreate = canEdit && can("users", "Add");
  const [mode, setMode] = useState<"create" | "link" | null>(null);
  const account = profile.account;

  if (account) {
    return (
      <Panel title="Login">
        <dl className="grid gap-x-6 gap-y-4 sm:grid-cols-2 lg:grid-cols-4">
          <div>
            <dt className="text-xs text-[var(--muted-foreground)]">Email</dt>
            <dd className="mt-0.5 truncate text-sm">{account.email}</dd>
          </div>
          <div>
            <dt className="text-xs text-[var(--muted-foreground)]">Roles</dt>
            <dd className="mt-0.5 flex flex-wrap gap-1">
              {account.roles.map((r) => (
                <StatusPill key={r}>{r}</StatusPill>
              ))}
            </dd>
          </div>
          <div>
            <dt className="text-xs text-[var(--muted-foreground)]">State</dt>
            <dd className="mt-0.5">
              <StatusPill tone={account.isActive ? "success" : "neutral"} dot>
                {account.isActive ? "Active" : "Disabled"}
              </StatusPill>
            </dd>
          </div>
          <div>
            <dt className="text-xs text-[var(--muted-foreground)]">Password</dt>
            <dd className="mt-0.5 text-sm">{account.mustChangePassword ? "Must change at first sign-in" : "Set by the user"}</dd>
          </div>
        </dl>
        {can("users", "View") ? (
          <Link to="/admin/users" className="mt-4 inline-block text-xs font-medium text-[var(--primary)] hover:underline">
            Open in Users
          </Link>
        ) : null}
      </Panel>
    );
  }

  return (
    <Panel title="Login" description="Give this staff member their own sign-in.">
      {!canCreate ? (
        <p className="text-sm text-[var(--muted-foreground)]">No login yet. Someone with permission to add users can create one here.</p>
      ) : (
        <div className="flex flex-wrap gap-2">
          <Btn type="button" onClick={() => setMode("create")}>
            <KeyRound /> Create login
          </Btn>
          <Btn type="button" variant="outline" onClick={() => setMode("link")}>
            <Link2 /> Link existing user
          </Btn>
        </div>
      )}
      {mode === "create" ? <CreateLoginDialog profile={profile} onClose={() => setMode(null)} onSaved={(p) => (onChange(p), setMode(null))} /> : null}
      {mode === "link" ? <LinkLoginDialog profile={profile} onClose={() => setMode(null)} onSaved={(p) => (onChange(p), setMode(null))} /> : null}
    </Panel>
  );
}

function CreateLoginDialog({ profile, onClose, onSaved }: { profile: StaffProfile; onClose: () => void; onSaved: (p: StaffProfile) => void }) {
  const [email, setEmail] = useState(profile.staff.email ?? "");
  const [password, setPassword] = useState(generatePassword);
  const [roles, setRoles] = useState<RoleListItem[]>([]);
  const [roleId, setRoleId] = useState(0);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    unwrap(api.get("/roles?assignable=true"))
      .then((r) => {
        const list = (r as RoleListItem[]).filter((x) => x.isActive && (x.campusId == null || x.campusId === profile.staff.campusId));
        setRoles(list);
        const wanted = roleForDesignation[profile.staff.designation];
        setRoleId(list.find((x) => x.name === wanted)?.id ?? list[0]?.id ?? 0);
      })
      .catch(() => setRoles([]));
  }, [profile.staff.campusId, profile.staff.designation]);

  return (
    <Modal
      open
      title="Create login"
      description="The staff member must change this temporary password when they first sign in."
      onClose={onClose}
      footer={
        <>
          <Btn type="button" variant="outline" onClick={onClose}>
            Cancel
          </Btn>
          <Btn
            type="button"
            disabled={busy || !email.trim() || password.length < 8 || !roleId}
            onClick={async () => {
              setBusy(true);
              try {
                const p = await unwrap(api.post(`/staff/${profile.staff.id}/account`, { email: email.trim(), password, roleId }));
                toast.success("Login created. Share the temporary password privately.");
                onSaved(p as StaffProfile);
              } catch (err) {
                toast.error(errorText(err, "Could not create the login"));
              } finally {
                setBusy(false);
              }
            }}
          >
            {busy ? "Creating…" : "Create login"}
          </Btn>
        </>
      }
    >
      <div className="space-y-4">
        <Field label="Email" required>
          <TextInput type="email" value={email} maxLength={256} onChange={(e) => setEmail(e.target.value)} autoFocus />
        </Field>
        <Field label="Temporary password" required>
          <div className="flex gap-2">
            <TextInput value={password} onChange={(e) => setPassword(e.target.value)} autoComplete="new-password" className="font-mono" />
            <IconButton label="Generate another" onClick={() => setPassword(generatePassword())}>
              <RefreshCw />
            </IconButton>
          </div>
        </Field>
        <Field label="Role" required>
          <Select value={roleId} onChange={(e) => setRoleId(Number(e.target.value))}>
            {roles.length === 0 ? <option value={0}>No roles you can assign</option> : null}
            {roles.map((r) => (
              <option key={r.id} value={r.id}>
                {r.name}
              </option>
            ))}
          </Select>
        </Field>
      </div>
    </Modal>
  );
}

function LinkLoginDialog({ profile, onClose, onSaved }: { profile: StaffProfile; onClose: () => void; onSaved: (p: StaffProfile) => void }) {
  const [users, setUsers] = useState<UserPick[] | null>(null);
  const [userId, setUserId] = useState(0);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    unwrap(api.get("/users?pick=true"))
      .then((u) => {
        const list = u as UserPick[];
        setUsers(list);
        setUserId(list[0]?.id ?? 0);
      })
      .catch(() => setUsers([]));
  }, []);

  return (
    <Modal
      open
      title="Link existing user"
      description="Pick a user of this campus who is not linked to other staff."
      onClose={onClose}
      footer={
        <>
          <Btn type="button" variant="outline" onClick={onClose}>
            Cancel
          </Btn>
          <Btn
            type="button"
            disabled={busy || !userId}
            onClick={async () => {
              setBusy(true);
              try {
                const p = await unwrap(api.post(`/staff/${profile.staff.id}/account/link`, { userId }));
                toast.success("Login linked");
                onSaved(p as StaffProfile);
              } catch (err) {
                toast.error(errorText(err, "Could not link"));
              } finally {
                setBusy(false);
              }
            }}
          >
            {busy ? "Linking…" : "Link"}
          </Btn>
        </>
      }
    >
      <Field label="User">
        <Select value={userId} onChange={(e) => setUserId(Number(e.target.value))}>
          {users?.length === 0 ? <option value={0}>No users available</option> : null}
          {users?.map((u) => (
            <option key={u.id} value={u.id}>
              {u.fullName}
            </option>
          ))}
        </Select>
      </Field>
    </Modal>
  );
}
