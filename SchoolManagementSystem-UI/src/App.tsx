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
import { GuardianDetailPage } from "@/pages/guardians/detail";
import { GuardiansListPage } from "@/pages/guardians/list";
import { AdmissionPage } from "@/pages/students/admission";
import { StudentsListPage } from "@/pages/students/list";
import { StudentProfilePage } from "@/pages/students/profile";
import { VacanciesPage } from "@/pages/hiring/vacancies";
import { CandidatesPage } from "@/pages/hiring/candidates";
import { CandidateProfilePage } from "@/pages/hiring/candidate";
import { StaffListPage } from "@/pages/staff/list";
import { StaffProfilePage } from "@/pages/staff/profile";
import { MarkAttendancePage } from "@/pages/attendance/mark";
import { AttendanceReportsPage } from "@/pages/attendance/reports";
import { StaffAttendancePage } from "@/pages/attendance/staff";
import { CalendarPage } from "@/pages/attendance/calendar";
import { ExamsPage } from "@/pages/exams/list";
import { ExamDetailPage } from "@/pages/exams/detail";
import { MarksEntryPage } from "@/pages/exams/marks";
import { ResultsPage } from "@/pages/exams/results";
import { GradingPage } from "@/pages/exams/grading";
import { ReportCardsPage } from "@/pages/exams/cards";
import { FeeSetupPage } from "@/pages/fees/setup";
import { ChallansPage } from "@/pages/fees/challans";
import { ChallanPrintPage } from "@/pages/fees/challan-print";
import { CollectFeePage } from "@/pages/fees/collect";
import { ReceiptPrintPage } from "@/pages/fees/receipt-print";
import { FeeReportsPage } from "@/pages/fees/reports";
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
        <Route path="students" element={<StudentsListPage />} />
        <Route path="students/new" element={<AdmissionPage />} />
        <Route path="students/:id" element={<StudentProfilePage />} />
        <Route path="guardians" element={<GuardiansListPage />} />
        <Route path="guardians/:id" element={<GuardianDetailPage />} />
        <Route path="hiring/vacancies" element={<VacanciesPage />} />
        <Route path="hiring/candidates" element={<CandidatesPage />} />
        <Route path="hiring/candidates/:id" element={<CandidateProfilePage />} />
        <Route path="staff" element={<StaffListPage />} />
        <Route path="staff/:id" element={<StaffProfilePage />} />
        <Route path="attendance" element={<MarkAttendancePage />} />
        <Route path="attendance/reports" element={<AttendanceReportsPage />} />
        <Route path="attendance/staff" element={<StaffAttendancePage />} />
        <Route path="attendance/calendar" element={<CalendarPage />} />
        <Route path="exams" element={<ExamsPage />} />
        <Route path="exams/marks" element={<MarksEntryPage />} />
        <Route path="exams/results" element={<ResultsPage />} />
        <Route path="exams/grading" element={<GradingPage />} />
        <Route path="exams/cards" element={<ReportCardsPage />} />
        <Route path="exams/:id" element={<ExamDetailPage />} />
        <Route path="fees/setup" element={<FeeSetupPage />} />
        <Route path="fees/challans" element={<ChallansPage />} />
        <Route path="fees/challans/print" element={<ChallanPrintPage />} />
        <Route path="fees/collect" element={<CollectFeePage />} />
        <Route path="fees/receipt/:id" element={<ReceiptPrintPage />} />
        <Route path="fees/reports" element={<FeeReportsPage />} />
      </Route>
      <Route path="*" element={<Navigate to="/admin" replace />} />
    </Routes>
  );
}
