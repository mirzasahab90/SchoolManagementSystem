import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { toast } from "sonner";
import { KpiCard, KpiGrid, PageHeader, Panel } from "@/components/sms-ui";
import { useAuth } from "@/lib/auth";
import { api, unwrap } from "@/lib/api";
import type { Dashboard } from "@/lib/types";

export function DashboardPage() {
  const { user } = useAuth();
  const navigate = useNavigate();
  const [data, setData] = useState<Dashboard | null>(null);
  const campus = user?.campuses.find((c) => c.id === user.activeCampusId);

  useEffect(() => {
    (async () => {
      try {
        setData((await unwrap(api.get("/dashboard"))) as Dashboard);
      } catch (err) {
        toast.error(err instanceof Error ? err.message : "Failed to load dashboard");
      }
    })();
  }, []);

  return (
    <div className="space-y-6">
      <PageHeader
        title="Dashboard"
        description={`HQ overview${campus ? ` · working campus: ${campus.name}` : ""}`}
      />

      {data ? (
        <KpiGrid>
          {data.kpis.map((kpi) => (
            <KpiCard
              key={kpi.id}
              label={kpi.label}
              value={kpi.value}
              hint={kpi.hint}
              tone={kpi.tone}
              onClick={kpi.href ? () => navigate(kpi.href!) : undefined}
            />
          ))}
        </KpiGrid>
      ) : (
        <div className="text-sm text-[var(--muted-foreground)]">Loading KPIs…</div>
      )}

      <Panel>
        <div className="font-display text-lg font-semibold">Signed in</div>
        <p className="mt-1 text-sm text-[var(--muted-foreground)]">
          {user?.fullName} · {user?.roles.join(", ")}
          {user?.isSuperAdmin ? " · full access" : ""}
        </p>
      </Panel>
    </div>
  );
}
