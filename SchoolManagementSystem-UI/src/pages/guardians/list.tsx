import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { HeartHandshake } from "lucide-react";
import { toast } from "sonner";
import {
  Avatar,
  DataTable,
  EmptyState,
  FilterBar,
  NoResults,
  PageHeader,
  Pagination,
  Panel,
  SearchField,
  TableSkeleton,
  Td,
  THead,
  Tr,
} from "@/components/sms-ui";
import { api, unwrap } from "@/lib/api";
import { useUrlFilters } from "@/lib/filters";
import type { GuardianListItem, PagedResult } from "@/lib/types";

/** Shows only the last digit of a CNIC, e.g. •••••-•••••••-7. */
export function maskCnic(cnic?: string | null) {
  if (!cnic) return "—";
  return `•••••-•••••••-${cnic.slice(-1)}`;
}

export function GuardiansListPage() {
  const navigate = useNavigate();
  const { values: f, set, clear, activeCount } = useUrlFilters({ q: "", page: "1", pageSize: "25" });
  const page = Number(f.page) || 1;
  const pageSize = Number(f.pageSize) || 25;
  const q = f.q.trim();
  const [result, setResult] = useState<PagedResult<GuardianListItem> | null>(null);

  useEffect(() => {
    const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
    if (q) params.set("q", q);
    let cancelled = false;
    setResult(null);
    unwrap(api.get(`/guardians?${params}`))
      .then((r) => !cancelled && setResult(r as PagedResult<GuardianListItem>))
      .catch((err) => {
        if (cancelled) return;
        toast.error(err instanceof Error ? err.message : "Failed to load guardians");
        setResult({ items: [], total: 0 });
      });
    return () => {
      cancelled = true;
    };
  }, [q, page, pageSize]);

  const total = result?.total ?? 0;

  return (
    <div>
      <PageHeader title="Guardians" description="Parents and guardians of your students. One guardian can have children on several campuses." />
      <Panel flush>
        <FilterBar shown={result ? total : null} noun={["guardian", "guardians"]} activeCount={activeCount} onClear={clear}>
          <SearchField value={f.q} onChange={(v) => set({ q: v })} debounce={300} placeholder="Name, phone or CNIC" className="sm:max-w-sm" />
        </FilterBar>
        {result == null ? (
          <TableSkeleton cols={4} />
        ) : result.items.length === 0 ? (
          activeCount > 0 ? (
            <NoResults noun="guardians" icon={HeartHandshake} onClear={clear} />
          ) : (
            <EmptyState icon={HeartHandshake} title="No guardians yet" description="Guardians appear here when students are admitted." />
          )
        ) : (
          <>
            <DataTable>
              <THead columns={[{ label: "Guardian" }, { label: "CNIC" }, { label: "Phone" }, { label: "Children", align: "right" }]} />
              <tbody>
                {result.items.map((g) => (
                  <Tr key={g.id} onClick={() => navigate(`/admin/guardians/${g.id}`)}>
                    <Td>
                      <div className="flex items-center gap-3">
                        <Avatar name={g.fullName} />
                        <span className="font-medium">{g.fullName}</span>
                      </div>
                    </Td>
                    <Td className="font-mono text-xs text-[var(--muted-foreground)]">{maskCnic(g.cnic)}</Td>
                    <Td>{g.phone}</Td>
                    <Td align="right" className="tabular">
                      {g.childrenCount}
                    </Td>
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
