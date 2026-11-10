# Attendance Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Daily student attendance by the class teacher (admins as backup), staff attendance by the office, a campus calendar, and the Today / Monthly / Student / Low-attendance reports.

**Architecture:**
- **Data:** `CampusCalendar`, `AttendanceDay`, `StaffAttendance` are `AuditableEntity, ICampusScoped`; `Holiday` is `ICampusOwnable` (null = all campuses); `AttendanceEntry` hangs off its day. One migration.
- **One calendar service** (`CalendarService.IsWorkingDayAsync`) answers "can this campus work on this date, and if not why" for every other service.
- **Marking access** lives in one place: `AttendanceAccess.ResolveAsync(section, date)` returns admin / class-teacher / denied with the reason.
- **Reports** are read-only queries in `AttendanceReportService`; percentages use one helper `AttendanceMath.Pct`.
- **UI:** an Attendance menu group with four pages and a student profile tab, built from the existing kit.

**Tech Stack:** .NET 10, ASP.NET Core, EF Core 10 (SQL Server), React 19, TypeScript, Tailwind v4, lucide-react, Python 3 e2e scripts (stdlib).

**Spec:** `docs/superpowers/specs/2026-11-11-attendance-design.md`

## Global Constraints

- **Git:** work on `main`. **No new branch, no commits.** Leave changes uncommitted.
- **Migration:** one migration, `Attendance`; regenerate (remove + add) if a later task changes the schema. Filtered index filters must not use `NOT IN` (SQL Server); write `<>` conditions. Run `sqlcmd` with `-I`.
- **Wording:** user-visible text says "Main Campus", never "HQ".
- **Dates:** "today" = `DateOnly.FromDateTime(DateTime.UtcNow)`; new `DateTime` columns use `Utc.Converter` / `Utc.NullableConverter` (in `HiringConfigurations.cs`).
- **Percent:** `% = (Present + Late) / (marked working days − Leave) × 100`, one decimal; staff `HalfDay` = 0.5 attended; no countable days → `null` (UI shows "—").
- **Class-teacher window:** today and the previous 2 calendar days.
- **Admin path:** `attendance:Edit` **and** (`sections:Edit` or `staff:Edit`).
- **Error copy (exact):**
  - "Select a campus."
  - "You cannot mark a future date."
  - "{d MMM yyyy} is a holiday: {Name}." / "{DayOfWeek} is a weekly off."
  - "This session is closed."
  - "Only the class teacher or an admin can mark this section."
  - "You can mark this section for today and the last 2 days only."
  - "The list does not match the students of this section on {d MMM yyyy}."
- **E2E:** new `tests/e2e/attendance_e2e.py`; reuse `sms_client`, `academics_e2e` (`campus_admin`, `demo_campuses`, `create_session`, `session_body`, `sessions_list`), `students_e2e` (`admit`, `section_in`), `onboarding_e2e` (`make_staff`, `assign`, `set_status`, `login_body`, `retire_staff`, `section_of`). Tests pick dates that are working days via `GET /api/calendar`, and clean holidays / weekly-off changes they make. Add cleanup kind `holidays` (route `calendar/holidays`) to `sms_client`, deleted first.
- **Regression:** after each API task run `hiring_e2e.py hire`, `onboarding_e2e.py lifecycle`, `students_e2e.py role-access`; add `"attendance-menu"` before `"settings"` in every existing `schema` / `seed` menu assertion (academics, students, hiring) in Task 1.

## Review Focus

1. **Late joiner / left student:** a student admitted after the date, or who left before it, is not on that day's roster, and saving them is refused; a student who changed section mid-month appears in each section only for their days. Test: Task 2 `roster_follows_enrollment_dates`.
2. **Class teacher changes:** after the class teacher is replaced, the old teacher can no longer mark that section and the new one can. Test: Task 3 `class_teacher_swap`.
3. **Holiday added after marking:** the day's attendance stays saved but the monthly sheet shows it as Holiday and the % ignores it. Test: Task 4 `holiday_after_marking_excluded`.
4. **Concurrent first save of a day:** two saves of a new (section, date) → one day row, never 500 (unique index → 409 on the loser, or the second updates). Test: Task 2 `concurrent_first_save_one_day`.
5. **Resigned staff and staff roster:** a member who left before the date or joined after it is not on that day's staff roster; saving them → 400. Test: Task 5 `staff_roster_by_dates`.

