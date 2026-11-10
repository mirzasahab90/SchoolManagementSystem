import { useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { BarChart3, Download } from "lucide-react";
import { toast } from "sonner";
import {
  Btn,
  DataTable,
  EmptyState,
  FilterBar,
  NoResults,
  PageHeader,
  Panel,
  SearchField,
  Select,
  Skeleton,
  StatusPill,
  Td,
  TextInput,
  THead,
  Tr,
} from "@/components/sms-ui";
import { useAuth } from "@/lib/auth";
import { api, downloadBlob, saveBlob, unwrap } from "@/lib/api";
import { matches, useUrlFilters } from "@/lib/filters";
import { cn } from "@/lib/utils";
import { attendanceLabel, type LowRow, type MonthlyReport, type MySection, type TodayReport } from "@/lib/types";
import { errorText, formatDate } from "@/pages/hiring/shared";
import { cellTone, pctText, thisMonth } from "./shared";

type Tab = "today" | "monthly" | "low";

const TABS: Tab[] = ["today", "monthly", "low"];
// Every key the tabs keep in the URL; switching tabs drops the other tab's picks.
const REPORT_DEFAULTS = { tab: "today", sectionId: "", month: "", threshold: "75", q: "" };

export function AttendanceReportsPage() {
  const { user: me } = useAuth();
  const { values: f, set } = useUrlFilters(REPORT_DEFAULTS);
  const tab: Tab = (TABS as string[]).includes(f.tab) ? (f.tab as Tab) : "today";
  const setTab = (t: Tab) => set({ ...REPORT_DEFAULTS, tab: t });
  const [sections, setSections] = useState<MySection[]>([]);

  useEffect(() => {
    if (me?.activeCampusId == null) return;
    unwrap(api.get("/attendance/my-sections"))
      .then((s) => setSections(s as MySection[]))
      .catch(() => setSections([]));
  }, [me?.activeCampusId]);

  if (me?.activeCampusId == null) {
    return (
      <div>
        <PageHeader title="Attendance reports" />
        <div className="panel">
          <EmptyState icon={BarChart3} title="Pick a campus first" description="Reports are per campus. Choose one from the switcher at the top." />
        </div>
      </div>
    );
  }

  return (
    <div>
      <PageHeader title="Attendance reports" />
      <div className="mb-4 flex gap-1 overflow-x-auto border-b border-[var(--border)]">
        {(
          [
            ["today", "Today"],
            ["monthly", "Monthly sheet"],
            ["low", "Low attendance"],
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
      {tab === "today" ? <TodayTab /> : null}
      {tab === "monthly" ? <MonthlyTab sections={sections} /> : null}
      {tab === "low" ? <LowTab sections={sections} /> : null}
    </div>
  );
}

function TodayTab() {
  const navigate = useNavigate();
  const [data, setData] = useState<TodayReport | null>(null);
  useEffect(() => {
    unwrap(api.get("/attendance/today"))
      .then((d) => setData(d as TodayReport))
      .catch((err) => toast.error(errorText(err, "Could not load today")));
  }, []);
  if (!data) return <Skeleton className="h-64 rounded-xl" />;
  const notMarked = data.sections.filter((s) => !s.marked).length;

  return (
    <div className="space-y-4">
      <div className="grid gap-4 sm:grid-cols-3">
        <div className="panel p-5">
          <div className="text-xs text-[var(--muted-foreground)]">Present today</div>
          <div className="mt-1 text-3xl font-semibold tabular-nums">{data.isWorkingDay ? pctText(data.presentPct) : "—"}</div>
          <div className="mt-1 text-xs text-[var(--muted-foreground)]">{data.isWorkingDay ? formatDate(data.date) : data.reason}</div>
        </div>
        <div className="panel p-5">
          <div className="text-xs text-[var(--muted-foreground)]">Sections not marked</div>
          <div className={cn("mt-1 text-3xl font-semibold tabular-nums", notMarked > 0 && data.isWorkingDay && "text-[var(--warning)]")}>
            {data.isWorkingDay ? notMarked : 0}
          </div>
          <div className="mt-1 text-xs text-[var(--muted-foreground)]">of {data.sections.length}</div>
        </div>
        <div className="panel p-5">
          <div className="text-xs text-[var(--muted-foreground)]">Absent / late / leave</div>
          <div className="mt-1 text-3xl font-semibold tabular-nums">{data.absentees.length}</div>
        </div>
      </div>

      <Panel title="Sections" flush>
        {data.sections.length === 0 ? (
          <p className="px-5 py-6 text-sm text-[var(--muted-foreground)]">No sections in the current session.</p>
        ) : (
          <div className="grid gap-2 p-4 sm:grid-cols-2 lg:grid-cols-3">
            {data.sections.map((s) => (
              <button
                key={s.sectionId}
                type="button"
                onClick={() => navigate(`/admin/attendance?sectionId=${s.sectionId}&date=${data.date}`)}
                className="rounded-lg border border-[var(--border)] bg-[var(--card)] px-3 py-2.5 text-left transition hover:border-[var(--border-strong)]"
              >
                <div className="flex items-center justify-between gap-2">
                  <span className="text-sm font-semibold">
                    {s.className} – {s.sectionName}
                  </span>
                  {s.marked ? <StatusPill tone="success">Marked</StatusPill> : <StatusPill tone="warning">Not marked</StatusPill>}
                </div>
                <div className="mt-1 text-xs text-[var(--muted-foreground)]">
                  {s.classTeacherName ?? <span className="italic">No class teacher</span>}
                  {s.marked ? ` · P ${s.present} · A ${s.absent} · L ${s.late} · Lv ${s.leave}` : ""}
                </div>
              </button>
            ))}
          </div>
        )}
      </Panel>

      <Panel title="Not present today" flush>
        {data.absentees.length === 0 ? (
          <p className="px-5 py-6 text-sm text-[var(--muted-foreground)]">Nobody yet.</p>
        ) : (
          <DataTable>
            <THead columns={[{ label: "Student" }, { label: "Class" }, { label: "Status" }]} />
            <tbody>
              {data.absentees.map((a) => (
                <Tr key={a.studentId} onClick={() => navigate(`/admin/students/${a.studentId}`)}>
                  <Td className="font-medium">{a.name}</Td>
                  <Td>
                    {a.className} – {a.sectionName}
                  </Td>
                  <Td>
                    <StatusPill tone={a.status === "Absent" ? "danger" : a.status === "Late" ? "warning" : "info"}>{attendanceLabel[a.status]}</StatusPill>
                  </Td>
                </Tr>
              ))}
            </tbody>
          </DataTable>
        )}
      </Panel>
    </div>
  );
}

function MonthlyTab({ sections }: { sections: MySection[] }) {
  const { values: f, set } = useUrlFilters({ sectionId: "", month: "", q: "" });
  // No section in the URL (or one no longer listed): show the first section. No month: this month.
  const sectionId = sections.find((s) => String(s.sectionId) === f.sectionId)?.sectionId ?? sections[0]?.sectionId ?? 0;
  const month = f.month || thisMonth();
  const [data, setData] = useState<MonthlyReport | null>(null);
  const students = useMemo(() => (data?.students ?? []).filter((s) => matches(f.q, s.name, s.rollNo)), [data, f.q]);

  useEffect(() => {
    if (!sectionId || !month) return;
    setData(null);
    unwrap(api.get(`/attendance/monthly?sectionId=${sectionId}&month=${month}`))
      .then((d) => setData(d as MonthlyReport))
      .catch((err) => toast.error(errorText(err, "Could not load the sheet")));
  }, [sectionId, month]);

  const csv = async () => {
    try {
      const { blob, fileName } = await downloadBlob(`/attendance/monthly.csv?sectionId=${sectionId}&month=${month}`);
      saveBlob(blob, fileName ?? `attendance-${month}.csv`);
    } catch (err) {
      toast.error(errorText(err, "Download failed"));
    }
  };

  return (
    <Panel
      flush
      title={data ? `${data.className} – ${data.sectionName}` : "Monthly sheet"}
      action={
        <Btn type="button" size="sm" variant="outline" disabled={!data} onClick={() => void csv()}>
          <Download /> CSV
        </Btn>
      }
    >
      <FilterBar
        shown={data ? students.length : null}
        total={data?.students.length ?? null}
        noun={["student", "students"]}
        activeCount={f.q.trim() ? 1 : 0}
        onClear={() => set({ q: "" })}
      >
        <SearchField value={f.q} onChange={(q) => set({ q })} debounce={200} placeholder="Student name or roll no." className="sm:max-w-xs" />
        <Select
          aria-label="Section"
          value={sectionId}
          onChange={(e) => set({ sectionId: Number(e.target.value) === sections[0]?.sectionId ? "" : e.target.value })}
          className="w-auto"
        >
          {sections.length === 0 ? <option value={0}>No sections</option> : null}
          {sections.map((s) => (
            <option key={s.sectionId} value={s.sectionId}>
              {s.className} – {s.sectionName}
            </option>
          ))}
        </Select>
        <TextInput
          type="month"
          aria-label="Month"
          value={month}
          max={thisMonth()}
          onChange={(e) => set({ month: e.target.value === thisMonth() ? "" : e.target.value })}
          className="w-auto"
        />
      </FilterBar>
      {!data ? (
        <div className="p-5">
          <Skeleton className="h-40 rounded-xl" />
        </div>
      ) : data.students.length === 0 ? (
        <p className="px-5 py-6 text-sm text-[var(--muted-foreground)]">No students in this section for the month.</p>
      ) : students.length === 0 ? (
        <NoResults noun="students" onClear={() => set({ q: "" })} />
      ) : (
        <div className="overflow-x-auto">
          <table className="min-w-full border-collapse text-xs">
            <thead>
              <tr className="border-b border-[var(--border)] text-[var(--muted-foreground)]">
                <th className="sticky left-0 z-10 bg-[var(--card)] px-3 py-2 text-left font-medium">Student</th>
                {data.days.map((d) => (
                  <th
                    key={d.date}
                    title={d.holidayName ?? d.kind}
                    className={cn("w-7 px-1 py-2 text-center font-medium tabular-nums", d.kind !== "Working" && "bg-[var(--muted)]/70")}
                  >
                    {Number(d.date.slice(8))}
                  </th>
                ))}
                <th className="px-2 py-2 text-right font-medium">P</th>
                <th className="px-2 py-2 text-right font-medium">A</th>
                <th className="px-2 py-2 text-right font-medium">L</th>
                <th className="px-2 py-2 text-right font-medium">Lv</th>
                <th className="px-3 py-2 text-right font-medium">%</th>
              </tr>
            </thead>
            <tbody>
              {students.map((s) => (
                <tr key={s.studentId} className="border-b border-[var(--border)] last:border-0">
                  <td className="sticky left-0 z-10 max-w-44 truncate bg-[var(--card)] px-3 py-1.5 font-medium">
                    <span className="mr-1.5 text-[var(--muted-foreground)] tabular-nums">{s.rollNo || ""}</span>
                    {s.name}
                  </td>
                  {s.cells.map((c, i) => (
                    <td
                      key={data.days[i].date}
                      className={cn("px-1 py-1.5 text-center", data.days[i].kind !== "Working" && "bg-[var(--muted)]/70", cellTone[c])}
                    >
                      {c}
                    </td>
                  ))}
                  <td className="px-2 py-1.5 text-right tabular-nums">{s.present}</td>
                  <td className="px-2 py-1.5 text-right tabular-nums">{s.absent}</td>
                  <td className="px-2 py-1.5 text-right tabular-nums">{s.late}</td>
                  <td className="px-2 py-1.5 text-right tabular-nums">{s.leave}</td>
                  <td className={cn("px-3 py-1.5 text-right font-semibold tabular-nums", s.pct != null && s.pct < 75 && "text-[var(--danger)]")}>
                    {pctText(s.pct)}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </Panel>
  );
}

function LowTab({ sections }: { sections: MySection[] }) {
  const navigate = useNavigate();
  const { values: f, set, clear, activeCount } = useUrlFilters({ threshold: "75", sectionId: "", q: "" });
  const threshold = Math.max(1, Math.min(100, Number(f.threshold) || 75));
  const sectionId = Number(f.sectionId) || 0;
  const [rows, setRows] = useState<LowRow[] | null>(null);
  const shown = useMemo(() => (rows ?? []).filter((r) => matches(f.q, r.name)), [rows, f.q]);

  useEffect(() => {
    setRows(null);
    const q = new URLSearchParams({ threshold: String(threshold) });
    if (sectionId) q.set("sectionId", String(sectionId));
    unwrap(api.get(`/attendance/low?${q}`))
      .then((r) => setRows(r as LowRow[]))
      .catch((err) => {
        toast.error(errorText(err, "Could not load"));
        setRows([]);
      });
  }, [threshold, sectionId]);

  return (
    <Panel flush title="Below the threshold" description="Current session, working days only">
      <FilterBar shown={rows ? shown.length : null} total={rows?.length ?? null} noun={["student", "students"]} activeCount={activeCount} onClear={clear}>
        <SearchField value={f.q} onChange={(q) => set({ q })} debounce={200} placeholder="Student name" className="sm:max-w-xs" />
        <Select aria-label="Section" value={sectionId} onChange={(e) => set({ sectionId: e.target.value === "0" ? "" : e.target.value })} className="w-auto">
          <option value={0}>All sections</option>
          {sections.map((s) => (
            <option key={s.sectionId} value={s.sectionId}>
              {s.className} – {s.sectionName}
            </option>
          ))}
        </Select>
        <label className="flex items-center gap-1.5 text-xs text-[var(--muted-foreground)]">
          Below
          <TextInput
            type="number"
            aria-label="Threshold %"
            min={1}
            max={100}
            value={threshold}
            onChange={(e) => set({ threshold: String(Math.max(1, Math.min(100, Number(e.target.value) || 75))) })}
            className="w-20"
          />
          %
        </label>
      </FilterBar>
      {rows == null ? (
        <div className="p-5">
          <Skeleton className="h-32 rounded-xl" />
        </div>
      ) : rows.length === 0 ? (
        <p className="px-5 py-6 text-sm text-[var(--muted-foreground)]">No one is below {threshold}%.</p>
      ) : shown.length === 0 ? (
        <NoResults noun="students" onClear={() => set({ q: "" })} />
      ) : (
        <DataTable>
          <THead columns={[{ label: "Student" }, { label: "Class" }, { label: "Days" }, { label: "Attended" }, { label: "%", align: "right" }]} />
          <tbody>
            {shown.map((r) => (
              <Tr key={r.studentId} onClick={() => navigate(`/admin/students/${r.studentId}`)}>
                <Td className="font-medium">{r.name}</Td>
                <Td>
                  {r.className} – {r.sectionName}
                </Td>
                <Td className="tabular">{r.workingDays}</Td>
                <Td className="tabular">{r.attended}</Td>
                <Td className="text-right font-semibold text-[var(--danger)] tabular-nums">{pctText(r.pct)}</Td>
              </Tr>
            ))}
          </tbody>
        </DataTable>
      )}
    </Panel>
  );
}
