import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { Pencil } from "lucide-react";
import { toast } from "sonner";
import { Modal } from "@/components/modal";
import { Btn, Field, Select } from "@/components/sms-ui";
import { useAuth } from "@/lib/auth";
import { api, unwrap } from "@/lib/api";
import type { OptionSubject, Section } from "@/lib/types";

type TeacherOption = { id: number; fullName: string; employeeNo: string };

/** Section detail: class teacher (editable with sections:Edit or staff:Edit) and the teacher of each subject. */
export function SectionTeachersDialog({
  section,
  closedSession,
  onClose,
  onEdit,
  onChanged,
}: {
  section: Section;
  closedSession: boolean;
  onClose: () => void;
  onEdit?: () => void;
  onChanged: (s: Section) => void;
}) {
  const { can } = useAuth();
  const canSet = !closedSession && (can("sections", "Edit") || can("staff", "Edit"));
  const [subjects, setSubjects] = useState<OptionSubject[] | null>(null);
  const [teachers, setTeachers] = useState<TeacherOption[]>([]);
  const [classTeacher, setClassTeacher] = useState<number | "">(section.classTeacherStaffId ?? "");
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    unwrap(api.get(`/sections/${section.id}/teachers`))
      .then((t) => setSubjects(t as OptionSubject[]))
      .catch(() => setSubjects([]));
    if (canSet) {
      unwrap(api.get(`/sections/${section.id}/class-teacher/options`))
        .then((r) => setTeachers(r as TeacherOption[]))
        .catch(() => setTeachers([]));
    }
  }, [section.id, canSet]);

  const saveClassTeacher = async () => {
    setBusy(true);
    try {
      const s = (await unwrap(api.put(`/sections/${section.id}/class-teacher`, { staffId: classTeacher === "" ? null : classTeacher }))) as Section;
      toast.success(s.classTeacherName ? `Class teacher: ${s.classTeacherName}` : "Class teacher cleared");
      onChanged(s);
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Could not save");
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal
      open
      title={`${section.className} – ${section.name}`}
      description={`Session ${section.sessionName}`}
      onClose={onClose}
      footer={
        <>
          {onEdit ? (
            <Btn type="button" variant="outline" onClick={onEdit} className="mr-auto">
              <Pencil /> Edit section
            </Btn>
          ) : null}
          <Btn type="button" variant="outline" onClick={onClose}>
            Close
          </Btn>
        </>
      }
    >
      <div className="space-y-5">
        <Field label="Class teacher">
          {canSet ? (
            <div className="flex gap-2">
              <Select value={classTeacher} onChange={(e) => setClassTeacher(e.target.value ? Number(e.target.value) : "")}>
                <option value="">No class teacher</option>
                {section.classTeacherStaffId && !teachers.some((t) => t.id === section.classTeacherStaffId) ? (
                  <option value={section.classTeacherStaffId}>{section.classTeacherName}</option>
                ) : null}
                {teachers.map((t) => (
                  <option key={t.id} value={t.id}>
                    {t.fullName} · {t.employeeNo}
                  </option>
                ))}
              </Select>
              <Btn type="button" disabled={busy || classTeacher === (section.classTeacherStaffId ?? "")} onClick={() => void saveClassTeacher()}>
                Save
              </Btn>
            </div>
          ) : (
            <div className="text-sm">{section.classTeacherName ?? <span className="text-[var(--muted-foreground)]">Not set</span>}</div>
          )}
        </Field>

        <div>
          <div className="mb-1.5 text-[13px] font-medium">Subject teachers</div>
          {subjects == null ? (
            <p className="text-sm text-[var(--muted-foreground)]">Loading…</p>
          ) : subjects.length === 0 ? (
            <p className="text-sm text-[var(--muted-foreground)]">This class has no subjects yet.</p>
          ) : (
            <ul className="divide-y divide-[var(--border)] rounded-lg border border-[var(--border)]">
              {subjects.map((s) => (
                <li key={s.subjectId} className="flex items-center justify-between gap-3 px-3 py-2 text-sm">
                  <span>{s.subjectName}</span>
                  {s.teacherStaffId ? (
                    can("staff", "View") ? (
                      <Link className="text-[var(--primary)] hover:underline" to={`/admin/staff/${s.teacherStaffId}`}>
                        {s.teacherName}
                      </Link>
                    ) : (
                      <span>{s.teacherName}</span>
                    )
                  ) : (
                    <span className="text-xs text-[var(--warning)]">No teacher</span>
                  )}
                </li>
              ))}
            </ul>
          )}
          <p className="mt-2 text-xs text-[var(--muted-foreground)]">Assign subjects from a teacher's profile (Staff → Teaching).</p>
        </div>
      </div>
    </Modal>
  );
}