---

## File Map

**API (SchoolManagementSystem-API/)**
- Domain: create `Entities/Attendance/AttendanceEntities.cs` (enums `StudentAttendanceStatus { Present, Absent, Late, Leave }`, `StaffAttendanceStatus { Present, Absent, Late, Leave, HalfDay }`, `DayKind { Working, WeeklyOff, Holiday, Future, OutsideSession }`; entities per spec §3), `DTOs/Attendance/AttendanceDtos.cs`; modify `Constants/AppModules.cs` (`AttendanceMenu = "attendance-menu"`, `Attendance = "attendance"`, `AttendanceReports = "attendance-reports"`, `StaffAttendance = "staff-attendance"`, `Calendar = "calendar"`).
- Infrastructure: create `Configurations/Attendance/AttendanceConfigurations.cs`; DbSets; `Seeding/DatabaseSeeder.cs` (group order 7, Settings → 8); `Seeding/RoleDefaultsSeeder.cs` (spec §5 table); migration `Attendance`.
- Application: create `Repository/AttendanceRepositories.cs` (`CalendarRepository`, `AttendanceRepository`, `StaffAttendanceRepository`); `Services/Attendance/{CalendarService,AttendanceAccess,AttendanceService,AttendanceReportService,StaffAttendanceService,AttendanceMath}.cs`; `Interfaces/IAttendanceServices.cs`; modify `Repository/SchoolManagementSystemRepositories.cs`, `Services/Dashboard/DashboardService.cs`.
- Api: create `Controllers/AttendanceControllers.cs` (`AttendanceController`, `StaffAttendanceController`, `CalendarController`); modify `Controllers/StudentsController.cs` (`GET {id}/attendance`), `Startup.cs`.

**Tests:** create `tests/e2e/attendance_e2e.py`; modify `tests/e2e/sms_client.py`, menu assertions in `academics_e2e.py`, `students_e2e.py`, `hiring_e2e.py`.

**UI (SchoolManagementSystem-UI/src/)**: modify `lib/types.ts`, `App.tsx`, `components/app-shell.tsx` (icons `calendar-check`, `clipboard-check`, `bar-chart-3`, `user-check`, `calendar-days`), `pages/dashboard.tsx` (kpi `attendance`), `pages/students/profile.tsx` (Attendance tab); create `pages/attendance/{mark,reports,staff,calendar}.tsx`.

---

### Task 1: Schema, menu, grants, calendar

**Interfaces:**
- `CalendarService` / `ICalendarService`:
  - `GetAsync() -> Result<CalendarDto>` (working campus; `CalendarDto(IReadOnlyList<DayOfWeek> WeeklyOff, IReadOnlyList<HolidayDto> Holidays)`; `HolidayDto(int Id, int? CampusId, string? CampusCode, string Name, DateOnly StartDate, DateOnly EndDate)`)
  - `SetWeeklyOffAsync(WeeklyOffRequest(IReadOnlyList<DayOfWeek> Days))` (0–6 days, distinct)
  - `CreateHolidayAsync / UpdateHolidayAsync(int id, …) / DeleteHolidayAsync(int id)` with `SaveHolidayRequest(string Name, DateOnly StartDate, DateOnly EndDate, bool AllCampuses)`
  - `internal Task<DayInfo> IsWorkingDayAsync(int campusId, DateOnly date)` → `DayInfo(DayKind Kind, string? Reason)` (Future when date > today; WeeklyOff; Holiday with the name). Session range is checked by callers.
- Routes `api/calendar`: `GET` (`[HasAnyPermission("calendar:View","attendance:View","staff-attendance:View")]`), `PUT weekly-off` (calendar:Edit), `POST holidays` (Add), `PUT holidays/{id}` (Edit), `DELETE holidays/{id}` (Delete). All-campus holiday by a non-Main-Campus user → 403. No working campus → 400 "Select a campus." (a Main Campus user may create an all-campus holiday without one).

