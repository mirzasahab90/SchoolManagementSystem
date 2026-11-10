import { useEffect, useState, type ComponentType } from "react";
import { useNavigate } from "react-router-dom";
import { ArrowUpRight, Briefcase, CalendarCheck, Building2, Globe2, GraduationCap, KeyRound, Plus, Shield, ShieldCheck, UserPlus, Users, FilePen, Wallet } from "lucide-react";
import { toast } from "sonner";
import { Avatar, KpiCard, KpiGrid, PageHeader, Panel, Skeleton, StatusPill, type StatusTone } from "@/components/sms-ui";
import { useAuth } from "@/lib/auth";
import { api, unwrap } from "@/lib/api";
import type { Dashboard } from "@/lib/types";
import { cn } from "@/lib/utils";

type Icon = ComponentType<{ className?: string }>;

const kpiStyle: Record<string, { icon: Icon; accent: StatusTone }> = {
  campuses: { icon: Building2, accent: "primary" },
  users: { icon: Users, accent: "info" },
  roles: { icon: Shield, accent: "warning" },
  hq: { icon: Globe2, accent: "success" },
  academics: { icon: GraduationCap, accent: "primary" },
  students: { icon: Users, accent: "info" },
  hiring: { icon: Briefcase, accent: "warning" },
  attendance: { icon: CalendarCheck, accent: "success" },
  exams: { icon: FilePen, accent: "primary" },
  fees: { icon: Wallet, accent: "success" },
};

const actionIcon: Record<string, Icon> = {
  "add-campus": Plus,
  "add-user": UserPlus,
  admit: GraduationCap,
  roles: KeyRound,
};

function greeting() {
  const hour = new Date().getHours();
  if (hour < 12) return "Good morning";
  if (hour < 17) return "Good afternoon";
  return "Good evening";
}

