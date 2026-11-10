# Exams and Results — Design

Exams, marks, results and report cards for students, completing the yearly flow admission → attendance → exams. Promotion to the next class is a later module that will read the annual result defined here. Builds on Academic Setup (sessions, classes with subject lists, sections), Students (enrollments), Staff (teaching assignments, class teachers) and Attendance (percentages on cards).

## 1. Goals

- A campus runs monthly tests, a mid-term, a final and other exams in a session.
- Each exam has papers per class and subject with total and pass marks, optional theory/practical split, and an optional date (date sheet).
- The subject teacher of a section enters and submits marks; admins can enter or correct anything.
- Results (subject and overall %, grade, pass/fail, position) are live until a section's result is published; publishing snapshots them so printed cards never change.
- An annual result combines the session's published exams with configurable weightage (monthly tests averaged).
- Exam and annual report cards (single student or whole section, A4 print), with attendance % and the class teacher's remark.

## 2. Decisions (agreed)

| Topic | Decision |
|---|---|
| Scope | Exams + results + cards. Promotion later. |
| Exam types | `Monthly`, `MidTerm`, `Final`, `Other`. |
| Marks | Per paper: total and pass marks (default 100 / 33). Optional theory + practical split. Per student: theory, practical, status `Present / Absent / Exempted`. |
| Grading | Grade scale (Main Campus default, campus may own one): A+ ≥ 90, A ≥ 80, B ≥ 70, C ≥ 60, D ≥ 50, E ≥ 40, F < 40. A student passes an exam only if every non-exempt subject is passed. |
| Position | Per section, among passed students, by overall % (ties share a position: 1, 2, 2, 4). |
| Entry | Subject teacher (via teaching assignment) enters and submits; class teacher sees the section's progress; admin enters / corrects anything, returns a paper to the teacher, publishes / unpublishes per section. |
| Publish | Live until publish; publish stores a snapshot per student; marks are locked until unpublished. |
| Annual | Result scheme (session; Main Campus default, campus may own one, optional per-class override): weight per exam type summing to 100. Monthly = average of the student's published monthly-test %. Types with no published exam drop out and the remaining weights are scaled to 100. Optional rule "must pass the final". |
| Cards | Exam card and annual card; one student or a whole section; print-ready A4 page (browser print / save as PDF); attendance % and class-teacher remark. |

## 3. Data model

All tables inherit `AuditableEntity` (audit + soft delete). Enums stored as strings.

### `GradeScale` (`ICampusOwnable`: null = Main Campus default)
| Column | Type / rule |
|---|---|
| CampusId | int? (one scale per owner; the campus scale wins over the Main Campus one) |
| Name | nvarchar(60) |

### `GradeBand`
| Column | Type / rule |
|---|---|
| GradeScaleId | int, FK |
| Grade | nvarchar(5) (e.g. "A+") |
| MinPercent | decimal(5,2), 0–100, unique per scale; the band with the highest MinPercent ≤ % applies; there must be a band with MinPercent 0 |
| IsFail | bool (true for F) |

Seeded Main Campus scale: A+ 90, A 80, B 70, C 60, D 50, E 40, F 0 (IsFail).

### `ResultScheme` (`ICampusOwnable`)
| Column | Type / rule |
|---|---|
| CampusId | int? |
| AcademicSessionId | int? (null = default for any session) |
| ClassId | int? (null = all classes) |
| MonthlyWeight, MidTermWeight, FinalWeight, OtherWeight | int 0–100, sum = 100 |
| MustPassFinal | bool |

Resolution for (campus, session, class): campus+session+class → campus+session → campus default → Main Campus+session+class → Main Campus+session → Main Campus default. Seeded Main Campus default: Monthly 20, MidTerm 30, Final 50, Other 0, MustPassFinal true.

### `Exam` (`ICampusOwnable`; a Main Campus exam applies to every campus)
| Column | Type / rule |
|---|---|
| CampusId | int? |
| AcademicSessionId | int, FK |
| Name | nvarchar(100), unique per owner + session |
| Type | `Monthly / MidTerm / Final / Other` |
| StartDate, EndDate | date?, inside the session |
| Status | `Draft / Open / Closed` (Draft: setup only; Open: marks entry; Closed: no new entry) |

### `ExamPaper` (`ICampusOwnable`, same owner as its exam)
| Column | Type / rule |
|---|---|
| ExamId | int, FK |
| ClassId, SubjectId | int, FK; unique per exam |
| TotalMarks | decimal(6,2) > 0 |
| PassMarks | decimal(6,2), 0 < pass ≤ total |
| HasPractical | bool |
| PracticalMarks | decimal(6,2)? (when HasPractical; theory total = Total − Practical) |
| PaperDate | date? |
| IsActive | bool (a removed subject is deactivated, not deleted, once marks exist) |