- [ ] **Step 1: Write groups `schema` and `calendar`.**
  - `schema`: menu `[... "staff-menu", "attendance-menu", "settings"]`; children `["attendance","attendance-reports","staff-attendance","calendar"]`; Principal full on attendance / staff-attendance / calendar and View on reports; Teacher `attendance` = {view, add, edit}, `attendance-reports` = {view}, none on staff-attendance / calendar; Clerk `attendance` {view}, reports {view}, staff-attendance {view, add, edit}, calendar {view}; Accountant none; CampusAdmin full on all four.
  - `calendar`: default weekly off `["Sunday"]`; set `["Friday","Sunday"]` → GET returns it; holiday create / edit / delete; End < Start → 400; 61 days → 400; campus admin all-campus → 403; Main Campus all-campus holiday visible on both campuses; restore weekly off at the end.
  - Update the three existing menu assertions.
- [ ] **Step 2: Run.** → FAIL.
- [ ] **Step 3: Implement** entities, configurations, DbSets, seeding, migration `Attendance` (rebuild after), calendar repository / service / controller.
- [ ] **Step 4: Run `schema calendar` + the three older `schema`/`seed` groups + regression.** → PASS.

### Task 2: Marking API — sheet read and save (admin path)

**Interfaces:**
- `AttendanceAccess.ResolveAsync(Section section, DateOnly date) -> AccessResult(bool CanEdit, bool IsAdmin, string? LockedReason)` — admin path per Global Constraints; class-teacher path = caller's `UserId` equals the linked `Staff.UserId` of `section.ClassTeacherStaffId`, window today−2..today; otherwise not allowed with the exact copy.
- DTOs: `SheetDto(int SectionId, string ClassName, string SectionName, DateOnly Date, bool IsWorkingDay, string? Reason, bool CanEdit, string? EditLockedReason, bool Marked, string? MarkedBy, DateTime? MarkedOn, IReadOnlyList<SheetRowDto> Rows)`; `SheetRowDto(int StudentId, int RollNo, string Name, bool HasPhoto, StudentAttendanceStatus Status, string? Note)`; `SaveSheetRequest(int SectionId, DateOnly Date, IReadOnlyList<SheetEntryRequest> Entries)`; `SheetEntryRequest(int StudentId, StudentAttendanceStatus Status, string? Note)`; `MySectionDto(int SectionId, string ClassName, string SectionName, bool IsClassTeacher, bool MarkedToday)`.
- `IAttendanceService`: `MySectionsAsync()`, `GetSheetAsync(int sectionId, DateOnly date)`, `SaveSheetAsync(SaveSheetRequest)`.
- Routes `api/attendance`: `GET my-sections`, `GET sheet` (attendance:View), `PUT sheet` (attendance:Add — Edit is checked by `AttendanceAccess`).
- Roster query: `AttendanceRepository.RosterAsync(int sectionId, DateOnly date)` per spec §4 (non-deleted enrollments of that section with `StartDate ≤ date` and `EndDate` null or ≥ date), ordered by `RollNo`.

- [ ] **Step 1: Write group `marking`** (as campus admin, on a fresh QA section with 3 admitted students):
  - unmarked sheet: everyone Present, `marked` false, `canEdit` true
  - save with one Absent (note "sick") and one Late → 200; sheet `marked` true, statuses back, counts present 1 / absent 1 / late 1
  - re-save changes Absent → Leave → sheet shows Leave; still one day
  - `roster_follows_enrollment_dates`: admit a 4th student with admission date after the test date → not on the sheet for that date; saving them → 400 roster message; missing one student → 400; duplicate student → 400; a student moved to another section (`change-section`) today is on the new section's sheet today and on the old section's sheet for yesterday
  - `concurrent_first_save_one_day`: two threads save a new date → codes ⊆ {200, 409}, at least one 200, one day afterwards
  - `my-sections` for the admin lists the QA section
- [ ] **Step 2: Run.** → FAIL. **Step 3: Implement.** **Step 4: Run + regression.** → PASS.

### Task 3: Rules and the class-teacher path

- [ ] **Step 1: Write groups `rules` and `class-teacher`.**
  - `rules`: future date → 400 copy; weekly off (set Friday off, pick a Friday ≤ today) → 400 "Friday is a weekly off."; holiday → 400 "… is a holiday: QA Holiday."; date before the section's session start → 400; closed session → 400 "This session is closed." (use a QA campus session set Active→Closed, restore).
  - `class-teacher`: a teacher with a login, class teacher of the QA section → `my-sections` lists only it (`isClassTeacher` true); marks today → 200; 2 days ago (working) → 200; 3+ days ago → 403 window copy; another section → 403 class-teacher copy; a section with no class teacher → teacher 403, admin 200; `class_teacher_swap`: set another teacher as class teacher → old teacher 403, new teacher 200; a Clerk (attendance View only) save → 403.
