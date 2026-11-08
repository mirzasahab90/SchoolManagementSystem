import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { ArrowRight } from "lucide-react";
import { toast } from "sonner";
import { KpiCard, KpiGrid, PageHeader, Panel } from "@/components/sms-ui";
import { useAuth } from "@/lib/auth";
import { api, unwrap } from "@/lib/api";
import type { Dashboard } from "@/lib/types";

export function DashboardPage() {
  const { user } = useAuth();
  const navigate = useNavigate();
  const [data, setData] = useState<Dashboard | null>(null);

  useEffect(() => {
    (async () => {
      try {
        setData((await unwrap(api.get("/dashboard"))) as Dashboard);
      } catch (err) {
        toast.error(err instanceof Error ? err.message : "Failed to load dashboard");
      }
    })();
  }, [user?.activeCampusId]);

  return (
    <div className="space-y-6">
      <PageHeader
        title={data ? `Welcome, ${user?.firstName ?? "Admin"}` : "Dashboard"}
        description={
          data
            ? `${data.schoolName}${data.activeCampusName ? ` · working on ${data.activeCampusName}` : ""}`
            : "Loading school overview…"
        }
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
        <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
          {[1, 2, 3, 4].map((i) => (
            <div key={i} className="panel h-28 animate-pulse bg-[var(--muted)]/60" />
          ))}
        </div>
      )}

      {data?.quickActions?.length ? (
        <Panel title="Quick actions" description="HQ setup shortcuts for Wave 0 foundation.">
          <div className="grid gap-3 md:grid-cols-3">
            {data.quickActions.map((action) => (
              <button
                key={action.id}
                type="button"
                onClick={() => navigate(action.href)}
                className="rounded-xl border border-[var(--border)] bg-white p-4 text-left transition hover:border-[var(--primary)]/40 hover:bg-[var(--muted)]/40"
              >
                <div className="flex items-start justify-between gap-2">
                  <div className="font-medium">{action.label}</div>
                  <ArrowRight className="mt-0.5 h-4 w-4 text-[var(--muted-foreground)]" />
                </div>
                <p className="mt-1 text-sm text-[var(--muted-foreground)]">{action.description}</p>
              </button>
            ))}
          </div>
        </Panel>
      ) : null}

      <Panel title="Session">
        <div className="text-sm">
          <div className="font-medium">{user?.fullName}</div>
          <p className="mt-1 text-[var(--muted-foreground)]">
            {user?.email} · {user?.roles.join(", ")}
            {user?.isSuperAdmin ? " · full access" : ""}
            {user?.isHq ? " · HQ" : ""}
          </p>
        </div>
      </Panel>
    </div>
  );
}
