import { useEffect, useState } from "react";
import { Panel, Skeleton, StatusPill } from "@/components/sms-ui";
import { api, unwrap } from "@/lib/api";
import { cn } from "@/lib/utils";
import { attendanceLabel, type StudentAttendance } from "@/lib/types";
import { formatDate } from "@/pages/hiring/shared";
import { pctText } from "@/pages/attendance/shared";

const monthName = (m: string) => new Date(`${m}-01T00:00:00`).toLocaleDateString(undefined, { month: "long", year: "numeric" });

/** Current-session attendance of one student. */
export function StudentAttendanceTab({ studentId }: { studentId: number }) {
  const [data, setData] = useState<StudentAttendance | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    unwrap(api.get(`/students/${studentId}/attendance`))
      .then((d) => setData(d as StudentAttendance))
      .catch((err) => setError(err instanceof Error ? err.message : "Could not load attendance"));
  }, [studentId]);

  if (error) return <p className="text-sm text-[var(--muted-foreground)]">{error}</p>;
  if (!data) return <Skeleton className="h-48 rounded-xl" />;

  return (
    <div className="grid gap-4 lg:grid-cols-3">
      <Panel title="This session">
        <div className={cn("text-4xl font-semibold tabular-nums", data.pct != null && data.pct < 75 && "text-[var(--danger)]")}>{pctText(data.pct)}</div>
        <div className="mt-1 text-xs text-[var(--muted-foreground)]">{data.workingDaysMarked} marked working days</div>
        <div className="mt-4 flex flex-wrap gap-1.5 text-xs">
          <StatusPill tone="success">Present {data.present}</StatusPill>
          <StatusPill tone="danger">Absent {data.absent}</StatusPill>
          <StatusPill tone="warning">Late {data.late}</StatusPill>
          <StatusPill tone="info">Leave {data.leave}</StatusPill>
        </div>
      </Panel>
      <Panel title="By month" flush className="lg:col-span-2">
        {data.months.length === 0 ? (
          <p className="px-5 py-6 text-sm text-[var(--muted-foreground)]">No attendance marked yet.</p>
        ) : (
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-[var(--border)] text-xs text-[var(--muted-foreground)]">
                <th className="px-5 py-2 text-left font-medium">Month</th>
                <th className="px-2 py-2 text-right font-medium">P</th>
                <th className="px-2 py-2 text-right font-medium">A</th>
                <th className="px-2 py-2 text-right font-medium">L</th>
                <th className="px-2 py-2 text-right font-medium">Lv</th>
                <th className="px-5 py-2 text-right font-medium">%</th>
              </tr>
            </thead>
            <tbody>
              {data.months.map((m) => (
                <tr key={m.month} className="border-b border-[var(--border)] last:border-0">
                  <td className="px-5 py-2">{monthName(m.month)}</td>
                  <td className="px-2 py-2 text-right tabular-nums">{m.present}</td>
                  <td className="px-2 py-2 text-right tabular-nums">{m.absent}</td>
                  <td className="px-2 py-2 text-right tabular-nums">{m.late}</td>
                  <td className="px-2 py-2 text-right tabular-nums">{m.leave}</td>
                  <td className="px-5 py-2 text-right font-semibold tabular-nums">{pctText(m.pct)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </Panel>
      <Panel title="Days not present" className="lg:col-span-3">
        {data.absences.length === 0 ? (
          <p className="text-sm text-[var(--muted-foreground)]">None.</p>
        ) : (
          <ul className="flex flex-wrap gap-2">
            {data.absences.map((a) => (
              <li key={a.date} className="rounded-lg border border-[var(--border)] px-2.5 py-1.5 text-xs">
                <span className="font-medium">{formatDate(a.date)}</span> · {attendanceLabel[a.status]}
                {a.note ? <span className="text-[var(--muted-foreground)]"> · {a.note}</span> : null}
              </li>
            ))}
          </ul>
        )}
      </Panel>
    </div>
  );
}