- [ ] **Step 2: Run.** → FAIL where not yet covered. **Step 3: Implement / fix.** **Step 4: Run all attendance groups + regression.** → PASS.

### Task 4: Reports

**Interfaces:**
- `AttendanceMath.Pct(int present, int late, int markedDays, int leave) -> decimal?` and staff overload with half days.
- DTOs: `TodayDto(DateOnly Date, bool IsWorkingDay, string? Reason, decimal? PresentPct, IReadOnlyList<TodaySectionDto> Sections, IReadOnlyList<AbsenteeDto> Absentees)`; `TodaySectionDto(int SectionId, string ClassName, string SectionName, string? ClassTeacherName, bool Marked, int Present, int Absent, int Late, int Leave)`; `AbsenteeDto(int StudentId, string Name, string ClassName, string SectionName, StudentAttendanceStatus Status)`; `MonthlyDto(IReadOnlyList<MonthDayDto> Days, IReadOnlyList<MonthStudentDto> Students)`; `MonthDayDto(DateOnly Date, DayKind Kind, string? HolidayName, bool Marked)`; `MonthStudentDto(int StudentId, int RollNo, string Name, IReadOnlyList<string> Cells, int Present, int Absent, int Late, int Leave, decimal? Pct)` (cells `"P"|"A"|"L"|"Lv"|""`); `LowDto(int StudentId, string Name, string ClassName, string SectionName, int WorkingDays, int Attended, decimal Pct)`; `StudentAttendanceDto(decimal? Pct, int Present, int Absent, int Late, int Leave, int WorkingDaysMarked, IReadOnlyList<MonthSummaryDto> Months, IReadOnlyList<AbsenceDto> Absences)`.
- `IAttendanceReportService`: `TodayAsync()`, `MonthlyAsync(int sectionId, string month)`, `MonthlyCsvAsync(int sectionId, string month) -> (byte[], string FileName)`, `LowAsync(decimal threshold, int? sectionId)`, `StudentAsync(int studentId)`.
- Routes: `GET api/attendance/today|monthly|monthly.csv|low` (attendance-reports:View); `GET api/students/{id}/attendance` (`[HasAnyPermission("students:View","attendance-reports:View")]`). Teachers without the admin path see only their class-teacher sections in today / monthly / low (403 for another section's monthly).
- CSV: header `Roll,Name,{d1..dn},Present,Absent,Late,Leave,%`, UTF-8 with BOM, file `attendance-{Class}-{Section}-{yyyy-MM}.csv`.

- [ ] **Step 1: Write groups `reports` and `dashboard-attendance`.**
  - Mark 3 working days for the QA section with known statuses; monthly cells match; totals and `pct` match the formula (e.g. P,P,A → 66.7; P,L,Lv → 100.0); weekly off / holiday days carry their kind
  - `holiday_after_marking_excluded`: add a holiday over a marked day → that day kind Holiday, `pct` recomputed without it; delete the holiday after
  - CSV: 200, `text/csv`, first line starts `Roll,Name,`, row for each student
  - today: QA section listed with counts; unmarked QA section listed `marked` false; absentees include the Absent student (marked today)
  - low (threshold 75) includes the 66.7% student, not the 100% one
  - student record: `pct`, months, absences list the Absent date
  - teacher sees only own sections in today; teacher monthly for another section → 403
  - `dashboard-attendance`: KPI `attendance` value ends with `%` or is `"—"`, hint matches `^\d+ sections? not marked$`; absent for Accountant
- [ ] **Step 2: Run.** → FAIL. **Step 3: Implement.** **Step 4: Run + regression.** → PASS.

### Task 5: Staff attendance

**Interfaces:**
- DTOs: `StaffSheetDto(DateOnly Date, bool IsWorkingDay, string? Reason, IReadOnlyList<StaffSheetRowDto> Rows)`; `StaffSheetRowDto(int StaffId, string EmployeeNo, string Name, StaffDesignation Designation, StaffAttendanceStatus? Status, TimeOnly? CheckIn, string? Note, bool Marked)`; `SaveStaffSheetRequest(DateOnly Date, IReadOnlyList<StaffSheetEntryRequest> Entries)`; `StaffSheetEntryRequest(int StaffId, StaffAttendanceStatus Status, TimeOnly? CheckIn, string? Note)`; `StaffMonthlyDto` like `MonthlyDto` with `"H"` for HalfDay.
- `IStaffAttendanceService`: `GetAsync(DateOnly date)`, `SaveAsync(SaveStaffSheetRequest)`, `MonthlyAsync(string month)`.
- Roster: staff of the working campus with `JoiningDate ≤ date`, status not Resigned / Terminated or `LeftOn ≥ date`.
- Routes `api/staff-attendance`: `GET` (View), `PUT` (Add), `GET monthly` (View).

- [ ] **Step 1: Write group `staff-attendance`:** two QA staff; save P with check-in 08:05 and HalfDay → GET shows them; re-save upserts; `staff_roster_by_dates`: a member joining tomorrow not listed, saving → 400; a member resigned yesterday not listed today; holiday / future → 400; monthly shows `"H"` and pct with 0.5; Clerk can save (200), Teacher 403.
- [ ] **Step 2–4:** RED → implement → GREEN + regression.

### Task 6: Isolation and permissions

- [ ] **Step 1: Write groups `attendance-isolation` and `attendance-permissions`.** Campus-B section marked, B staff marked, B holiday. As campus-A admin: sheet GET / PUT for the B section, monthly / CSV for it, student record of a B student, B holiday edit / delete → 404; today and low lists show no B rows; staff sheet shows no B staff; saving a B staff id → 400. Permissions: Accountant 403 on sheet / reports / staff sheet / calendar edit; Teacher 403 on staff-attendance and calendar edit; Principal 200 everywhere.
- [ ] **Step 2: Run; fix real bugs in the owning service. Step 3: Run every attendance group and all older suites.** → PASS.

### Task 7: UI — Mark attendance and Calendar

**Files:** `lib/types.ts`, `App.tsx` (routes `attendance`, `attendance/reports`, `attendance/staff`, `attendance/calendar`), `app-shell.tsx` icons, `pages/attendance/mark.tsx`, `pages/attendance/calendar.tsx`.

- [ ] **Step 1: Build.**
  - **Mark:** date input (max today) + section select from `my-sections` (✓ / • marker); roster table (roll, photo via `StudentPhoto`, name, segmented P / A / L / Lv, note); live counts; "Mark all present"; Save; locked banner with `editLockedReason` / `reason`; "Marked by X at time" line. Rows stack on mobile.
  - **Calendar:** seven weekday toggles (Save), holidays table with add / edit / delete dialog (name, start, end, "All campuses" switch for Main Campus users).
- [ ] **Step 2: Verify:** build 0; browser as a class teacher (Teacher login linked to staff): mark a section, edit it, see the lock on a 3-days-old date; as admin set a holiday and see the form lock; 375 px + dark mode; no console errors.

### Task 8: UI — Reports, Staff attendance, Student tab, Dashboard

**Files:** `pages/attendance/reports.tsx`, `pages/attendance/staff.tsx`, `pages/students/profile.tsx`, `pages/dashboard.tsx`.

- [ ] **Step 1: Build.**
  - **Reports:** tabs Today (pct tile, sections grid with not-marked first and "No class teacher" label, absentees list; clicking a section opens Mark for it) / Monthly (section + month pickers, sticky name column, scrollable days, grey off-days, totals, Download CSV via `downloadBlob`) / Low (threshold input default 75, section filter, table linking to the student).
  - **Staff attendance:** date input, table with P / A / L / Lv / ½ buttons, check-in time input, note, Save; Monthly tab.
  - **Student profile:** Attendance tab (pct tile, month table, absences).
  - **Dashboard:** `kpiStyle.attendance` (CalendarCheck, success).
- [ ] **Step 2: Verify:** build 0; browser as Campus Admin: today, monthly + CSV, low, staff attendance save, student tab, dashboard card; Teacher sees only own section in reports; 375 px + dark mode (monthly table scrolls inside its panel, page does not overflow).

### Task 9: Final check

- [ ] Every attendance group, every older suite → PASS; `npm run build` → 0; clean QA data (holidays, weekly off restored, attendance rows of QA sections removed through cleanup).
