# Attendance — Design

Daily student attendance taken by the class teacher, staff attendance taken by the office, a campus calendar of weekly offs and holidays, and the reports a school needs. Builds on Academic Setup (sessions, sections), Students (enrollments with start/end dates) and Staff (class teachers on sections).

## 1. Goals

- The **class teacher** marks the whole section's attendance once a day; subject teachers do not.
- Campus Admin / Principal can mark or correct any section and any past date in the session (backup when the class teacher is away, corrections).
- The office marks staff attendance daily.
- Holidays and weekly offs are known, so attendance is refused on those days and reports ignore them.
- Reports: today's summary, a section's monthly sheet (with CSV), a student's record, and low attendance.
- Everything is campus-isolated.

## 2. Decisions (agreed)

| Topic | Decision |
|---|---|
| Frequency | Once a day per section (no periods / timetable). |
| Who marks students | The section's class teacher (today and the previous 2 days). Users with `attendance:Edit` who are not restricted teachers (Campus Admin, Principal) may mark any section of their campus and any past date inside the session. A section with no class teacher can only be marked by them. |
| Student statuses | `Present`, `Absent`, `Late`, `Leave`, plus an optional note. The form defaults everyone to Present. |
| Storage | One `AttendanceDay` per (section, date) with who / when, and one `AttendanceEntry` per enrolled student. Saving again edits the same day. |
| Calendar | Per-campus weekly off days (default Sunday) and holidays (date range + name). Main Campus can add a holiday for all campuses. Marking on a weekly off / holiday is refused; reports skip those days. |
| Staff attendance | Office / admin marks daily: `Present`, `Absent`, `Late`, `Leave`, `HalfDay`, optional check-in time and note. Staff who are Onboarding, Active or OnLeave on that date (joined on or before it, not left before it). |
| Percentage | % = (Present + Late) / (marked working days − Leave days) × 100, rounded to one decimal; approved Leave does not lower the %. For staff, HalfDay counts 0.5 attended. Unmarked working days are shown as "not marked" and excluded. With no countable days the % is shown as "—". |
| Reports | Today, Monthly sheet (+CSV), Student record (profile tab), Low attendance (threshold, default 75%). |

## 3. Data model

All tables inherit `AuditableEntity` (audit + soft delete) and are `ICampusScoped` unless noted. Enums stored as strings.

### `CampusCalendar` (one row per campus, created on first use)

| Column | Type / rule |
|---|---|
| CampusId | int, unique |
| WeeklyOff | nvarchar(60): comma-separated `DayOfWeek` names, default `"Sunday"` |

### `Holiday` (`ICampusOwnable`: `CampusId` null = all campuses, written only by Main Campus)

| Column | Type / rule |
|---|---|
| CampusId | int? |
| Name | nvarchar(100) |
| StartDate, EndDate | date, End ≥ Start, at most 60 days long |

### `AttendanceDay`

