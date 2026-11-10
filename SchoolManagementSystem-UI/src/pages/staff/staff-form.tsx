import { useEffect, useState, type ReactNode } from "react";
import { toast } from "sonner";
import { Modal } from "@/components/modal";
import { Btn, Field, Select, TextArea, TextInput } from "@/components/sms-ui";
import { api, unwrap } from "@/lib/api";
import {
  designationLabel,
  employmentTypeLabel,
  genderLabel,
  teachingDesignations,
  type EmploymentType,
  type Gender,
  type SaveStaffRequest,
  type StaffCategory,
  type StaffDesignation,
  type StaffProfile,
} from "@/lib/types";
import { errorText, todayIso } from "@/pages/hiring/shared";

function Group({ title, children }: { title: string; children: ReactNode }) {
  return (
    <fieldset className="sm:col-span-2">
      <legend className="mb-2 text-xs font-semibold tracking-wide text-[var(--muted-foreground)] uppercase">{title}</legend>
      <div className="grid gap-4 sm:grid-cols-2">{children}</div>
    </fieldset>
  );
}

function blank(): SaveStaffRequest {
  return {
    firstName: "",
    lastName: "",
    cnic: null,
    phone: "",
    email: null,
    gender: null,
    dateOfBirth: null,
    address: null,
    emergencyName: null,
    emergencyRelation: null,
    emergencyPhone: null,
    category: "Teaching",
    designation: "Teacher",
    employmentType: "Permanent",
    joiningDate: todayIso(),
    basicSalary: 0,
    qualification: null,
    experienceYears: null,
    previousEmployer: null,
    bankName: null,
    bankAccount: null,
  };
}

function fromProfile(p: StaffProfile): SaveStaffRequest {
  const s = p.staff;
  const x = p.personal;
  return {
    firstName: s.firstName,
    lastName: s.lastName,
    cnic: s.cnic ?? null,
    phone: s.phone,
    email: s.email ?? null,
    gender: x.gender ?? null,
    dateOfBirth: x.dateOfBirth ?? null,
    address: x.address ?? null,
    emergencyName: x.emergencyName ?? null,
    emergencyRelation: x.emergencyRelation ?? null,
    emergencyPhone: x.emergencyPhone ?? null,
    category: s.category,
    designation: s.designation,
    employmentType: s.employmentType,
    joiningDate: s.joiningDate,
    basicSalary: s.basicSalary,
    qualification: x.qualification ?? null,
    experienceYears: x.experienceYears ?? null,
    previousEmployer: x.previousEmployer ?? null,
    bankName: x.bankName ?? null,
    bankAccount: null, // loaded from /bank for edit
  };
}

