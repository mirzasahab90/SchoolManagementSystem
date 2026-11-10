import { useCallback, useEffect, useMemo, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { CheckCheck, ClipboardCheck, Lock, Save } from "lucide-react";
import { toast } from "sonner";
import { StudentPhoto } from "@/components/student-photo";
import { Btn, EmptyState, Field, PageHeader, Panel, Select, Skeleton, StatusPill, TextInput } from "@/components/sms-ui";
import { useAuth } from "@/lib/auth";
import { api, unwrap } from "@/lib/api";
import type { MySection, Sheet, SheetRow, StudentAttendanceStatus } from "@/lib/types";
import { errorText, formatDateTime } from "@/pages/hiring/shared";
import { StatusButtons, isoToday } from "./shared";

const OPTIONS: StudentAttendanceStatus[] = ["Present", "Absent", "Late", "Leave"];

export function MarkAttendancePage() {
  const { user: me } = useAuth();
  const [params, setParams] = useSearchParams();
  const [date, setDate] = useState(params.get("date") ?? isoToday());
  const [sections, setSections] = useState<MySection[] | null>(null);
  const [sectionId, setSectionId] = useState<number>(Number(params.get("sectionId")) || 0);
  const [sheet, setSheet] = useState<Sheet | null>(null);
  const [rows, setRows] = useState<SheetRow[]>([]);
  const [busy, setBusy] = useState(false);
  const onAllCampuses = me?.activeCampusId == null;

  useEffect(() => {
    if (onAllCampuses) return;
    unwrap(api.get("/attendance/my-sections"))
      .then((s) => {
        const list = s as MySection[];
        setSections(list);
        setSectionId((cur) => (cur && list.some((x) => x.sectionId === cur) ? cur : list[0]?.sectionId ?? 0));
      })
      .catch((err) => {
        toast.error(errorText(err, "Could not load sections"));
        setSections([]);
      });
  }, [onAllCampuses]);

  const load = useCallback(async () => {
    if (!sectionId || !date) return;
    setSheet(null);
    try {
      const s = (await unwrap(api.get(`/attendance/sheet?sectionId=${sectionId}&date=${date}`))) as Sheet;
      setSheet(s);
      setRows(s.rows);
    } catch (err) {
      toast.error(errorText(err, "Could not load the attendance"));
    }
  }, [sectionId, date]);

  useEffect(() => {
    void load();
    setParams(sectionId ? { sectionId: String(sectionId), date } : { date }, { replace: true });
  }, [load, sectionId, date, setParams]);

  const counts = useMemo(() => {
    const c = { Present: 0, Absent: 0, Late: 0, Leave: 0 } as Record<StudentAttendanceStatus, number>;
    rows.forEach((r) => (c[r.status] += 1));
    return c;
  }, [rows]);

  const setRow = (id: number, patch: Partial<SheetRow>) => setRows((list) => list.map((r) => (r.studentId === id ? { ...r, ...patch } : r)));
  const editable = !!sheet?.canEdit;

  const save = async () => {
    if (!sheet) return;
    setBusy(true);
    try {
      const s = (await unwrap(
        api.put("/attendance/sheet", {
          sectionId: sheet.sectionId,
          date: sheet.date,
          entries: rows.map((r) => ({ studentId: r.studentId, status: r.status, note: r.note?.trim() || null })),
        }),
      )) as Sheet;
      setSheet(s);
      setRows(s.rows);
      setSections((list) => list?.map((x) => (x.sectionId === s.sectionId && s.date === isoToday() ? { ...x, markedToday: true } : x)) ?? list);
      toast.success(`Attendance saved · ${s.className} – ${s.sectionName}`);
    } catch (err) {
      toast.error(errorText(err, "Could not save"));
    } finally {
      setBusy(false);
    }
  };

  if (onAllCampuses) {
    return (
      <div>
        <PageHeader title="Mark attendance" />
        <div className="panel">
          <EmptyState icon={ClipboardCheck} title="Pick a campus first" description="Attendance is marked for one campus at a time. Choose a campus from the switcher at the top." />
        </div>
      </div>
    );
  }

  return (
    <div>
      <PageHeader title="Mark attendance" description="Once a day per section. Everyone starts as Present; change only those who are not." />

      <Panel className="mb-4">
        <div className="flex flex-wrap items-end gap-3">
          <Field label="Date" className="w-44">
            <TextInput type="date" value={date} max={isoToday()} onChange={(e) => setDate(e.target.value)} />
          </Field>
          <Field label="Section" className="min-w-52 flex-1 sm:flex-none">
            <Select value={sectionId} onChange={(e) => setSectionId(Number(e.target.value))} disabled={!sections?.length}>
              {sections?.length === 0 ? <option value={0}>No sections to mark</option> : null}
              {sections?.map((s) => (
                <option key={s.sectionId} value={s.sectionId}>
                  {s.markedToday ? "✓ " : "• "}
                  {s.className} – {s.sectionName}
                  {s.isClassTeacher ? " (your class)" : ""}
                </option>
              ))}
            </Select>
          </Field>
          {sheet ? (
            <div className="flex flex-wrap gap-1.5 pb-1 text-xs">
              <StatusPill tone="success">Present {counts.Present}</StatusPill>
              <StatusPill tone="danger">Absent {counts.Absent}</StatusPill>
              <StatusPill tone="warning">Late {counts.Late}</StatusPill>
              <StatusPill tone="info">Leave {counts.Leave}</StatusPill>
            </div>
          ) : null}
        </div>
      </Panel>

      {sections?.length === 0 ? (
        <div className="panel">
          <EmptyState icon={ClipboardCheck} title="No sections for you" description="Only class teachers and admins mark attendance. Ask an admin to make you class teacher of a section." />
        </div>
      ) : !sheet ? (
        <Skeleton className="h-64 rounded-xl" />
      ) : (
        <Panel
          flush
          title={`${sheet.className} – ${sheet.sectionName}`}
          description={sheet.marked ? `Marked by ${sheet.markedBy ?? "—"} · ${formatDateTime(sheet.markedOn)}` : "Not marked yet"}
          action={
            editable ? (
              <div className="flex gap-2">
                <Btn type="button" size="sm" variant="outline" onClick={() => setRows((list) => list.map((r) => ({ ...r, status: "Present" })))}>
                  <CheckCheck /> All present
                </Btn>
                <Btn type="button" size="sm" disabled={busy || rows.length === 0} onClick={() => void save()}>
                  <Save /> {busy ? "Saving…" : sheet.marked ? "Update" : "Save"}
                </Btn>
              </div>
            ) : null
          }
        >
          {!editable ? (
            <div className="flex items-center gap-2 border-b border-[var(--border)] bg-[var(--muted)]/60 px-5 py-2.5 text-sm text-[var(--muted-foreground)]">
              <Lock className="size-4 shrink-0" /> {sheet.editLockedReason ?? sheet.reason ?? "You cannot change this attendance."}
            </div>
          ) : null}
          {rows.length === 0 ? (
            <p className="px-5 py-6 text-sm text-[var(--muted-foreground)]">No students were enrolled in this section on this date.</p>
          ) : (
            <ul className="divide-y divide-[var(--border)]">
              {rows.map((r) => (
                <li key={r.studentId} className="flex flex-wrap items-center gap-3 px-5 py-2.5">
                  <span className="tabular w-7 text-right text-xs text-[var(--muted-foreground)]">{r.rollNo}</span>
                  <StudentPhoto studentId={r.studentId} name={r.name} hasPhoto={r.hasPhoto} size="sm" />
                  <span className="min-w-0 flex-1 truncate text-sm font-medium">{r.name}</span>
                  <StatusButtons value={r.status} options={OPTIONS} disabled={!editable} name={r.name} onChange={(v) => setRow(r.studentId, { status: v })} />
                  {r.status !== "Present" || r.note ? (
                    <TextInput
                      className="h-8 w-full text-xs sm:w-48"
                      placeholder="Note (optional)"
                      maxLength={200}
                      value={r.note ?? ""}
                      disabled={!editable}
                      onChange={(e) => setRow(r.studentId, { note: e.target.value })}
                    />
                  ) : null}
                </li>
              ))}
            </ul>
          )}
        </Panel>
      )}
    </div>
  );
}
