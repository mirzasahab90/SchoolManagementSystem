import { useEffect, useMemo, useRef, useState, type ComponentType, type ReactNode, type RefObject } from "react";
import { Link, NavLink, Outlet, useLocation } from "react-router-dom";
import {
  Building2,
  Check,
  ChevronRight,
  ChevronsUpDown,
  GraduationCap,
  Globe2,
  LayoutDashboard,
  LogOut,
  Menu,
  Moon,
  PanelLeftClose,
  PanelLeftOpen,
  Search,
  Settings,
  Shield,
  Sun,
  Users,
  X,
} from "lucide-react";
import { Avatar, StatusPill } from "@/components/sms-ui";
import { useAuth } from "@/lib/auth";
import { useTheme } from "@/lib/theme";
import type { MenuItem } from "@/lib/types";
import { cn } from "@/lib/utils";

const iconMap: Record<string, ComponentType<{ className?: string }>> = {
  "layout-dashboard": LayoutDashboard,
  "building-2": Building2,
  users: Users,
  shield: Shield,
  settings: Settings,
};

const COLLAPSE_KEY = "schoolms.sidebarCollapsed";

function readCollapsed() {
  try {
    return localStorage.getItem(COLLAPSE_KEY) === "1";
  } catch {
    return false;
  }
}

function useClickOutside(ref: RefObject<HTMLElement | null>, onOutside: () => void, active: boolean) {
  useEffect(() => {
    if (!active) return;
    const onDown = (e: MouseEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node)) onOutside();
    };
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") onOutside();
    };
    document.addEventListener("mousedown", onDown);
    document.addEventListener("keydown", onKey);
    return () => {
      document.removeEventListener("mousedown", onDown);
      document.removeEventListener("keydown", onKey);
    };
  }, [ref, onOutside, active]);
}

/* ------------------------------------------------------------------ */
/* Sidebar                                                             */
/* ------------------------------------------------------------------ */

function NavItem({ item, collapsed, onNavigate }: { item: MenuItem; collapsed: boolean; onNavigate?: () => void }) {
  const Icon = iconMap[item.icon ?? ""] ?? LayoutDashboard;
  if (!item.route) return null;

  return (
    <NavLink
      to={item.route}
      end={item.route === "/admin"}
      title={collapsed ? item.name : undefined}
      onClick={onNavigate}
      className={({ isActive }) =>
        cn(
          "group relative flex h-9 items-center gap-3 rounded-lg px-2.5 text-sm font-medium transition-colors",
          collapsed && "justify-center px-0",
          isActive
            ? "bg-[var(--primary-soft)] text-[var(--primary-soft-foreground)]"
            : "text-[var(--muted-foreground)] hover:bg-[var(--muted)] hover:text-[var(--foreground)]",
        )
      }
    >
      <Icon className="size-[18px] shrink-0" />
      {!collapsed ? <span className="truncate">{item.name}</span> : null}
    </NavLink>
  );
}

function SidebarNav({ menu, collapsed, onNavigate }: { menu: MenuItem[]; collapsed: boolean; onNavigate?: () => void }) {
  const flat = menu.filter((m) => !m.children?.length);
  const groups = menu.filter((m) => m.children?.length);

  return (
    <nav className="flex-1 space-y-6 overflow-y-auto px-3 py-4">
      <div className="space-y-0.5">
        {flat.map((item) => (
          <NavItem key={item.key} item={item} collapsed={collapsed} onNavigate={onNavigate} />
        ))}
      </div>
      {groups.map((group) => (
        <div key={group.key} className="space-y-0.5">
          {collapsed ? (
            <div className="mx-auto mb-2 h-px w-6 bg-[var(--border)]" />
          ) : (
            <div className="mb-1.5 px-2.5 text-[11px] font-semibold tracking-wider text-[var(--muted-foreground)]/80 uppercase">
              {group.name}
            </div>
          )}
          {group.children.map((child) => (
            <NavItem key={child.key} item={child} collapsed={collapsed} onNavigate={onNavigate} />
          ))}
        </div>
      ))}
    </nav>
  );
}

