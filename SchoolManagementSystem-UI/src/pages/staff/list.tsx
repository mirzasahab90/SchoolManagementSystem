import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { IdCard, Plus } from "lucide-react";
import { toast } from "sonner";
import {
  Avatar,
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
  THead,
  Tr,
} from "@/components/sms-ui";
import { useAuth } from "@/lib/auth";
import { api, unwrap } from "@/lib/api";
import { useUrlFilters } from "@/lib/filters";
import {
  categoryLabel,
  designationLabel,
  type StaffDesignation,
  staffStatusLabel,
  type PagedResult,
  type StaffListItem,
  type StaffStatus,
} from "@/lib/types";
import { errorText, formatDate, staffTone } from "@/pages/hiring/shared";
import { StaffFormDialog } from "./staff-form";

export function StaffListPage() {
  const { user: me, can } = useAuth();
  const navigate = useNavigate();
  const onAllCampuses = me?.activeCampusId == null;
  const [adding, setAdding] = useState(false);
  const { values: f, set, clear, activeCount } = useUrlFilters({
    q: "",
    status: "",
    category: "",
    designation: "",
    sort: "",
    dir: "asc",
    page: "1",
    pageSize: "25",
  });
  const page = Number(f.page) || 1;
  const pageSize = Number(f.pageSize) || 25;
  const [result, setResult] = useState<PagedResult<StaffListItem> | null>(null);

  useEffect(() => {
    const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
    if (f.q.trim()) params.set("q", f.q.trim());
    if (f.status) params.set("status", f.status);
    if (f.category) params.set("category", f.category);
    if (f.designation) params.set("designation", f.designation);
    if (f.sort) {
      params.set("sort", f.sort);
      params.set("dir", f.dir);
    }
    let cancelled = false;
    setResult(null);
    unwrap(api.get(`/staff?${params}`))
      .then((r) => !cancelled && setResult(r as PagedResult<StaffListItem>))
      .catch((err) => {
        if (cancelled) return;
        toast.error(errorText(err, "Failed to load staff"));
        setResult({ items: [], total: 0 });
      });
    return () => {
      cancelled = true;
    };
  }, [f.q, f.status, f.category, f.designation, f.sort, f.dir, page, pageSize]);

  const total = result?.total ?? 0;

  return (
    <div>
      <PageHeader
        title="Staff"
        description={onAllCampuses ? "Staff across every campus." : "Staff of this campus: hired from vacancies or added directly."}
        actions={
          can("staff", "Add") ? (
            <Btn type="button" onClick={() => setAdding(true)} disabled={onAllCampuses} title={onAllCampuses ? "Pick a campus first" : undefined}>
              <Plus /> Add staff
            </Btn>
          ) : null
        }
      />

      <Panel flush>
        <FilterBar shown={result ? total : null} noun={["person", "people"]} activeCount={activeCount} onClear={clear}>
          <SearchField value={f.q} onChange={(q) => set({ q })} debounce={300} placeholder="Name, employee no., CNIC or phone" className="sm:max-w-sm" />
          <Select aria-label="Status" value={f.status} onChange={(e) => set({ status: e.target.value })} className="w-auto">
            <option value="">All statuses</option>
            {(Object.keys(staffStatusLabel) as StaffStatus[]).map((s) => (
              <option key={s} value={s}>
                {staffStatusLabel[s]}
              </option>
            ))}
          </Select>
          <Select aria-label="Category" value={f.category} onChange={(e) => set({ category: e.target.value })} className="w-auto">
            <option value="">All categories</option>
            <option value="Teaching">Teaching</option>
            <option value="NonTeaching">Non-teaching</option>
          </Select>
          <Select aria-label="Designation" value={f.designation} onChange={(e) => set({ designation: e.target.value })} className="w-auto">
            <option value="">All designations</option>
            {(Object.keys(designationLabel) as StaffDesignation[]).map((d) => (
              <option key={d} value={d}>
                {designationLabel[d]}
              </option>
            ))}
          </Select>
        </FilterBar>

        {result == null ? (
          <TableSkeleton cols={5} />
        ) : result.items.length === 0 ? (
          activeCount > 0 ? (
            <NoResults noun="staff" icon={IdCard} onClear={clear} />
          ) : (
            <EmptyState icon={IdCard} title="No staff yet" description="Hire a candidate from a vacancy or add someone directly." />
          )
        ) : (
          <>
            <DataTable>
              <THead
                sort={f.sort ? { key: f.sort, dir: f.dir as "asc" | "desc" } : null}
                onSort={(x) => set({ sort: x?.key ?? "", dir: x?.dir ?? "asc" })}
                columns={[
                  { label: "Employee no.", sortKey: "employeeNo" },
                  { label: "Name", sortKey: "name" },
                  { label: "Designation", sortKey: "designation" },
                  { label: "Category" },
                  { label: "Joining", sortKey: "joined" },
                  { label: "Status" },
                  ...(onAllCampuses ? [{ label: "Campus" }] : []),
                ]}
              />
              <tbody>
                {result.items.map((s) => (
                  <Tr key={s.id} onClick={() => navigate(`/admin/staff/${s.id}`)}>
                    <Td>
                      <span className="font-mono text-xs">{s.employeeNo}</span>
                    </Td>
                    <Td>
                      <div className="flex items-center gap-3">
                        <Avatar name={s.fullName} size="sm" />
                        <span className="truncate font-medium">{s.fullName}</span>
                      </div>
                    </Td>
                    <Td>{designationLabel[s.designation]}</Td>
                    <Td>{categoryLabel[s.category]}</Td>
                    <Td className="tabular">{formatDate(s.joiningDate)}</Td>
                    <Td>
                      <StatusPill tone={staffTone[s.status]} dot>
                        {staffStatusLabel[s.status]}
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
      {adding ? (
        <StaffFormDialog
          onClose={() => setAdding(false)}
          onSaved={(p) => {
            setAdding(false);
            navigate(`/admin/staff/${p.staff.id}`);
          }}
        />
      ) : null}
    </div>
  );
}
