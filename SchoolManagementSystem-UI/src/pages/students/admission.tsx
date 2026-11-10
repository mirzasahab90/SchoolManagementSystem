import { useEffect, useMemo, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { ArrowLeft, ArrowRight, Check, Link2, Loader2, Plus, Trash2, Upload, UserRound } from "lucide-react";
import { toast } from "sonner";
import {
  Btn,
  EmptyState,
  Field,
  FormSection,
  IconButton,
  PageHeader,
  Panel,
  Select,
  StatusPill,
  Switch,
  TextArea,
  TextInput,
} from "@/components/sms-ui";
import { useAuth } from "@/lib/auth";
import { api, unwrap } from "@/lib/api";
import { cn } from "@/lib/utils";
import {
  genderLabel,
  relationLabel,
  type AcademicSessionList,
  type AdmissionRequest,
  type Gender,
  type GuardianLookup,
  type GuardianRelation,
  type SaveStudentRequest,
  type SchoolClass,
  type Section,
  type StudentProfile,
} from "@/lib/types";

type GuardianCard = {
  key: number;
  relation: GuardianRelation;
  isPrimary: boolean;
  isEmergencyContact: boolean;
  fullName: string;
  cnic: string;
  phone: string;
  altPhone: string;
  email: string;
  occupation: string;
  match: GuardianLookup | null; // offered existing guardian
  linked: GuardianLookup | null; // chosen existing guardian
  dismissed: boolean;
};

const today = () => new Date().toISOString().slice(0, 10);
const digits = (v: string) => v.replace(/\D/g, "");

let nextKey = 1;
function newCard(primary: boolean, relation: GuardianRelation = "Father"): GuardianCard {
  return {
    key: nextKey++,
    relation,
    isPrimary: primary,
    isEmergencyContact: primary,
    fullName: "",
    cnic: "",
    phone: "",
    altPhone: "",
    email: "",
    occupation: "",
    match: null,
    linked: null,
    dismissed: false,
  };
}

const STEPS = ["Student", "Guardians", "Class & section"] as const;

/** One-page, three-step admission: student, guardians (with existing-guardian lookup), class and section. */
export function AdmissionPage() {
  const { user: me, can } = useAuth();
  const canSetPhoto = can("students", "Edit");
  const navigate = useNavigate();
  const [step, setStep] = useState(0);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const [student, setStudent] = useState<SaveStudentRequest>({
    firstName: "",
    lastName: "",
    gender: "Male",
    dateOfBirth: "",
    bFormNo: "",
    bloodGroup: "",
    religion: "",
    nationality: "Pakistani",
    address: "",
    phone: "",
    admissionDate: today(),
    previousSchool: "",
    notes: "",
  });
  const [photo, setPhoto] = useState<File | null>(null);
  const [photoUrl, setPhotoUrl] = useState<string | null>(null);
  const fileInput = useRef<HTMLInputElement>(null);

  const [guardians, setGuardians] = useState<GuardianCard[]>([newCard(true)]);

  const [sessions, setSessions] = useState<AcademicSessionList | null>(null);
  const [sessionId, setSessionId] = useState<number | null>(null);
  const [classes, setClasses] = useState<SchoolClass[]>([]);
  const [classId, setClassId] = useState<number | null>(null);
  const [sections, setSections] = useState<Section[]>([]);
  const [sectionId, setSectionId] = useState<number | null>(null);
  const [rollNo, setRollNo] = useState("");

  useEffect(() => {
    (async () => {
      try {
        const [s, c] = await Promise.all([
          unwrap(api.get("/academic-sessions")) as Promise<AcademicSessionList>,
          unwrap(api.get("/classes")) as Promise<SchoolClass[]>,
        ]);
        setSessions(s);
        setSessionId(s.effectiveCurrentId ?? null);
        setClasses(c.filter((x) => x.isActive));
      } catch (err) {
        toast.error(err instanceof Error ? err.message : "Failed to load academic setup");
      }
    })();
  }, []);

  useEffect(() => {
    if (sessionId == null) return;
    unwrap(api.get(`/sections?sessionId=${sessionId}`))
      .then((s) => setSections((s as Section[]).filter((x) => x.isActive && x.campusId === me?.activeCampusId)))
      .catch(() => setSections([]));
  }, [sessionId, me?.activeCampusId]);

  useEffect(() => {
    if (!photo) {
      setPhotoUrl(null);
      return;
    }
    const url = URL.createObjectURL(photo);
    setPhotoUrl(url);
    return () => URL.revokeObjectURL(url);
  }, [photo]);

  const classSections = useMemo(() => sections.filter((s) => s.classId === classId), [sections, classId]);
  const section = sections.find((s) => s.id === sectionId) ?? null;

  if (me?.activeCampusId == null) {
    return (
      <div className="panel">
        <EmptyState icon={UserRound} title="Pick a campus" description="Admissions are made into a campus. Choose one in the top bar." />
      </div>
    );
  }

  /* ---------------- validation per step ---------------- */

  function validate(i: number): string | null {
    if (i === 0) {
      if (!student.firstName.trim() || !student.lastName.trim()) return "First and last name are required.";
      if (!student.dateOfBirth) return "Date of birth is required.";
      if (student.dateOfBirth >= today()) return "Date of birth must be in the past.";
    }
    if (i === 1) {
      if (guardians.length === 0) return "Add at least one guardian.";
      if (guardians.filter((g) => g.isPrimary).length !== 1) return "Choose one primary guardian.";
      for (const g of guardians) {
        if (g.linked) continue;
        if (!g.fullName.trim()) return "Every new guardian needs a name.";
        if (digits(g.phone).length < 10) return "Every new guardian needs a phone number.";
      }
    }
    if (i === 2 && !sectionId) return "Pick a section.";
    return null;
  }

  function go(next: number) {
    if (next > step) {
      for (let i = step; i < next; i++) {
        const problem = validate(i);
        if (problem) {
          setError(problem);
          setStep(i);
          return;
        }
      }
    }
    setError(null);
    setStep(next);
  }

  /* ---------------- guardians ---------------- */

  function updateGuardian(key: number, patch: Partial<GuardianCard>) {
    setGuardians((gs) => gs.map((g) => (g.key === key ? { ...g, ...patch } : g)));
  }

  function setPrimary(key: number) {
    setGuardians((gs) => gs.map((g) => ({ ...g, isPrimary: g.key === key })));
  }

  async function lookup(card: GuardianCard) {
    if (card.linked || card.dismissed) return;
    const cnic = digits(card.cnic);
    const phone = digits(card.phone);
    if (cnic.length < 13 && phone.length < 10) return;
    try {
      const params = new URLSearchParams();
      if (cnic.length >= 13) params.set("cnic", cnic);
      else params.set("phone", phone);
      const rows = (await unwrap(api.get(`/guardians/lookup?${params}`))) as GuardianLookup[];
      updateGuardian(card.key, { match: rows[0] ?? null });
    } catch {
      /* lookup is a convenience only */
    }
  }

  /* ---------------- submit ---------------- */

  async function admit() {
    const problem = validate(0) ?? validate(1) ?? validate(2);
    if (problem) {
      setError(problem);
      return;
    }
    setSaving(true);
    setError(null);
    const clean = (v?: string | null) => (v && v.trim() ? v.trim() : null);
    const body: AdmissionRequest = {
      student: {
        ...student,
        bFormNo: clean(student.bFormNo),
        bloodGroup: clean(student.bloodGroup),
        religion: clean(student.religion),
        nationality: clean(student.nationality),
        address: clean(student.address),
        phone: clean(student.phone),
        previousSchool: clean(student.previousSchool),
        notes: clean(student.notes),
      },
      admissionNo: null,
      enrollment: { sessionId, sectionId: sectionId!, rollNo: rollNo ? Number(rollNo) : null },
      guardians: guardians.map((g) => ({
        guardianId: g.linked?.id ?? null,
        // For a linked guardian the typed CNIC/phone proves we know them (required for other campuses).
        newGuardian: {
          fullName: g.linked ? g.linked.fullName : g.fullName.trim(),
          cnic: clean(g.cnic),
          phone: g.phone.trim(),
          altPhone: clean(g.altPhone),
          email: clean(g.email),
          occupation: clean(g.occupation),
          address: null,
        },
        relation: g.relation,
        isPrimary: g.isPrimary,
        isEmergencyContact: g.isEmergencyContact,
      })),
    };
    try {
      const profile = (await unwrap(api.post("/students", body))) as StudentProfile;
      if (photo) {
        const form = new FormData();
        form.append("file", photo);
        try {
          await unwrap(api.post(`/students/${profile.student.id}/photo`, form));
        } catch (err) {
          toast.error(err instanceof Error ? `Admitted, but the photo failed: ${err.message}` : "Photo upload failed");
        }
      }
      toast.success(`Admitted · ${profile.student.admissionNo}`);
      navigate(`/admin/students/${profile.student.id}`, { replace: true });
    } catch (err) {
      setError(err instanceof Error ? err.message : "Admission failed");
    } finally {
      setSaving(false);
    }
  }

  const setS = (patch: Partial<SaveStudentRequest>) => setStudent((s) => ({ ...s, ...patch }));

  return (
    <div className="mx-auto max-w-3xl">
      <PageHeader
        eyebrow={
          <button type="button" onClick={() => navigate("/admin/students")} className="inline-flex items-center gap-1 hover:text-[var(--foreground)]">
            <ArrowLeft className="size-3.5" /> Students
          </button>
        }
        title="New admission"
        description="Student details, guardians, then the class and section they join."
      />

      {/* Stepper */}
      <ol className="mb-6 grid grid-cols-3 gap-2">
        {STEPS.map((label, i) => (
          <li key={label}>
            <button
              type="button"
              onClick={() => go(i)}
              className={cn(
                "flex w-full items-center gap-2.5 rounded-xl border px-3 py-2.5 text-left text-sm transition",
                i === step ? "border-[var(--primary)] bg-[var(--primary-soft)]" : "border-[var(--border)] bg-[var(--card)] hover:border-[var(--border-strong)]",
              )}
            >
              <span
                className={cn(
                  "inline-flex size-6 shrink-0 items-center justify-center rounded-full text-xs font-semibold",
                  i < step ? "bg-[var(--success)] text-white" : i === step ? "bg-[var(--primary)] text-[var(--primary-foreground)]" : "bg-[var(--muted)] text-[var(--muted-foreground)]",
                )}
              >
                {i < step ? <Check className="size-3.5" strokeWidth={3} /> : i + 1}
              </span>
              <span className={cn("truncate font-medium", i !== step && "text-[var(--muted-foreground)]")}>{label}</span>
            </button>
          </li>
        ))}
      </ol>

      {error ? (
        <div role="alert" className="mb-4 rounded-lg border border-[var(--danger)]/25 bg-[var(--danger-soft)] px-3 py-2.5 text-sm text-[var(--danger)]">
          {error}
        </div>
      ) : null}

      <Panel>
        {step === 0 ? (
          <div className="space-y-6">
            {canSetPhoto ? (
            <div className="flex items-center gap-4">
              <button
                type="button"
                onClick={() => fileInput.current?.click()}
                className="group relative inline-flex size-20 shrink-0 items-center justify-center overflow-hidden rounded-2xl border border-dashed border-[var(--border-strong)] bg-[var(--muted)] text-[var(--muted-foreground)] hover:border-[var(--primary)]"
              >
                {photoUrl ? <img src={photoUrl} alt="" className="size-full object-cover" /> : <Upload className="size-5" />}
              </button>
              <div className="text-sm">
                <div className="font-medium">Photo</div>
                <div className="text-xs text-[var(--muted-foreground)]">JPG, PNG or WEBP, up to 2 MB.</div>
                {photo ? (
                  <button type="button" className="mt-1 text-xs text-[var(--danger)]" onClick={() => setPhoto(null)}>
                    Remove
                  </button>
                ) : null}
              </div>
              <input
                ref={fileInput}
                type="file"
                accept="image/jpeg,image/png,image/webp"
                className="hidden"
                onChange={(e) => {
                  const f = e.target.files?.[0];
                  e.target.value = "";
                  if (!f) return;
                  if (f.size > 2_097_152 || !/^image\/(jpeg|png|webp)$/.test(f.type)) {
                    toast.error("Only JPG, PNG or WEBP images up to 2 MB.");
                    return;
                  }
                  setPhoto(f);
                }}
              />
            </div>
            ) : null}

            <FormSection title="Personal details">
              <div className="grid gap-4 sm:grid-cols-2">
                <Field label="First name" required>
                  <TextInput value={student.firstName} onChange={(e) => setS({ firstName: e.target.value })} autoFocus />
                </Field>
                <Field label="Last name" required>
                  <TextInput value={student.lastName} onChange={(e) => setS({ lastName: e.target.value })} />
                </Field>
                <Field label="Gender" required>
                  <Select value={student.gender} onChange={(e) => setS({ gender: e.target.value as Gender })}>
                    {(Object.keys(genderLabel) as Gender[]).map((g) => (
                      <option key={g} value={g}>
                        {genderLabel[g]}
                      </option>
                    ))}
                  </Select>
                </Field>
                <Field label="Date of birth" required>
                  <TextInput type="date" max={today()} value={student.dateOfBirth} onChange={(e) => setS({ dateOfBirth: e.target.value })} />
                </Field>
                <Field label="B-Form number" hint="13 digits, optional">
                  <TextInput value={student.bFormNo ?? ""} onChange={(e) => setS({ bFormNo: e.target.value })} inputMode="numeric" />
                </Field>
                <Field label="Blood group">
                  <Select value={student.bloodGroup ?? ""} onChange={(e) => setS({ bloodGroup: e.target.value })}>
                    <option value="">—</option>
                    {["A+", "A-", "B+", "B-", "AB+", "AB-", "O+", "O-"].map((b) => (
                      <option key={b} value={b}>
                        {b}
                      </option>
                    ))}
                  </Select>
                </Field>
                <Field label="Religion">
                  <TextInput value={student.religion ?? ""} onChange={(e) => setS({ religion: e.target.value })} />
                </Field>
                <Field label="Nationality">
                  <TextInput value={student.nationality ?? ""} onChange={(e) => setS({ nationality: e.target.value })} />
                </Field>
              </div>
            </FormSection>
            <FormSection title="Contact & admission">
              <Field label="Address">
                <TextInput value={student.address ?? ""} onChange={(e) => setS({ address: e.target.value })} />
              </Field>
              <div className="grid gap-4 sm:grid-cols-2">
                <Field label="Phone">
                  <TextInput value={student.phone ?? ""} onChange={(e) => setS({ phone: e.target.value })} />
                </Field>
                <Field label="Admission date" required>
                  <TextInput type="date" value={student.admissionDate} onChange={(e) => setS({ admissionDate: e.target.value })} />
                </Field>
              </div>
              <Field label="Previous school">
                <TextInput value={student.previousSchool ?? ""} onChange={(e) => setS({ previousSchool: e.target.value })} />
              </Field>
              <Field label="Notes">
                <TextArea rows={2} value={student.notes ?? ""} onChange={(e) => setS({ notes: e.target.value })} />
              </Field>
            </FormSection>
          </div>
        ) : null}

        {step === 1 ? (
          <div className="space-y-4">
            {guardians.map((g, index) => (
              <div key={g.key} className="rounded-xl border border-[var(--border)] p-4">
                <div className="mb-3 flex items-center justify-between gap-3">
                  <div className="text-sm font-semibold">Guardian {index + 1}</div>
                  <div className="flex items-center gap-3">
                    <label className="flex items-center gap-1.5 text-xs">
                      <input type="radio" name="primary" checked={g.isPrimary} onChange={() => setPrimary(g.key)} />
                      Primary
                    </label>
                    {guardians.length > 1 ? (
                      <IconButton label="Remove guardian" tone="danger" onClick={() => setGuardians((gs) => gs.filter((x) => x.key !== g.key))}>
                        <Trash2 />
                      </IconButton>
                    ) : null}
                  </div>
                </div>

                <div className="grid gap-4 sm:grid-cols-3">
                  <Field label="Relation">
                    <Select value={g.relation} onChange={(e) => updateGuardian(g.key, { relation: e.target.value as GuardianRelation })}>
                      {(Object.keys(relationLabel) as GuardianRelation[]).map((r) => (
                        <option key={r} value={r}>
                          {relationLabel[r]}
                        </option>
                      ))}
                    </Select>
                  </Field>
                  <Field label="CNIC">
                    <TextInput
                      value={g.cnic}
                      placeholder="13 digits"
                      inputMode="numeric"
                      disabled={!!g.linked}
                      onChange={(e) => updateGuardian(g.key, { cnic: e.target.value, match: null, dismissed: false })}
                      onBlur={() => lookup(g)}
                    />
                  </Field>
                  <Field label="Phone" required>
                    <TextInput
                      value={g.phone}
                      placeholder="03xx-xxxxxxx"
                      disabled={!!g.linked}
                      onChange={(e) => updateGuardian(g.key, { phone: e.target.value, match: null, dismissed: false })}
                      onBlur={() => lookup(g)}
                    />
                  </Field>
                </div>

                {g.match && !g.linked && !g.dismissed ? (
                  <div className="mt-3 flex flex-wrap items-center justify-between gap-3 rounded-lg bg-[var(--info-soft)] px-3 py-2.5 text-sm text-[var(--info)]">
                    <span className="flex items-center gap-2">
                      <Link2 className="size-4" />
                      Link existing: <strong>{g.match.fullName}</strong> ({g.match.childrenCount}{" "}
                      {g.match.childrenCount === 1 ? "child" : "children"})
                    </span>
                    <span className="flex gap-1.5">
                      <Btn type="button" size="sm" onClick={() => updateGuardian(g.key, { linked: g.match })}>
                        Link
                      </Btn>
                      <Btn type="button" size="sm" variant="ghost" onClick={() => updateGuardian(g.key, { dismissed: true })}>
                        Ignore
                      </Btn>
                    </span>
                  </div>
                ) : null}

                {g.linked ? (
                  <div className="mt-3 flex items-center justify-between rounded-lg border border-[var(--border)] bg-[var(--muted)] px-3 py-2.5 text-sm">
                    <span className="flex items-center gap-2">
                      <Check className="size-4 text-[var(--success)]" />
                      Linked to <strong>{g.linked.fullName}</strong>
                      <StatusPill>Sibling</StatusPill>
                    </span>
                    <button type="button" className="text-xs text-[var(--muted-foreground)] hover:text-[var(--foreground)]" onClick={() => updateGuardian(g.key, { linked: null })}>
                      Unlink
                    </button>
                  </div>
                ) : (
                  <div className="mt-4 grid gap-4 sm:grid-cols-2">
                    <Field label="Full name" required>
                      <TextInput value={g.fullName} onChange={(e) => updateGuardian(g.key, { fullName: e.target.value })} />
                    </Field>
                    <Field label="Occupation">
                      <TextInput value={g.occupation} onChange={(e) => updateGuardian(g.key, { occupation: e.target.value })} />
                    </Field>
                    <Field label="Alternate phone">
                      <TextInput value={g.altPhone} onChange={(e) => updateGuardian(g.key, { altPhone: e.target.value })} />
                    </Field>
                    <Field label="Email">
                      <TextInput type="email" value={g.email} onChange={(e) => updateGuardian(g.key, { email: e.target.value })} />
                    </Field>
                  </div>
                )}

                <div className="mt-3">
                  <Switch
                    checked={g.isEmergencyContact}
                    onChange={(v) => updateGuardian(g.key, { isEmergencyContact: v })}
                    label="Emergency contact"
                  />
                </div>
              </div>
            ))}
            <Btn
              type="button"
              variant="outline"
              onClick={() => setGuardians((gs) => [...gs, newCard(false, gs.some((x) => x.relation === "Father") ? "Mother" : "Father")])}
            >
              <Plus /> Add guardian
            </Btn>
          </div>
        ) : null}

        {step === 2 ? (
          <div className="space-y-6">
            <div className="grid gap-4 sm:grid-cols-2">
              <Field label="Session">
                <Select value={sessionId ?? ""} onChange={(e) => { setSessionId(Number(e.target.value)); setSectionId(null); }}>
                  {(sessions?.items ?? [])
                    .filter((s) => s.status !== "Closed")
                    .map((s) => (
                      <option key={s.id} value={s.id}>
                        {s.name}
                        {s.id === sessions?.effectiveCurrentId ? " · Current" : ""}
                      </option>
                    ))}
                </Select>
              </Field>
              <Field label="Class" required>
                <Select value={classId ?? ""} onChange={(e) => { setClassId(Number(e.target.value)); setSectionId(null); }}>
                  <option value="" disabled>
                    Choose a class
                  </option>
                  {classes.map((c) => (
                    <option key={c.id} value={c.id}>
                      {c.name}
                    </option>
                  ))}
                </Select>
              </Field>
            </div>

            {classId ? (
              classSections.length ? (
                <div>
                  <div className="mb-2 text-[13px] font-medium">Section</div>
                  <div className="grid gap-2 sm:grid-cols-3">
                    {classSections.map((s) => {
                      const full = s.capacity != null && (s.activeStudents ?? 0) >= s.capacity;
                      return (
                        <button
                          key={s.id}
                          type="button"
                          onClick={() => setSectionId(s.id)}
                          className={cn(
                            "rounded-lg border px-3 py-2.5 text-left text-sm transition",
                            sectionId === s.id ? "border-[var(--primary)] bg-[var(--primary-soft)]" : "border-[var(--border)] hover:border-[var(--border-strong)]",
                          )}
                        >
                          <div className="font-semibold">Section {s.name}</div>
                          <div className={cn("tabular text-xs", full ? "text-[var(--warning)]" : "text-[var(--muted-foreground)]")}>
                            {s.activeStudents ?? 0}
                            {s.capacity != null ? `/${s.capacity}` : ""} students{full ? " · full" : ""}
                          </div>
                        </button>
                      );
                    })}
                  </div>
                </div>
              ) : (
                <p className="text-sm text-[var(--muted-foreground)]">This class has no sections in this session. Add one on the Sections page.</p>
              )
            ) : null}

            <Field label="Roll number" hint="Leave empty to use the next free number.">
              <TextInput type="number" min={1} value={rollNo} placeholder="Next free" onChange={(e) => setRollNo(e.target.value)} className="max-w-[160px]" />
            </Field>

            <div className="rounded-xl bg-[var(--muted)] p-4 text-sm">
              <div className="mb-2 text-[11px] font-semibold tracking-wider text-[var(--muted-foreground)] uppercase">Summary</div>
              <div className="grid gap-1.5 sm:grid-cols-2">
                <div>
                  <span className="text-[var(--muted-foreground)]">Student:</span>{" "}
                  <strong>{`${student.firstName} ${student.lastName}`.trim() || "—"}</strong>
                </div>
                <div>
                  <span className="text-[var(--muted-foreground)]">Guardians:</span>{" "}
                  {guardians.map((g) => (g.linked ? g.linked.fullName : g.fullName) || "—").join(", ")}
                </div>
                <div>
                  <span className="text-[var(--muted-foreground)]">Class:</span>{" "}
                  {section ? `${section.className} – ${section.name}` : "—"}
                </div>
                <div>
                  <span className="text-[var(--muted-foreground)]">Admission no.:</span> assigned automatically
                </div>
              </div>
            </div>
          </div>
        ) : null}
      </Panel>

      <div className="mt-5 flex items-center justify-between">
        <Btn type="button" variant="outline" onClick={() => (step === 0 ? navigate("/admin/students") : go(step - 1))}>
          <ArrowLeft /> {step === 0 ? "Cancel" : "Back"}
        </Btn>
        {step < 2 ? (
          <Btn type="button" onClick={() => go(step + 1)}>
            Next <ArrowRight />
          </Btn>
        ) : (
          <Btn type="button" onClick={admit} disabled={saving}>
            {saving ? (
              <>
                <Loader2 className="animate-spin" /> Admitting…
              </>
            ) : (
              <>
                <Check /> Admit student
              </>
            )}
          </Btn>
        )}
      </div>
    </div>
  );
}