function Brand({ collapsed }: { collapsed: boolean }) {
  const { user } = useAuth();
  const name = user?.schoolShortName || user?.schoolName || "School";
  return (
    <Link to="/admin" className={cn("flex h-16 items-center gap-3 px-4", collapsed && "justify-center px-0")}>
      <span className="inline-flex size-9 shrink-0 items-center justify-center rounded-xl bg-[var(--primary)] text-[var(--primary-foreground)] shadow-[var(--shadow-sm)]">
        <GraduationCap className="size-5" />
      </span>
      {!collapsed ? (
        <span className="min-w-0 leading-tight">
          <span className="block truncate text-sm font-semibold">{name}</span>
          <span className="block truncate text-xs text-[var(--muted-foreground)]">
            {user?.isHq ? "Head office" : "Campus workspace"}
          </span>
        </span>
      ) : null}
    </Link>
  );
}

function SidebarFooter({ collapsed }: { collapsed: boolean }) {
  const { user } = useAuth();
  if (!user) return null;
  return (
    <div className={cn("border-t border-[var(--border)] p-3", collapsed && "flex justify-center")}>
      {collapsed ? (
        <Avatar name={user.fullName} size="sm" />
      ) : (
        <div className="flex items-center gap-3 rounded-lg px-1.5 py-1">
          <Avatar name={user.fullName} size="sm" />
          <div className="min-w-0 flex-1 leading-tight">
            <div className="truncate text-sm font-medium">{user.fullName}</div>
            <div className="truncate text-xs text-[var(--muted-foreground)]">{user.roles.join(", ")}</div>
          </div>
        </div>
      )}
    </div>
  );
}

/* ------------------------------------------------------------------ */
/* Top bar pieces                                                      */
/* ------------------------------------------------------------------ */