Created for each class picked when the exam is created, from the class's subject list (`ClassSubject`), with default 100/33 (Monthly default 25/9). Classes / papers can be added later.

### `PaperSection` (`ICampusScoped`) — per paper and section
| Column | Type / rule |
|---|---|
| CampusId | int |
| ExamPaperId | int, FK |
| SectionId | int, FK; unique per paper |
| Status | `NotStarted / InProgress / Submitted` |
| SubmittedByUserId, SubmittedOn | int?, datetime2? |

### `Mark` (`ICampusScoped`)
| Column | Type / rule |
|---|---|
| CampusId | int |
| ExamPaperId | int, FK |
| SectionId | int (the section the student sat in) |
| StudentId | int, FK; unique per paper |
| Theory | decimal(6,2)? (0 ≤ theory ≤ theory total) |
| Practical | decimal(6,2)? (only when HasPractical) |
| Status | `Present / Absent / Exempted` |

### `SectionResult` (`ICampusScoped`) — publish state per exam and section
| Column | Type / rule |
|---|---|
| CampusId | int |
| ExamId, SectionId | int; unique pair |
| IsPublished | bool |
| PublishedOn, PublishedByUserId | datetime2?, int? |

### `StudentResult` (`ICampusScoped`) — snapshot written on publish, removed on unpublish
| Column | Type / rule |
|---|---|
| CampusId | int |
| ExamId, SectionId, StudentId | int; unique (exam, student) |
| Obtained, Total | decimal(7,2) |
| Percent | decimal(5,2) |
| Grade | nvarchar(5) |
| Passed | bool |
| Position | int? |
| SubjectsJson | nvarchar(max): per subject `{ subjectId, name, theory, practical, obtained, total, percent, grade, passed, status }` |

### `ResultRemark` (`ICampusScoped`)
| Column | Type / rule |
|---|---|
| CampusId | int |
| ExamId | int? (null = annual card) |
| AcademicSessionId | int |
| StudentId | int |
| Remark | nvarchar(300) |
Unique (exam/session, student).

## 4. Rules

**Roster of a paper section:** students enrolled in the section on the exam's end date (or today if none / later), as in attendance (`StartDate ≤ date`, `EndDate` null or ≥ date).

**Marks entry** (`PUT /api/marks/{paperId}/sections/{sectionId}` with `{ rows: [{ studentId, theory, practical, status }] }`):
- Exam must be `Open` (400 "Marks can be entered only while the exam is open.").
- Section result must not be published (400 "This result is published. Unpublish it first.").
- **Teacher path:** caller's staff (linked login) holds the current teaching assignment for (section, subject), and the paper section is not `Submitted` (403 "Only the subject teacher or an admin can enter these marks." / 400 "Submitted. Ask an admin to return it.").
- **Admin path:** `marks:Edit` + (`sections:Edit` or `staff:Edit`).
- Every row's student must be on the roster; theory/practical within their totals; Absent/Exempted rows store null marks. Partial saves allowed (missing students stay unentered).
- Saving sets the paper section `InProgress`.
- **Submit** (`POST …/submit`): teacher or admin; every roster student must have a row (400 listing the count missing). **Return** (`POST …/return`): admin only, back to `InProgress`.

**Result computation (per exam, section, student):**
- Paper obtained = theory + practical; Absent = 0 and fail; Exempted = excluded from totals and pass.
- Subject pass = obtained ≥ PassMarks (and Present).
- Overall total = Σ paper totals (non-exempt, active papers); obtained = Σ obtained; % = obtained / total × 100 (2 dp).
- Grade from the resolved scale; overall pass = every non-exempt subject passed.
- Position: among passed students of the section by % descending; equal % share a position (competition ranking).
- Students with no marks for any paper are "Not entered" and excluded from position.

**Publish** (`POST /api/results/{examId}/sections/{sectionId}/publish`, `results:Edit` + admin path): all active paper sections of the section `Submitted` (400 listing pending subjects); writes `StudentResult` rows and sets `IsPublished`. **Unpublish** removes the rows and unlocks marks.

**Annual result (per student, session):** for each exam type with a weight > 0: Monthly = average % of the student's published monthly exams; MidTerm / Final / Other = average % of published exams of that type (normally one). Types without a published exam drop out; the remaining weights are scaled to 100. Annual % = Σ type% × weight / Σ weights; grade from the scale; pass = passed the final (when `MustPassFinal`) and annual grade not a fail band. Annual results are computed live from the snapshots (no annual snapshot in this module).

**Attendance on cards:** student attendance % (Attendance module rules) from the exam's `StartDate` back to the session start for exam cards, whole session for annual cards.

## 5. Permissions and menu

New menu group `exams-menu` ("Exams", icon `file-pen`, after Attendance) with CampusLevel children:

| Key | Name | Route | Icon |
|---|---|---|---|
| `exams` | Exams | `/admin/exams` | `file-text` |
| `marks` | Marks entry | `/admin/exams/marks` | `pencil-line` |
| `results` | Results | `/admin/exams/results` | `trophy` |
| `grading` | Grading | `/admin/exams/grading` | `sliders-horizontal` |

Default grants: Principal full on all four. Teacher: exams View, marks View + Edit, results View. Clerk: exams View, results View. Accountant: none. CampusAdmin full (CampusLevel).

Teachers see results only for sections where they are class teacher or teach a subject; marks entry only for their assigned papers.

## 6. API

- Exams: `GET /api/exams?sessionId=` · `GET /api/exams/{id}` (with papers grid and section progress) · `POST /api/exams` `{ name, type, sessionId, startDate, endDate, classIds }` · `PUT /api/exams/{id}` · `POST /api/exams/{id}/status` `{ status }` · `DELETE /api/exams/{id}` (only without marks) · `PUT /api/exams/{id}/papers/{paperId}` (totals, practical, date, active) · `POST /api/exams/{id}/classes` (add classes)
- Marks: `GET /api/marks/my-papers?examId=` · `GET /api/marks/{paperId}/sections/{sectionId}` · `PUT …` · `POST …/submit` · `POST …/return`
- Results: `GET /api/results/{examId}/sections/{sectionId}` (tabulation, live or snapshot) · `GET …/csv` · `POST …/publish` · `POST …/unpublish` · `GET /api/results/annual?sessionId=&sectionId=` · `PUT /api/results/remarks` `{ examId?, sessionId, studentId, remark }`
- Cards: `GET /api/report-cards/exam/{examId}?sectionId=|studentId=` · `GET /api/report-cards/annual?sessionId=&sectionId=|studentId=` (JSON the print page renders)
- Grading: `GET/PUT /api/grading/scale` (campus or Main Campus) · `GET/PUT /api/grading/schemes`
- Student: `GET /api/students/{id}/results` (published exams + annual)
- Dashboard KPI `exams`: "Exams" value = name of the latest open exam or "—", hint `"{submitted}/{total} sections submitted"`.

## 7. UI

- **Exams list:** session filter, cards per exam (type, dates, status, progress bar of submitted sections), "New exam" dialog (name, type, dates, classes).
- **Exam detail:** papers grid by class (subject rows: total, pass, practical split, date; inline edit while Draft/Open), section progress chips, status buttons (Open / Close), per-section Publish / Unpublish.
- **Marks entry:** paper picker (teacher: own papers only), roll-wise table (theory, practical, Absent / Exempt toggles), live total and grade, Save, Submit; locked banner with the reason.
- **Results:** exam + section picker, tabulation table (sticky name column, subject columns, total, %, grade, position, pass/fail), Publish, CSV; annual view toggle.
- **Report cards:** print page (`/admin/exams/cards?…`) with one A4 card per student, school/campus header, marks table, totals, grade, position, attendance %, remark, signature lines; print CSS (one card per page); remark editing for the class teacher before print.
- **Grading:** grade bands table; result scheme weights (must sum to 100) with MustPassFinal switch; class overrides.
- **Student profile:** Results tab (published exams list with %, grade, position; annual summary; card link).
- **Dashboard:** Exams KPI.

## 8. Errors

| Case | Response |
|---|---|
| Marks above the paper's theory/practical total, or negative | 400 |
| Exam not Open | 400 |
| Section published | 400 "This result is published. Unpublish it first." |
| Teacher not assigned to the paper's section/subject | 403 |
| Teacher editing a submitted paper | 400 |
| Submit with unentered students | 400 with the count |
| Publish with pending papers | 400 listing subjects |
| Weights not summing to 100 / pass > total / bad grade bands | 400 |
| Delete an exam that has marks | 400 |
| Another campus's exam / section / student / paper | 404 |

## 9. Testing

- One migration `Exams`; seeding twice changes nothing (scale, scheme, menu, grants).
- E2E `tests/e2e/exams_e2e.py` groups: `schema`, `setup` (create exam → papers from class subjects, practical split, date sheet, status), `marks` (teacher path, admin path, limits, absent/exempt, submit/return, partial save), `results` (computation, grades, pass rule, positions with ties, publish snapshot unchanged after scale change, unpublish), `annual` (weights, monthly average, missing type rescale, must-pass-final), `cards`, `grading` (scale and scheme validation and resolution), `exams-isolation`, `exams-permissions`, `dashboard-exams`.
- Regression: all existing suites.
- UI: build; browser: admin creates an exam, teacher enters and submits marks, admin publishes, section cards printed (print preview), annual card; mobile and dark mode.

## 10. Out of scope

Promotion to the next class, online exams, question banks, timetable/seating plans, SMS of results, GPA/CGPA systems, board-specific grading, marks import from Excel.
