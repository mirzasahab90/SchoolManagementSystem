import type { ComponentType } from "react";
import { NavLink, Outlet, useLocation } from "react-router-dom";
import {
  Building2,
  LayoutDashboard,
  LogOut,
  Settings,
  Shield,
  Users,
} from "lucide-react";
import { useAuth } from "@/lib/auth";
import type { MenuItem } from "@/lib/types";
import { cn } from "@/lib/utils";

const iconMap: Record<string, ComponentType<{ className?: string }>> = {
  "layout-dashboard": LayoutDashboard,
  "building-2": Building2,
  users: Users,
  shield: Shield,
  settings: Settings,
};

function NavItem({ item, nested = false }: { item: MenuItem; nested?: boolean }) {
  const Icon = iconMap[item.icon ?? ""] ?? LayoutDashboard;
  const location = useLocation();
  const hasChildren = item.children?.length > 0;

  if (hasChildren) {
    const open = item.children.some((c) => c.route && location.pathname.startsWith(c.route));
    return (
      <div className="space-y-1">
        <div
          className={cn(
            "flex items-center gap-3 rounded-lg px-3 py-2 text-xs font-semibold uppercase tracking-wide",
            open ? "text-white" : "text-white/50",
          )}
        >
          <Icon className="h-4 w-4" />
          {item.name}
        </div>
        <div className="ml-2 space-y-1 border-l border-white/10 pl-2">
          {item.children.map((child) => (
            <NavItem key={child.key} item={child} nested />
          ))}
        </div>
      </div>
    );
  }

  if (!item.route) return null;

  return (
    <NavLink
      to={item.route}
      end={item.route === "/admin"}
      className={({ isActive }) =>
        cn(
          "flex items-center gap-3 rounded-lg px-3 py-2.5 text-sm transition",
          nested && "py-2",
          isActive ? "bg-white/15 text-white" : "text-white/70 hover:bg-white/10 hover:text-white",
        )
      }
    >
      <Icon className="h-4 w-4" />
      {item.name}
    </NavLink>
  );
}

export function AppShell() {
  const { user, logout, setActiveCampus } = useAuth();

  return (
    <div className="flex min-h-screen">
      <aside className="flex w-64 shrink-0 flex-col bg-[var(--sidebar)] text-[var(--sidebar-foreground)]">
        <div className="border-b border-white/10 px-5 py-5">
          <div className="font-display text-xl font-semibold tracking-tight">School Management System</div>
          <div className="mt-1 text-xs text-white/60">Multi-campus management</div>
        </div>
        <nav className="flex-1 space-y-2 p-3">
          {(user?.menu ?? []).map((item) => (
            <NavItem key={item.key} item={item} />
          ))}
        </nav>
        <div className="border-t border-white/10 p-4 text-xs text-white/60">
          <div className="font-medium text-white/90">{user?.fullName}</div>
          <div>{user?.roles?.join(", ")}</div>
        </div>
      </aside>

      <div className="flex min-w-0 flex-1 flex-col">
        <header className="sticky top-0 z-10 flex h-[68px] items-center justify-between border-b border-[var(--border)] bg-white/80 px-6 backdrop-blur">
          <div className="flex items-center gap-3">
            <label className="text-xs font-medium uppercase tracking-wide text-[var(--muted-foreground)]">
              Campus
            </label>
            <select
              className="h-10 min-w-[220px] rounded-lg border border-[var(--border)] bg-white px-3 text-sm"
              value={user?.activeCampusId ?? ""}
              onChange={(e) => setActiveCampus(Number(e.target.value))}
            >
              {(user?.campuses ?? []).map((c) => (
                <option key={c.id} value={c.id}>
                  {c.code} — {c.name}
                </option>
              ))}
            </select>
          </div>
          <button
            onClick={() => logout()}
            className="inline-flex items-center gap-2 rounded-lg border border-[var(--border)] px-3 py-2 text-sm hover:bg-[var(--muted)]"
          >
            <LogOut className="h-4 w-4" />
            Sign out
          </button>
        </header>
        <main className="mx-auto w-full max-w-[1280px] flex-1 p-6">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
