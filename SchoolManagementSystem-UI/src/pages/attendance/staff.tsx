import { useCallback, useEffect, useMemo, useState } from "react";
import { Lock, Save, UserCheck } from "lucide-react";
import { toast } from "sonner";
import {
  Btn,
  EmptyState,
  FilterBar,
  NoResults,
  PageHeader,
  Panel,
  SearchField,
  Select,
  Skeleton,
  StatusPill,
  TextInput,
} from "@/components/sms-ui";
import { useAuth } from "@/lib/auth";
import { api, unwrap } from "@/lib/api";
import { matches, useUrlFilters } from "@/lib/filters";
import { cn } from "@/lib/utils";
import {
  attendanceLabel,
  categoryLabel,
  designationLabel,
  teachingDesignations,
  type StaffAttendanceStatus,
  type StaffCategory,
  type StaffMonthly,
  type StaffSheet,
  type StaffSheetRow,
} from "@/lib/types";
import { errorText } from "@/pages/hiring/shared";
import { StatusButtons, cellTone, isoToday, pctText, thisMonth } from "./shared";

const OPTIONS: StaffAttendanceStatus[] = ["Present", "Absent", "Late", "Leave", "HalfDay"];
type Tab = "daily" | "monthly";
// Every key the tabs keep in the URL; switching tabs drops the other tab's picks.
const STAFF_DEFAULTS = { tab: "daily", date: "", month: "", q: "", category: "", status: "" };
const categoryOf = (r: StaffSheetRow): StaffCategory => (teachingDesignations.includes(r.designation) ? "Teaching" : "NonTeaching");