function CampusSwitcher() {
  const { user, setActiveCampus } = useAuth();
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState("");
  const [busy, setBusy] = useState(false);
  const ref = useRef<HTMLDivElement>(null);
  useClickOutside(ref, () => setOpen(false), open);

  const campuses = user?.campuses ?? [];
  const active = campuses.find((c) => c.id === user?.activeCampusId) ?? null;
  const isHq = !!user?.isHq;
  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    return q ? campuses.filter((c) => `${c.code} ${c.name}`.toLowerCase().includes(q)) : campuses;
  }, [campuses, query]);

  if (!user) return null;
  if (!isHq && campuses.length === 0) {
    return <StatusPill tone="warning">No campus assigned</StatusPill>;
  }

  async function choose(id: number | null) {
    setOpen(false);
    setQuery("");
    if (id === (user?.activeCampusId ?? null)) return;
    setBusy(true);
    try {
      await setActiveCampus(id);
    } finally {
      setBusy(false);
    }
  }

  const canSwitch = isHq || campuses.length > 1;

  return (
    <div ref={ref} className="relative">
      <button
        type="button"
        disabled={!canSwitch || busy}
        onClick={() => setOpen((v) => !v)}
        aria-haspopup="listbox"
        aria-expanded={open}
        className={cn(
          "flex h-9 max-w-[260px] items-center gap-2.5 rounded-lg border border-[var(--border)] bg-[var(--card)] pr-2 pl-2.5 text-sm shadow-[var(--shadow-xs)] transition outline-none hover:border-[var(--border-strong)] focus-visible:ring-4 focus-visible:ring-[var(--ring)]",
          !canSwitch && "cursor-default hover:border-[var(--border)]",
        )}
      >
        <span
          className={cn(
            "inline-flex size-5 shrink-0 items-center justify-center rounded-md",
            active ? "bg-[var(--primary-soft)] text-[var(--primary-soft-foreground)]" : "bg-[var(--info-soft)] text-[var(--info)]",
          )}
        >
          {active ? <Building2 className="size-3.5" /> : <Globe2 className="size-3.5" />}
        </span>
        <span className="min-w-0 truncate font-medium">{active ? active.name : "All campuses"}</span>
        {active ? <span className="hidden text-xs text-[var(--muted-foreground)] sm:inline">{active.code}</span> : null}
        {canSwitch ? <ChevronsUpDown className="ml-auto size-3.5 shrink-0 text-[var(--muted-foreground)]" /> : null}
      </button>

      {open ? (
        <div className="animate-pop absolute top-11 right-0 z-40 w-72 overflow-hidden rounded-xl border border-[var(--border)] bg-[var(--card)] shadow-[var(--shadow-lg)] sm:right-auto sm:left-0">
          {campuses.length > 5 ? (
            <div className="relative border-b border-[var(--border)]">
              <Search className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-[var(--muted-foreground)]" />
              <input
                autoFocus
                value={query}
                onChange={(e) => setQuery(e.target.value)}
                placeholder="Find a campus…"
                className="h-10 w-full bg-transparent pr-3 pl-9 text-sm outline-none"
              />
            </div>
          ) : null}
          <div role="listbox" className="max-h-80 overflow-y-auto p-1.5">
            {isHq ? (
              <>
                <SwitcherOption
                  selected={!active}
                  onClick={() => choose(null)}
                  icon={<Globe2 className="size-4" />}
                  title="All campuses"
                  subtitle="Consolidated HQ view"
                />
                <div className="my-1.5 h-px bg-[var(--border)]" />
              </>
            ) : null}
            <div className="px-2 pt-1 pb-1.5 text-[11px] font-semibold tracking-wider text-[var(--muted-foreground)] uppercase">
              Campuses
            </div>
            {filtered.map((c) => (
              <SwitcherOption
                key={c.id}
                selected={active?.id === c.id}
                onClick={() => choose(c.id)}
                icon={<Building2 className="size-4" />}
                title={c.name}
                subtitle={c.code}
                badge={c.isPrimary ? <StatusPill tone="primary">Primary</StatusPill> : null}
              />
            ))}
            {filtered.length === 0 ? (
              <div className="px-2 py-6 text-center text-sm text-[var(--muted-foreground)]">No campus found</div>
            ) : null}
          </div>
        </div>
      ) : null}
    </div>
  );
}

function SwitcherOption({
  selected,
  onClick,
  icon,
  title,
  subtitle,
  badge,
}: {
  selected: boolean;
  onClick: () => void;
  icon: ReactNode;
  title: string;
  subtitle?: string;
  badge?: ReactNode;
}) {
  return (
    <button
      type="button"
      role="option"
      aria-selected={selected}
      onClick={onClick}
      className={cn(
        "flex w-full items-center gap-3 rounded-lg px-2 py-2 text-left text-sm transition-colors",
        selected ? "bg-[var(--muted)]" : "hover:bg-[var(--muted)]",
      )}
    >
      <span className="inline-flex size-8 shrink-0 items-center justify-center rounded-lg border border-[var(--border)] text-[var(--muted-foreground)]">
        {icon}
      </span>
      <span className="min-w-0 flex-1">
        <span className="flex items-center gap-2">
          <span className="truncate font-medium">{title}</span>
          {badge}
        </span>
        {subtitle ? <span className="block truncate text-xs text-[var(--muted-foreground)]">{subtitle}</span> : null}
      </span>
      {selected ? <Check className="size-4 shrink-0 text-[var(--primary)]" /> : null}
    </button>
  );
}

