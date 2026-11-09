import type { ReactNode } from "react";
import { Navigate, Route, Routes } from "react-router-dom";
import { GraduationCap } from "lucide-react";
import { AppShell } from "@/components/app-shell";
import { useAuth } from "@/lib/auth";
import { AcademicSessionsPage } from "@/pages/academics/sessions";
import { ClassesPage } from "@/pages/academics/classes";
import { SectionsPage } from "@/pages/academics/sections";
import { SubjectsPage } from "@/pages/academics/subjects";
import { CampusesPage } from "@/pages/campuses";
import { ChangePasswordPage } from "@/pages/change-password";
import { MyCampusPage } from "@/pages/my-campus";
import { DashboardPage } from "@/pages/dashboard";
import { LoginPage } from "@/pages/login";
import { RolesPage } from "@/pages/roles";
import { UsersPage } from "@/pages/users";

function Protected({ children }: { children: ReactNode }) {
  const { user, loading } = useAuth();
  if (loading) {
    return (
      <div className="grid min-h-screen place-items-center">
        <div className="flex flex-col items-center gap-3 text-sm text-[var(--muted-foreground)]">
          <span className="inline-flex size-10 animate-pulse items-center justify-center rounded-xl bg-[var(--primary)] text-[var(--primary-foreground)]">
            <GraduationCap className="size-5" />
          </span>
          Loading your workspace…
        </div>
      </div>
    );
  }
  if (!user) return <Navigate to="/login" replace />;
  if (user.mustChangePassword) return <ChangePasswordPage />;
  return children;
}

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route
        path="/admin"
        element={
          <Protected>
            <AppShell />
          </Protected>
        }
      >
        <Route index element={<DashboardPage />} />
        <Route path="campuses" element={<CampusesPage />} />
        <Route path="users" element={<UsersPage />} />
        <Route path="roles" element={<RolesPage />} />
        <Route path="my-campus" element={<MyCampusPage />} />
        <Route path="academics/sessions" element={<AcademicSessionsPage />} />
        <Route path="academics/classes" element={<ClassesPage />} />
        <Route path="academics/subjects" element={<SubjectsPage />} />
        <Route path="academics/sections" element={<SectionsPage />} />
      </Route>
      <Route path="*" element={<Navigate to="/admin" replace />} />
    </Routes>
  );
}
