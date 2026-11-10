import { useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { GraduationCap, Plus } from "lucide-react";
import { toast } from "sonner";
import { StudentPhoto } from "@/components/student-photo";
import {
  Btn,
  DataTable,
  EmptyState,
  FilterBar,
  NoResults,
  PageHeader,
  Pagination,
  Panel,
  SearchField,
  Select,
  StatusPill,
  TableSkeleton,
  Td,
  TextInput,
  THead,
  Tr,
} from "@/components/sms-ui";
import { useAuth } from "@/lib/auth";
import { api, unwrap } from "@/lib/api";
import { useUrlFilters } from "@/lib/filters";
import type { AcademicSessionList, PagedResult, SchoolClass, Section, StudentListItem } from "@/lib/types";

export function StudentsListPage() {
  const { can, user: me } = useAuth();
  const navigate = useNavigate();
  const onAllCampuses = me?.activeCampusId == null;

  const { values: f, set, clear, activeCount } = useUrlFilters({
    q: "",
    sessionId: "",
    classId: "",
    sectionId: "",
    status: "Active",
    from: "",
    to: "",
    sort: "",
    dir: "asc",
    page: "1",
    pageSize: "25",
  });
  const page = Number(f.page) || 1;
  const pageSize = Number(f.pageSize) || 25;

  const [result, setResult] = useState<PagedResult<StudentListItem> | null>(null);
  const [classes, setClasses] = useState<SchoolClass[]>([]);
  const [sections, setSections] = useState<Section[]>([]);
  const [sessions, setSessions] = useState<AcademicSessionList["items"]>([]);

  useEffect(() => {
    (async () => {
      try {
        const [c, s, ss] = await Promise.all([
          unwrap(api.get("/classes")) as Promise<SchoolClass[]>,
          unwrap(api.get("/sections")) as Promise<Section[]>,
          unwrap(api.get("/academic-sessions")) as Promise<AcademicSessionList>,
        ]);
        setClasses(c.filter((x) => x.isActive));
        setSections(s);
        setSessions(ss.items);
      } catch {
        /* filters stay empty */
      }
    })();
  }, []);

  useEffect(() => {
    const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
    if (f.q.trim()) params.set("q", f.q.trim());
    if (f.sessionId) params.set("sessionId", f.sessionId);
    if (f.classId) params.set("classId", f.classId);
    if (f.sectionId) params.set("sectionId", f.sectionId);
    if (f.status) params.set("status", f.status);
    if (f.from) params.set("admittedFrom", f.from);
    if (f.to) params.set("admittedTo", f.to);
    if (f.sort) {
      params.set("sort", f.sort);
      params.set("dir", f.dir);
    }
    let cancelled = false;
    setResult(null);
    unwrap(api.get(`/students?${params}`))
      .then((r) => !cancelled && setResult(r as PagedResult<StudentListItem>))
      .catch((err) => {
        if (cancelled) return;
        toast.error(err instanceof Error ? err.message : "Failed to load students");
        setResult({ items: [], total: 0 });
      });
    return () => {
      cancelled = true;
    };
  }, [f.q, f.sessionId, f.classId, f.sectionId, f.status, f.from, f.to, f.sort, f.dir, page, pageSize]);

  const classSections = useMemo(
    () =>
      sections.filter(
        (s) =>
          String(s.classId) === f.classId &&
          (!f.sessionId || String(s.academicSessionId) === f.sessionId) &&
          (onAllCampuses || s.campusId === me?.activeCampusId),
      ),
    [sections, f.classId, f.sessionId, onAllCampuses, me?.activeCampusId],
  );

  const canAdmit = can("students", "Add");
  const total = result?.total ?? 0;

  return (
    <div>
      <PageHeader
        title="Students"
        description={onAllCampuses ? "Students across every campus." : "Students of this campus."}
        actions={
          canAdmit ? (
            <Btn
              type="button"
              onClick={() => navigate("/admin/students/new")}
              disabled={onAllCampuses}
              title={onAllCampuses ? "Pick a campus first" : undefined}
            >
              <Plus />
              New admission
            </Btn>
          ) : null
        }
      />

      <Panel flush>
        <FilterBar shown={result ? total : null} noun={["student", "students"]} activeCount={activeCount} onClear={clear}>
          <SearchField
            value={f.q}
            onChange={(q) => set({ q })}
            debounce={300}
            placeholder="Name, admission no., B-Form or guardian phone"
            className="sm:max-w-sm"
          />
          <Select aria-label="Session" value={f.sessionId} onChange={(e) => set({ sessionId: e.target.value, sectionId: "" })} className="w-auto">
            <option value="">All sessions</option>
            {sessions.map((s) => (
              <option key={s.id} value={s.id}>
                {s.name}
                {s.isCurrent ? " · Current" : ""}
              </option>
            ))}
          </Select>
          <Select aria-label="Class" value={f.classId} onChange={(e) => set({ classId: e.target.value, sectionId: "" })} className="w-auto">
            <option value="">All classes</option>
            {classes.map((c) => (
              <option key={c.id} value={c.id}>
                {c.name}
              </option>
            ))}
          </Select>
          <Select
            aria-label="Section"
            value={f.sectionId}
            onChange={(e) => set({ sectionId: e.target.value })}
            disabled={!f.classId}
            title={!f.classId ? "Pick a class first" : undefined}
            className="w-auto"
          >
            <option value="">All sections</option>
            {classSections.map((s) => (
              <option key={s.id} value={s.id}>
                {s.name}
                {onAllCampuses ? ` · ${s.campusCode}` : ""}
              </option>
            ))}
          </Select>
          <Select aria-label="Status" value={f.status} onChange={(e) => set({ status: e.target.value })} className="w-auto">
            <option value="Active">Active</option>
            <option value="Left">Left</option>
            <option value="">All statuses</option>
          </Select>
          <div className="flex items-center gap-1.5 text-xs text-[var(--muted-foreground)]">
            <span>Admitted</span>
            <TextInput type="date" aria-label="Admitted from" value={f.from} max={f.to || undefined} onChange={(e) => set({ from: e.target.value })} className="w-auto" />
            <span>to</span>
            <TextInput type="date" aria-label="Admitted to" value={f.to} min={f.from || undefined} onChange={(e) => set({ to: e.target.value })} className="w-auto" />
          </div>
        </FilterBar>

        {result == null ? (
          <TableSkeleton cols={5} />
        ) : result.items.length === 0 ? (
          activeCount > 0 ? (
            <NoResults noun="students" icon={GraduationCap} onClear={clear} />
          ) : (
            <EmptyState
              icon={GraduationCap}
              title="No students yet"
              description="Admit your first student to get started."
              action={
                canAdmit && !onAllCampuses ? (
                  <Btn type="button" onClick={() => navigate("/admin/students/new")}>
                    <Plus />
                    New admission
                  </Btn>
                ) : null
              }
            />
          )
        ) : (
          <>
            <DataTable>
              <THead
                sort={f.sort ? { key: f.sort, dir: f.dir as "asc" | "desc" } : null}
                onSort={(s) => set({ sort: s?.key ?? "", dir: s?.dir ?? "asc" })}
                columns={[
                  { label: "Student", sortKey: "name" },
                  { label: "Class", sortKey: "class" },
                  { label: "Roll", sortKey: "roll" },
                  { label: "Guardian" },
                  { label: "Status" },
                  ...(onAllCampuses ? [{ label: "Campus" }] : []),
                ]}
              />
              <tbody>
                {result.items.map((s) => (
                  <Tr key={s.id} onClick={() => navigate(`/admin/students/${s.id}`)}>
                    <Td>
                      <div className="flex items-center gap-3">
                        <StudentPhoto studentId={s.id} name={s.fullName} hasPhoto={s.hasPhoto} />
                        <div className="min-w-0">
                          <div className="truncate font-medium">{s.fullName}</div>
                          <div className="font-mono text-xs text-[var(--muted-foreground)]">{s.admissionNo}</div>
                        </div>
                      </div>
                    </Td>
                    <Td>{s.className ? `${s.className} – ${s.sectionName}` : <span className="text-[var(--muted-foreground)]">—</span>}</Td>
                    <Td className="tabular">{s.rollNo ?? "—"}</Td>
                    <Td>
                      {s.guardianName ? (
                        <div className="min-w-0 leading-tight">
                          <div className="truncate">{s.guardianName}</div>
                          <div className="text-xs text-[var(--muted-foreground)]">{s.guardianPhone}</div>
                        </div>
                      ) : (
                        "—"
                      )}
                    </Td>
                    <Td>
                      <StatusPill tone={s.status === "Active" ? "success" : "neutral"} dot>
                        {s.status}
                      </StatusPill>
                    </Td>
                    {onAllCampuses ? (
                      <Td>
                        <span className="rounded-md border border-[var(--border)] px-1.5 py-0.5 font-mono text-[11px]">{s.campusCode}</span>
                      </Td>
                    ) : null}
                  </Tr>
                ))}
              </tbody>
            </DataTable>
            <Pagination
              page={page}
              pageSize={pageSize}
              total={total}
              onPage={(p) => set({ page: String(p) })}
              onPageSize={(n) => set({ pageSize: String(n) })}
            />
          </>
        )}
      </Panel>
    </div>
  );
}