/** Add (direct onboarding) or edit a staff member. */
export function StaffFormDialog({
  profile,
  onClose,
  onSaved,
}: {
  profile?: StaffProfile;
  onClose: () => void;
  onSaved: (p: StaffProfile) => void;
}) {
  const editing = profile != null;
  const [form, setForm] = useState<SaveStaffRequest>(() => (profile ? fromProfile(profile) : blank()));
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (!profile) return;
    unwrap(api.get(`/staff/${profile.staff.id}/bank`))
      .then((b) => {
        const bank = b as { bankName?: string | null; bankAccount?: string | null };
        setForm((f) => ({ ...f, bankName: bank.bankName ?? null, bankAccount: bank.bankAccount ?? "" }));
      })
      .catch(() => undefined); // without Edit on bank the field keeps null = unchanged
  }, [profile]);

  const set = <K extends keyof SaveStaffRequest>(key: K, value: SaveStaffRequest[K]) => setForm((f) => ({ ...f, [key]: value }));
  const text = (key: keyof SaveStaffRequest) => (e: { target: { value: string } }) => set(key, (e.target.value || null) as never);
  const cnicDigits = (form.cnic ?? "").replace(/\D/g, "");

  const save = async () => {
    setBusy(true);
    try {
      const body = { ...form, cnic: cnicDigits || null };
      const p = editing
        ? await unwrap(api.put(`/staff/${profile.staff.id}`, body))
        : await unwrap(api.post("/staff", { ...body, bankAccount: body.bankAccount || null }));
      toast.success(editing ? "Staff member updated" : `Added · ${(p as StaffProfile).staff.employeeNo}`);
      onSaved(p as StaffProfile);
    } catch (err) {
      toast.error(errorText(err, "Could not save"));
    } finally {
      setBusy(false);
    }
  };

  const valid =
    form.firstName.trim() && form.lastName.trim() && form.phone.trim() && form.basicSalary > 0 && form.joiningDate &&
    (cnicDigits.length === 0 || cnicDigits.length === 13);

  return (
    <Modal
      open
      wide
      title={editing ? `Edit ${profile.staff.fullName}` : "Add staff"}
      description={editing ? undefined : "Onboard someone directly, without a vacancy. An employee number is given on save."}
      onClose={onClose}
      footer={
        <>
          <Btn type="button" variant="outline" onClick={onClose}>
            Cancel
          </Btn>
          <Btn type="button" disabled={busy || !valid} onClick={() => void save()}>
            {busy ? "Saving…" : editing ? "Save changes" : "Add staff"}
          </Btn>
        </>
      }
    >
      <div className="grid gap-6 sm:grid-cols-2">
        <Group title="Basic">
          <Field label="First name" required>
            <TextInput value={form.firstName} maxLength={100} onChange={(e) => set("firstName", e.target.value)} autoFocus />
          </Field>
          <Field label="Last name" required>
            <TextInput value={form.lastName} maxLength={100} onChange={(e) => set("lastName", e.target.value)} />
          </Field>
          <Field label="CNIC" hint={cnicDigits.length > 0 && cnicDigits.length !== 13 ? "13 digits" : undefined}>
            <TextInput value={form.cnic ?? ""} maxLength={20} inputMode="numeric" placeholder="35202-1234567-1" onChange={text("cnic")} />
          </Field>
          <Field label="Phone" required>
            <TextInput value={form.phone} maxLength={50} inputMode="tel" onChange={(e) => set("phone", e.target.value)} />
          </Field>
          <Field label="Email">
            <TextInput type="email" value={form.email ?? ""} maxLength={256} onChange={text("email")} />
          </Field>
        </Group>

        <Group title="Personal">
          <Field label="Gender">
            <Select value={form.gender ?? ""} onChange={(e) => set("gender", (e.target.value || null) as Gender | null)}>
              <option value="">—</option>
              {(Object.keys(genderLabel) as Gender[]).map((g) => (
                <option key={g} value={g}>
                  {genderLabel[g]}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Date of birth">
            <TextInput type="date" value={form.dateOfBirth ?? ""} onChange={text("dateOfBirth")} />
          </Field>
          <Field label="Address" className="sm:col-span-2">
            <TextArea rows={2} maxLength={300} value={form.address ?? ""} onChange={text("address")} />
          </Field>
        </Group>

        <Group title="Emergency contact">
          <Field label="Name">
            <TextInput value={form.emergencyName ?? ""} maxLength={150} onChange={text("emergencyName")} />
          </Field>
          <Field label="Relation">
            <TextInput value={form.emergencyRelation ?? ""} maxLength={50} placeholder="e.g. Brother" onChange={text("emergencyRelation")} />
          </Field>
          <Field label="Phone">
            <TextInput value={form.emergencyPhone ?? ""} maxLength={50} inputMode="tel" onChange={text("emergencyPhone")} />
          </Field>
        </Group>

        <Group title="Job & salary">
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
          <Field label="Employment" required>
            <Select value={form.employmentType} onChange={(e) => set("employmentType", e.target.value as EmploymentType)}>
              {(Object.keys(employmentTypeLabel) as EmploymentType[]).map((t) => (
                <option key={t} value={t}>
                  {employmentTypeLabel[t]}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Joining date" required>
            <TextInput type="date" value={form.joiningDate} onChange={(e) => set("joiningDate", e.target.value)} />
          </Field>
          <Field label="Basic salary" required>
            <TextInput type="number" min={1} value={form.basicSalary || ""} onChange={(e) => set("basicSalary", Number(e.target.value))} />
          </Field>
        </Group>

        <Group title="Qualification">
          <Field label="Highest qualification">
            <TextInput value={form.qualification ?? ""} maxLength={200} placeholder="e.g. M.Sc Mathematics" onChange={text("qualification")} />
          </Field>
          <Field label="Experience (years)">
            <TextInput
              type="number"
              min={0}
              max={99}
              step={0.5}
              value={form.experienceYears ?? ""}
              onChange={(e) => set("experienceYears", e.target.value === "" ? null : Number(e.target.value))}
            />
          </Field>
          <Field label="Previous employer" className="sm:col-span-2">
            <TextInput value={form.previousEmployer ?? ""} maxLength={200} onChange={text("previousEmployer")} />
          </Field>
        </Group>

        <Group title="Bank">
          <Field label="Bank name">
            <TextInput value={form.bankName ?? ""} maxLength={100} onChange={text("bankName")} />
          </Field>
          <Field label="Account number / IBAN" hint="Shown masked everywhere except this form.">
            <TextInput
              value={form.bankAccount ?? ""}
              maxLength={40}
              autoComplete="off"
              onChange={(e) => set("bankAccount", e.target.value)}
            />
          </Field>
        </Group>
      </div>
    </Modal>
  );
}