| Column | Type / rule |
|---|---|
| CampusId | int |
| SectionId | int, FK |
| AcademicSessionId | int, FK (the section's session) |
| Date | date |
| MarkedByUserId | int? |
| MarkedOn | datetime2 (UTC) |
| Present, Absent, Late, Leave | int (counts, kept in sync on save) |

Unique filtered index `(SectionId, Date)` where `[IsDeleted] = 0`.

### `AttendanceEntry` (no campus filter of its own; reached only through its day)

| Column | Type / rule |
|---|---|
| AttendanceDayId | int, FK |
| StudentId | int, FK |
| Status | enum `Present / Absent / Late / Leave` |
| Note | nvarchar(200)? |

Unique `(AttendanceDayId, StudentId)`.

### `StaffAttendance`

| Column | Type / rule |
|---|---|
| CampusId | int |
| StaffId | int, FK |
| Date | date |
| Status | enum `Present / Absent / Late / Leave / HalfDay` |
| CheckIn | time? |
| Note | nvarchar(200)? |
| MarkedByUserId | int? |

Unique filtered index `(StaffId, Date)` where `[IsDeleted] = 0`.

## 4. Rules

**Working day** for a campus and date: not a weekly off of that campus, not inside a holiday of that campus or an all-campus holiday, and inside the section's session (`StartDate ≤ date ≤ EndDate`) for student attendance.

**Roster** of a section on a date: students with an enrollment in that section whose `StartDate ≤ date` and (`EndDate` is null or `EndDate ≥ date`), in roll-number order. Students who are not on the roster cannot be saved for that day (400).

**Marking students** (`PUT /api/attendance/sheet` with `{ sectionId, date, entries: [{ studentId, status, note }] }`):
- Date must not be in the future (campus local = UTC for now) → 400 "You cannot mark a future date."
- Date must be a working day → 400 "{date} is a holiday: {name}." / "{weekday} is a weekly off."
- The section's session must not be Closed → 400 "This session is closed."
- **Restricted (class teacher) path:** a user who is the linked login of the section's class teacher may mark only that section and only for today and the previous 2 calendar days (403 "You can mark this section for today and the last 2 days only."). A teacher who is not the class teacher → 403 "Only the class teacher or an admin can mark this section."
- **Admin path:** a user with `attendance:Edit` whose roles are not Teacher-only — concretely, `attendance:Edit` together with `sections:Edit` or `staff:Edit` (Campus Admin, Principal) — may mark any section of the campus, any working day in the session up to today.
- Every roster student must appear exactly once; missing or extra students → 400.
- Save replaces the entries of the day, updates the counts, `MarkedByUserId`, `MarkedOn`.

**Marking staff** (`PUT /api/staff-attendance` with `{ date, entries: [{ staffId, status, checkIn, note }] }`): working day (weekly off / holiday of the campus), not future, staff must be on the campus's staff roster for that date. Save upserts each `(staff, date)`; entries not sent are left as they are.

**Calendar:** weekly off needs `calendar:Edit`; holidays need `calendar:Add/Edit/Delete`; all-campus holidays only by Main Campus users. Adding a holiday on days that already have attendance is allowed but those days then count as holidays in reports (the saved attendance stays and is shown greyed in the sheet).

## 5. Permissions and menu

New menu group `attendance-menu` ("Attendance", icon `calendar-check`, order after Staff) with CampusLevel children:

| Module key | Name | Route | Icon |
|---|---|---|---|
| `attendance` | Mark attendance | `/admin/attendance` | `clipboard-check` |
| `attendance-reports` | Reports | `/admin/attendance/reports` | `bar-chart-3` |
| `staff-attendance` | Staff attendance | `/admin/attendance/staff` | `user-check` |
| `calendar` | Calendar | `/admin/attendance/calendar` | `calendar-days` |

Default grants (new-module seeding rule):

| Module | Principal | Teacher | Clerk | Accountant |
|---|---|---|---|---|
| attendance | Full | View, Add, Edit | View | — |
| attendance-reports | View | View | View | — |
| staff-attendance | Full | — | View, Add, Edit | — |
| calendar | Full | — | View | — |

CampusAdmin gets full on all (CampusLevel). Read access to the calendar (needed by the marking forms) is granted by `[HasAnyPermission("calendar:View", "attendance:View", "staff-attendance:View")]`.

A Teacher with `attendance-reports:View` sees reports **only for sections they are class teacher of** (the API filters); admins see the campus.

## 6. API

All responses `ResponseDto<T>`, campus-filtered.

- `GET /api/attendance/my-sections` → sections the caller may mark today (class-teacher sections, or all campus sections for admins) with today's status (marked / not marked / holiday).
- `GET /api/attendance/sheet?sectionId=&date=` → `{ section, date, isWorkingDay, reason, canEdit, editLockedReason, marked, markedBy, markedOn, rows: [{ studentId, rollNo, name, hasPhoto, status, note }] }` (unmarked days return everyone Present and `marked: false`).
- `PUT /api/attendance/sheet`
- `GET /api/attendance/today` → `{ date, isWorkingDay, presentPct, sections: [{ sectionId, className, sectionName, classTeacherName, marked, present, absent, late, leave }], absentees: [{ studentId, name, className, sectionName, status }] }`
- `GET /api/attendance/monthly?sectionId=&month=yyyy-MM` → `{ days: [{ date, kind: Working|WeeklyOff|Holiday|Future, holidayName, marked }], students: [{ studentId, rollNo, name, cells: ["P"|"A"|"L"|"Lv"|""], present, absent, late, leave, pct }] }`; `GET /api/attendance/monthly.csv?…` (same, CSV)
- `GET /api/attendance/low?threshold=75&sectionId=` → students of the current session below the threshold (`pct`, `workingDays`, `attended`).
- `GET /api/students/{id}/attendance` → current-session `{ pct, present, absent, late, leave, workingDaysMarked, months: [{ month, present, absent, late, leave, pct }], absences: [{ date, status, note }] }`
- `GET /api/staff-attendance?date=` → `{ date, isWorkingDay, reason, rows: [{ staffId, employeeNo, name, designation, status, checkIn, note, marked }] }`; `PUT /api/staff-attendance`; `GET /api/staff-attendance/monthly?month=`
- `GET /api/calendar` → `{ weeklyOff: [...], holidays: [...] }` for the working campus (+ all-campus ones); `PUT /api/calendar/weekly-off`; `POST/PUT/DELETE /api/calendar/holidays[/{id}]`
- Dashboard KPI `attendance`: "Attendance today" value `{pct}%`, hint `"{n} sections not marked"`, link `/admin/attendance/reports`, shown with `attendance:View`.

Working campus: marking, today, staff and calendar endpoints need a working campus (400 "Select a campus."); section-based endpoints use the section's campus.

## 7. UI

- **Mark attendance:** date picker (defaults today) + section select (from `my-sections`, each shows ✓ marked / • not marked). Roster table: roll no., photo, name, status segmented buttons (P / A / L / Lv), note. Header counts update live; "Mark all present"; Save. Locked states with the reason (holiday, weekly off, future, closed session, older than 2 days for teachers). Mobile: rows stack, buttons stay tappable.
- **Reports:** tabs Today / Monthly sheet / Low attendance.
  - Today: % tile, sections grid (not-marked first, "No class teacher" label), absentees list.
  - Monthly: section + month pickers, sticky first column, horizontally scrolling day columns (weekends/holidays grey), totals and %, "Download CSV".
  - Low attendance: threshold input, section filter, table.
- **Staff attendance:** date picker, staff table with status buttons (P / A / L / Lv / ½), check-in time, note, Save; Monthly tab.
- **Calendar:** weekly-off day toggles; holidays list with add / edit / delete (Main Campus sees an "All campuses" switch).
- **Student profile:** new Attendance tab (%, month table, absences).
- **Dashboard:** Attendance today KPI.

## 8. Errors

| Case | Response |
|---|---|
| Future date | 400 |
| Weekly off / holiday | 400 with the reason |
| Closed session or date outside the session | 400 |
| Teacher not the class teacher | 403 |
| Class teacher, date older than 2 days | 403 |
| Roster mismatch (missing / extra / duplicate student) | 400 |
| Staff not on the roster for the date | 400 |
| Holiday End < Start or longer than 60 days | 400 |
| All-campus holiday by a campus user | 403 |
| Another campus's section / student / staff / holiday | 404 |
| No working campus where required | 400 |

## 9. Testing

- One migration `Attendance`; seeding twice changes nothing; menu and default grants as §5.
- E2E file `tests/e2e/attendance_e2e.py` (reuses helpers, cleans up). Groups: `schema`, `calendar`, `marking` (defaults, save, re-save edits, counts, roster rules incl. late joiner / left student), `class-teacher` (own section today and 2 days back OK, 3 days → 403, other section → 403, no class teacher → admin only), `rules` (future, holiday, weekly off, closed session), `reports` (today, monthly + CSV, low attendance, student record, percentages), `staff-attendance`, `attendance-isolation`, `attendance-permissions`, `dashboard-attendance`.
- Regression: all existing suites.
- UI: build; browser walk-through as a Teacher (class teacher) marking a section, then Campus Admin reports, staff attendance and calendar; mobile and dark mode.

## 10. Out of scope

Period / subject attendance and timetables, SMS / WhatsApp alerts to guardians, biometric devices, staff self check-in, leave applications and approval workflow, payroll deductions, campus time zones other than UTC.