function UserMenu() {
  const { user, logout } = useAuth();
  const { theme, toggle } = useTheme();
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);
  useClickOutside(ref, () => setOpen(false), open);
  if (!user) return null;

  return (
    <div ref={ref} className="relative">
      <button
        type="button"
        onClick={() => setOpen((v) => !v)}
        aria-haspopup="menu"
        aria-expanded={open}
        className="flex items-center gap-2 rounded-full p-0.5 outline-none focus-visible:ring-4 focus-visible:ring-[var(--ring)]"
      >
        <Avatar name={user.fullName} size="sm" />
      </button>
      {open ? (
        <div
          role="menu"
          className="animate-pop absolute top-11 right-0 z-40 w-64 overflow-hidden rounded-xl border border-[var(--border)] bg-[var(--card)] shadow-[var(--shadow-lg)]"
        >
          <div className="flex items-center gap-3 border-b border-[var(--border)] px-4 py-3.5">
            <Avatar name={user.fullName} />
            <div className="min-w-0 leading-tight">
              <div className="truncate text-sm font-semibold">{user.fullName}</div>
              <div className="truncate text-xs text-[var(--muted-foreground)]">{user.email}</div>
            </div>
          </div>
          <div className="flex flex-wrap gap-1.5 border-b border-[var(--border)] px-4 py-3">
            {user.isSuperAdmin ? <StatusPill tone="primary">Super admin</StatusPill> : null}
            {user.isHq && !user.isSuperAdmin ? <StatusPill tone="info">HQ access</StatusPill> : null}
            {user.roles
              .filter((r) => r !== "SuperAdmin")
              .map((r) => (
                <StatusPill key={r}>{r}</StatusPill>
              ))}
          </div>
          <div className="p-1.5">
            <MenuRow onClick={toggle} icon={theme === "dark" ? <Sun /> : <Moon />}>
              {theme === "dark" ? "Light mode" : "Dark mode"}
            </MenuRow>
            <MenuRow
              onClick={() => {
                setOpen(false);
                void logout();
              }}
              icon={<LogOut />}
              danger
            >
              Sign out
            </MenuRow>
          </div>
        </div>
      ) : null}
    </div>
  );
}

function MenuRow({
  children,
  icon,
  onClick,
  danger,
}: {
  children: ReactNode;
  icon: ReactNode;
  onClick: () => void;
  danger?: boolean;
}) {
  return (
    <button
      type="button"
      role="menuitem"
      onClick={onClick}
      className={cn(
        "flex w-full items-center gap-2.5 rounded-lg px-2.5 py-2 text-sm transition-colors hover:bg-[var(--muted)] [&_svg]:size-4",
        danger ? "text-[var(--danger)]" : "text-[var(--foreground)]",
      )}
    >
      {icon}
      {children}
    </button>
  );
}

function Breadcrumbs({ menu }: { menu: MenuItem[] }) {
  const { pathname } = useLocation();
  const trail = useMemo(() => {
    for (const item of menu) {
      if (item.route && (pathname === item.route || (item.route !== "/admin" && pathname.startsWith(item.route)))) {
        return [item.name];
      }
      for (const child of item.children ?? []) {
        if (child.route && pathname.startsWith(child.route)) return [item.name, child.name];
      }
    }
    return ["Dashboard"];
  }, [menu, pathname]);

  return (
    <div className="hidden min-w-0 items-center gap-1.5 text-sm md:flex">
      {trail.map((part, i) => (
        <span key={part} className="flex items-center gap-1.5">
          {i > 0 ? <ChevronRight className="size-3.5 text-[var(--muted-foreground)]" /> : null}
          <span className={cn(i === trail.length - 1 ? "font-medium" : "text-[var(--muted-foreground)]")}>{part}</span>
        </span>
      ))}
    </div>
  );
}

/* ------------------------------------------------------------------ */
/* Shell                                                               */
/* ------------------------------------------------------------------ */