export function DashboardPage() {
  const { user, setActiveCampus } = useAuth();
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
  }, []);

  const scope = data?.activeCampusName ?? (user?.isHq ? "All campuses" : null);
  const today = new Date().toLocaleDateString(undefined, { weekday: "long", day: "numeric", month: "long" });

  return (
    <div className="space-y-6">
      <PageHeader
        eyebrow={today}
        title={`${greeting()}, ${user?.firstName ?? "there"}`}
        description={
          data
            ? `Here is what is happening at ${data.schoolName}${scope ? ` · ${scope}` : ""}.`
            : "Loading your school overview…"
        }
      />

      {data ? (
        <KpiGrid>
          {data.kpis.map((kpi) => {
            const style = kpiStyle[kpi.id] ?? { icon: Building2, accent: "neutral" as StatusTone };
            return (
              <KpiCard
                key={kpi.id}
                label={kpi.label}
                value={kpi.value}
                hint={kpi.hint}
                icon={style.icon}
                accent={style.accent}
                onClick={kpi.href ? () => navigate(kpi.href!) : undefined}
              />
            );
          })}
        </KpiGrid>
      ) : (
        <KpiGrid>
          {[1, 2, 3, 4].map((i) => (
            <div key={i} className="panel space-y-4 p-5">
              <div className="flex justify-between">
                <Skeleton className="h-3 w-20" />
                <Skeleton className="size-8 rounded-lg" />
              </div>
              <Skeleton className="h-7 w-16" />
              <Skeleton className="h-3 w-28" />
            </div>
          ))}
        </KpiGrid>
      )}

      <div className="grid gap-6 lg:grid-cols-3">
        <div className="space-y-6 lg:col-span-2">
          {data?.quickActions?.length ? (
            <Panel title="Quick actions" description="Common setup tasks">
              <div className="grid gap-3 sm:grid-cols-3">
                {data.quickActions.map((action) => {
                  const ActionIcon = actionIcon[action.id] ?? ArrowUpRight;
                  return (
                    <button
                      key={action.id}
                      type="button"
                      onClick={() => navigate(action.href)}
                      className="group rounded-xl border border-[var(--border)] bg-[var(--card)] p-4 text-left transition outline-none hover:border-[var(--primary)]/40 hover:shadow-[var(--shadow-sm)] focus-visible:ring-4 focus-visible:ring-[var(--ring)]"
                    >
                      <div className="flex items-center justify-between">
                        <span className="inline-flex size-9 items-center justify-center rounded-lg bg-[var(--primary-soft)] text-[var(--primary-soft-foreground)]">
                          <ActionIcon className="size-4" />
                        </span>
                        <ArrowUpRight className="size-4 text-[var(--muted-foreground)] opacity-0 transition group-hover:opacity-100" />
                      </div>
                      <div className="mt-3 text-sm font-semibold">{action.label}</div>
                      <p className="mt-1 text-xs leading-relaxed text-[var(--muted-foreground)]">{action.description}</p>
                    </button>
                  );
                })}
              </div>
            </Panel>
          ) : null}

          <Panel
            title={user?.isHq ? "Campuses" : "Your campuses"}
            description="Pick a campus to work in"
            flush
          >
            {user?.campuses?.length ? (
              <ul className="divide-y divide-[var(--border)]">
                {user.campuses.map((c) => {
                  const active = c.id === user.activeCampusId;
                  return (
                    <li key={c.id}>
                      <button
                        type="button"
                        onClick={() => (active ? undefined : void setActiveCampus(c.id))}
                        className={cn(
                          "flex w-full items-center gap-4 px-5 py-3.5 text-left transition-colors",
                          active ? "bg-[var(--primary-soft)]/50" : "hover:bg-[var(--card-hover)]",
                        )}
                      >
                        <span className="inline-flex size-9 items-center justify-center rounded-lg border border-[var(--border)] bg-[var(--card)] text-[var(--muted-foreground)]">
                          <Building2 className="size-4" />
                        </span>
                        <span className="min-w-0 flex-1">
                          <span className="block truncate text-sm font-medium">{c.name}</span>
                          <span className="block text-xs text-[var(--muted-foreground)]">{c.code}</span>
                        </span>
                        {c.isPrimary ? <StatusPill tone="primary">Primary</StatusPill> : null}
                        {active ? (
                          <StatusPill tone="success" dot>
                            Working here
                          </StatusPill>
                        ) : (
                          <span className="text-xs font-medium text-[var(--primary)]">Switch</span>
                        )}
                      </button>
                    </li>
                  );
                })}
              </ul>
            ) : (
              <div className="px-5 py-8 text-center text-sm text-[var(--muted-foreground)]">
                No campuses are assigned to you yet.
              </div>
            )}
          </Panel>
        </div>

        <Panel title="Your access">
          {user ? (
            <div className="space-y-5">
              <div className="flex items-center gap-3">
                <Avatar name={user.fullName} size="lg" />
                <div className="min-w-0">
                  <div className="truncate font-semibold">{user.fullName}</div>
                  <div className="truncate text-sm text-[var(--muted-foreground)]">{user.email}</div>
                </div>
              </div>
              <dl className="space-y-3 text-sm">
                <div className="flex items-center justify-between gap-3">
                  <dt className="text-[var(--muted-foreground)]">Access level</dt>
                  <dd>
                    {user.isSuperAdmin ? (
                      <StatusPill tone="primary">Super admin</StatusPill>
                    ) : user.isHq ? (
                      <StatusPill tone="info">Main Campus</StatusPill>
                    ) : (
                      <StatusPill>Campus</StatusPill>
                    )}
                  </dd>
                </div>
                <div className="flex items-start justify-between gap-3">
                  <dt className="text-[var(--muted-foreground)]">Roles</dt>
                  <dd className="flex flex-wrap justify-end gap-1">
                    {user.roles.map((r) => (
                      <StatusPill key={r}>{r}</StatusPill>
                    ))}
                  </dd>
                </div>
                <div className="flex items-center justify-between gap-3">
                  <dt className="text-[var(--muted-foreground)]">Campuses</dt>
                  <dd className="tabular font-medium">{user.isHq ? "All" : user.campuses.length}</dd>
                </div>
              </dl>
              <div className="flex items-start gap-2.5 rounded-lg bg-[var(--muted)] px-3 py-2.5 text-xs leading-relaxed text-[var(--muted-foreground)]">
                <ShieldCheck className="mt-0.5 size-4 shrink-0 text-[var(--success)]" />
                {user.isHq
                  ? "You can work across every campus. Pick one in the top bar to focus on it."
                  : "You can only see and change data of the campuses assigned to you."}
              </div>
            </div>
          ) : null}
        </Panel>
      </div>
    </div>
  );
}