export function StaffAttendancePage() {
  const { can, user: me } = useAuth();
  const { values: f, set } = useUrlFilters(STAFF_DEFAULTS);
  const tab: Tab = f.tab === "monthly" ? "monthly" : "daily";
  const setTab = (t: Tab) => set({ ...STAFF_DEFAULTS, tab: t });
  if (me?.activeCampusId == null) {
    return (
      <div>
        <PageHeader title="Staff attendance" />
        <div className="panel">
          <EmptyState icon={UserCheck} title="Pick a campus first" description="Staff attendance is kept per campus." />
        </div>
      </div>
    );
  }
  return (
    <div>
      <PageHeader title="Staff attendance" description="Marked by the office each working day." />
      <div className="mb-4 flex gap-1 border-b border-[var(--border)]">
        {(
          [
            ["daily", "Daily"],
            ["monthly", "Monthly"],
          ] as const
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
      {tab === "daily" ? <Daily canEdit={can("staff-attendance", "Add")} /> : <Monthly />}
    </div>
  );
}

function Daily({ canEdit }: { canEdit: boolean }) {
  const { values: f, set, activeCount } = useUrlFilters({ date: "", q: "", category: "", status: "" });
  const date = f.date || isoToday();
  const [sheet, setSheet] = useState<StaffSheet | null>(null);
  const [rows, setRows] = useState<StaffSheetRow[]>([]);
  const [busy, setBusy] = useState(false);

  const load = useCallback(async () => {
    setSheet(null);
    try {
      const s = (await unwrap(api.get(`/staff-attendance?date=${date}`))) as StaffSheet;
      setSheet(s);
      setRows(s.rows.map((r) => ({ ...r, status: r.status ?? "Present" })));
    } catch (err) {
      toast.error(errorText(err, "Could not load"));
    }
  }, [date]);

  useEffect(() => {
    void load();
  }, [load]);

  const counts = useMemo(() => {
    const c: Record<string, number> = {};
    rows.forEach((r) => (c[r.status ?? "Present"] = (c[r.status ?? "Present"] ?? 0) + 1));
    return c;
  }, [rows]);

  // Status filters on what is saved, so a row does not vanish while its status is being changed.
  const saved = useMemo(() => new Map((sheet?.rows ?? []).map((r) => [r.staffId, r])), [sheet]);
  const shown = useMemo(
    () =>
      rows.filter((r) => {
        const s = saved.get(r.staffId);
        const savedStatus = s?.marked ? (s.status ?? "Present") : "NotMarked";
        return (
          (!f.category || categoryOf(r) === f.category) &&
          (!f.status || savedStatus === f.status) &&
          matches(f.q, r.name, r.employeeNo, designationLabel[r.designation])
        );
      }),
    [rows, saved, f.q, f.category, f.status],
  );
  // The date picks which sheet exists; it is not counted as a filter.
  const filterCount = activeCount - (f.date ? 1 : 0);
  const clearFilters = () => set({ q: "", category: "", status: "" });

  const editable = canEdit && !!sheet?.isWorkingDay;
  const setRow = (id: number, patch: Partial<StaffSheetRow>) => setRows((list) => list.map((r) => (r.staffId === id ? { ...r, ...patch } : r)));

  const save = async () => {
    setBusy(true);
    try {
      const s = (await unwrap(
        api.put("/staff-attendance", {
          date,
          entries: rows.map((r) => ({
            staffId: r.staffId,
            status: r.status ?? "Present",
            checkIn: r.checkIn ? (r.checkIn.length === 5 ? `${r.checkIn}:00` : r.checkIn) : null,
            note: r.note?.trim() || null,
          })),
        }),
      )) as StaffSheet;
      setSheet(s);
      setRows(s.rows.map((r) => ({ ...r, status: r.status ?? "Present" })));
      toast.success("Staff attendance saved");
    } catch (err) {
      toast.error(errorText(err, "Could not save"));
    } finally {
      setBusy(false);
    }
  };

  return (
    <Panel
      flush
      title="Daily attendance"
      action={
        editable && rows.length > 0 ? (
          <Btn type="button" size="sm" disabled={busy} onClick={() => void save()}>
            <Save /> {busy ? "Saving…" : "Save"}
          </Btn>
        ) : null
      }
    >
      <FilterBar
        shown={sheet ? shown.length : null}
        total={sheet ? rows.length : null}
        noun={["staff member", "staff members"]}
        activeCount={filterCount}
        onClear={clearFilters}
      >
        <SearchField value={f.q} onChange={(q) => set({ q })} debounce={200} placeholder="Name or employee no." className="sm:max-w-xs" />
        <TextInput
          type="date"
          aria-label="Date"
          value={date}
          max={isoToday()}
          onChange={(e) => set({ date: e.target.value === isoToday() ? "" : e.target.value })}
          className="w-auto"
        />
        <Select aria-label="Category" value={f.category} onChange={(e) => set({ category: e.target.value })} className="w-auto">
          <option value="">All categories</option>
          {(Object.keys(categoryLabel) as StaffCategory[]).map((c) => (
            <option key={c} value={c}>
              {categoryLabel[c]}
            </option>
          ))}
        </Select>
        <Select aria-label="Status" value={f.status} onChange={(e) => set({ status: e.target.value })} className="w-auto">
          <option value="">All statuses</option>
          {OPTIONS.map((o) => (
            <option key={o} value={o}>
              {attendanceLabel[o]}
            </option>
          ))}
          <option value="NotMarked">Not marked</option>
        </Select>
      </FilterBar>
      {Object.keys(counts).length ? (
        <div className="flex flex-wrap gap-1.5 border-b border-[var(--border)] px-5 py-2 text-xs">
          {Object.entries(counts).map(([k, n]) => (
            <StatusPill key={k}>
              {k === "HalfDay" ? "Half day" : k} {n}
            </StatusPill>
          ))}
        </div>
      ) : null}
      {!sheet ? (
        <div className="p-5">
          <Skeleton className="h-40 rounded-xl" />
        </div>
      ) : (
        <>
          {!sheet.isWorkingDay ? (
            <div className="flex items-center gap-2 border-b border-[var(--border)] bg-[var(--muted)]/60 px-5 py-2.5 text-sm text-[var(--muted-foreground)]">
              <Lock className="size-4" /> {sheet.reason}
            </div>
          ) : null}
          {rows.length === 0 ? (
            <p className="px-5 py-6 text-sm text-[var(--muted-foreground)]">No staff on this date.</p>
          ) : shown.length === 0 ? (
            <NoResults noun="staff" onClear={clearFilters} />
          ) : (
            <ul className="divide-y divide-[var(--border)]">
              {shown.map((r) => (
                <li key={r.staffId} className="flex flex-wrap items-center gap-3 px-5 py-2.5">
                  <div className="min-w-0 flex-1">
                    <div className="truncate text-sm font-medium">
                      {r.name} {!r.marked ? <span className="text-xs font-normal text-[var(--muted-foreground)]">· not marked</span> : null}
                    </div>
                    <div className="text-xs text-[var(--muted-foreground)]">
                      <span className="font-mono">{r.employeeNo}</span> · {designationLabel[r.designation]}
                    </div>
                  </div>
                  <StatusButtons value={r.status} options={OPTIONS} disabled={!editable} name={r.name} onChange={(v) => setRow(r.staffId, { status: v })} />
                  <TextInput
                    type="time"
                    aria-label={`Check-in for ${r.name}`}
                    className="h-8 w-28 text-xs"
                    value={(r.checkIn ?? "").slice(0, 5)}
                    disabled={!editable}
                    onChange={(e) => setRow(r.staffId, { checkIn: e.target.value || null })}
                  />
                </li>
              ))}
            </ul>
          )}
        </>
      )}
    </Panel>
  );
}

function Monthly() {
  const { values: f, set } = useUrlFilters({ month: "", q: "" });
  const month = f.month || thisMonth();
  const [data, setData] = useState<StaffMonthly | null>(null);
  const staff = useMemo(() => (data?.staff ?? []).filter((s) => matches(f.q, s.name, s.employeeNo)), [data, f.q]);
  useEffect(() => {
    setData(null);
    unwrap(api.get(`/staff-attendance/monthly?month=${month}`))
      .then((d) => setData(d as StaffMonthly))
      .catch((err) => toast.error(errorText(err, "Could not load")));
  }, [month]);

  return (
    <Panel flush title="Monthly sheet">
      <FilterBar
        shown={data ? staff.length : null}
        total={data?.staff.length ?? null}
        noun={["staff member", "staff members"]}
        activeCount={f.q.trim() ? 1 : 0}
        onClear={() => set({ q: "" })}
      >
        <SearchField value={f.q} onChange={(q) => set({ q })} debounce={200} placeholder="Name or employee no." className="sm:max-w-xs" />
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
      ) : data.staff.length === 0 ? (
        <p className="px-5 py-6 text-sm text-[var(--muted-foreground)]">No staff this month.</p>
      ) : staff.length === 0 ? (
        <NoResults noun="staff" onClear={() => set({ q: "" })} />
      ) : (
        <div className="overflow-x-auto">
          <table className="min-w-full border-collapse text-xs">
            <thead>
              <tr className="border-b border-[var(--border)] text-[var(--muted-foreground)]">
                <th className="sticky left-0 z-10 bg-[var(--card)] px-3 py-2 text-left font-medium">Staff</th>
                {data.days.map((d) => (
                  <th key={d.date} title={d.holidayName ?? d.kind} className={cn("w-7 px-1 py-2 text-center font-medium", d.kind !== "Working" && "bg-[var(--muted)]/70")}>
                    {Number(d.date.slice(8))}
                  </th>
                ))}
                <th className="px-3 py-2 text-right font-medium">%</th>
              </tr>
            </thead>
            <tbody>
              {staff.map((s) => (
                <tr key={s.staffId} className="border-b border-[var(--border)] last:border-0">
                  <td className="sticky left-0 z-10 max-w-44 truncate bg-[var(--card)] px-3 py-1.5 font-medium">{s.name}</td>
                  {s.cells.map((c, i) => (
                    <td key={data.days[i].date} className={cn("px-1 py-1.5 text-center", data.days[i].kind !== "Working" && "bg-[var(--muted)]/70", cellTone[c])}>
                      {c === "H" ? "½" : c}
                    </td>
                  ))}
                  <td className="px-3 py-1.5 text-right font-semibold tabular-nums">{pctText(s.pct)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </Panel>
  );
}