export function AppShell() {
  const { user } = useAuth();
  const { theme, toggle } = useTheme();
  const [collapsed, setCollapsed] = useState(readCollapsed);
  const [mobileOpen, setMobileOpen] = useState(false);
  const location = useLocation();
  const menu = user?.menu ?? [];

  useEffect(() => setMobileOpen(false), [location.pathname]);

  function toggleCollapsed() {
    setCollapsed((v) => {
      try {
        localStorage.setItem(COLLAPSE_KEY, v ? "0" : "1");
      } catch {
        /* ignore */
      }
      return !v;
    });
  }

  return (
    <div className="flex min-h-screen">
      {/* Desktop sidebar */}
      <aside
        className={cn(
          "sticky top-0 hidden h-screen shrink-0 flex-col border-r border-[var(--border)] bg-[var(--sidebar)] transition-[width] duration-200 lg:flex",
          collapsed ? "w-[68px]" : "w-64",
        )}
      >
        <Brand collapsed={collapsed} />
        <SidebarNav menu={menu} collapsed={collapsed} />
        <div className={cn("px-3 pb-2", collapsed && "flex justify-center")}>
          <button
            type="button"
            onClick={toggleCollapsed}
            title={collapsed ? "Expand sidebar" : "Collapse sidebar"}
            className={cn(
              "flex h-8 items-center gap-2 rounded-lg px-2.5 text-xs text-[var(--muted-foreground)] transition-colors hover:bg-[var(--muted)] hover:text-[var(--foreground)]",
              collapsed && "w-9 justify-center px-0",
            )}
          >
            {collapsed ? <PanelLeftOpen className="size-4" /> : <PanelLeftClose className="size-4" />}
            {!collapsed ? "Collapse" : null}
          </button>
        </div>
        <SidebarFooter collapsed={collapsed} />
      </aside>

      {/* Mobile drawer */}
      {mobileOpen ? (
        <div className="fixed inset-0 z-50 lg:hidden">
          <button
            type="button"
            aria-label="Close menu"
            className="animate-fade absolute inset-0 bg-[var(--overlay)]"
            onClick={() => setMobileOpen(false)}
          />
          <aside className="animate-pop absolute inset-y-0 left-0 flex w-72 flex-col border-r border-[var(--border)] bg-[var(--sidebar)] shadow-[var(--shadow-lg)]">
            <div className="flex items-center justify-between pr-3">
              <Brand collapsed={false} />
              <button
                type="button"
                aria-label="Close menu"
                onClick={() => setMobileOpen(false)}
                className="inline-flex size-8 items-center justify-center rounded-lg text-[var(--muted-foreground)] hover:bg-[var(--muted)]"
              >
                <X className="size-4" />
              </button>
            </div>
            <SidebarNav menu={menu} collapsed={false} onNavigate={() => setMobileOpen(false)} />
            <SidebarFooter collapsed={false} />
          </aside>
        </div>
      ) : null}

      <div className="flex min-w-0 flex-1 flex-col">
        <header className="sticky top-0 z-30 flex h-16 items-center gap-3 border-b border-[var(--border)] bg-[var(--background)]/85 px-4 backdrop-blur-md sm:px-6">
          <button
            type="button"
            aria-label="Open menu"
            onClick={() => setMobileOpen(true)}
            className="inline-flex size-9 items-center justify-center rounded-lg text-[var(--muted-foreground)] hover:bg-[var(--muted)] lg:hidden"
          >
            <Menu className="size-5" />
          </button>
          <CampusSwitcher />
          <div className="mx-1 hidden h-5 w-px bg-[var(--border)] md:block" />
          <Breadcrumbs menu={menu} />
          <div className="ml-auto flex items-center gap-1.5">
            <button
              type="button"
              onClick={toggle}
              aria-label={theme === "dark" ? "Switch to light mode" : "Switch to dark mode"}
              title={theme === "dark" ? "Light mode" : "Dark mode"}
              className="inline-flex size-9 items-center justify-center rounded-lg text-[var(--muted-foreground)] transition-colors hover:bg-[var(--muted)] hover:text-[var(--foreground)]"
            >
              {theme === "dark" ? <Sun className="size-[18px]" /> : <Moon className="size-[18px]" />}
            </button>
            <UserMenu />
          </div>
        </header>
        <main className="mx-auto w-full max-w-[1280px] flex-1 px-4 py-6 sm:px-6 lg:px-8 lg:py-8">
          {/* Remount pages when the working campus changes so their data reloads. */}
          <Outlet key={user?.activeCampusId ?? "all"} />
        </main>
      </div>
    </div>
  );
}
