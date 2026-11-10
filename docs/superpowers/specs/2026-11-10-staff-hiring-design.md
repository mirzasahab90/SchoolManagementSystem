# Staff: Hiring and Onboarding — Design

**Date:** 2026-11-10
**Status:** Draft. Awaiting review.
**Depends on:** campus isolation (`ICampusScoped`), Campus Admin and role ownership, academic setup, and students (for the shared file-upload safety). All are on `main`.
**Repo rule:** work on `main` and do not commit (product owner's instruction).
**Wording rule:** user-visible text says "Main Campus" for head office (never "HQ").

## 1. Purpose and scope

A campus fills its staff positions through a hiring pipeline, from vacancy to candidate, interview, offer and hire. Hiring creates a staff member, who is then onboarded: documents, checklist, login, and, for teachers, sections and subjects. Staff can also be onboarded directly, without hiring. This covers teaching and non-teaching staff.

The work is delivered in two parts. They share this spec but get separate plans, and the **Hiring part is built first** (product owner's decision):

- **Part 1, Hiring (this plan):**
  - vacancies
  - candidates with CVs
  - a stage pipeline
  - interview rounds with feedback
  - offers
  - hire, which creates a basic `Staff` record
  - a basic staff list
  - a dashboard card
- **Part 2, Staff and Onboarding (later plan):**
  - the full staff profile
  - direct onboarding
  - documents and the onboarding checklist
  - login account creation
  - teaching assignments and class teachers
  - the staff status lifecycle (leave, resign, terminate)

## 2. Decisions (agreed with the product owner)

| Topic | Decision |
|---|---|
| How candidates arrive | Campus staff create vacancies and enter candidates themselves (walk-in, email, referral). There is no public application form. |
| Interviews | Multiple rounds per candidate (Screening, Demo class, Principal, Final, or custom). Each has a date and time, an interviewer, a result (Scheduled / Pass / Fail / Hold), a rating from 1 to 5, and notes. |
| Pipeline | `Applied → Shortlisted → Interview → Offered → Hired`, plus `Rejected` and `Withdrawn` (both need a reason). |
| Onboarding contents (Part 2) | Teaching assignment, login account, salary and joining info, documents and checklist. |
| Staff types | All staff, teaching and non-teaching. Teaching assignments apply to teaching staff only. |
| Home campus | Each staff member and candidate belongs to one campus. Visiting staff on several campuses come later. |
| Build order | Hiring first. `Hire` creates a basic staff record with status `Onboarding`, and Part 2 completes it. |
| Sensitive data | Salary and CNIC data stay out of Teacher, Clerk and Accountant reach by default. |

## 3. Data model

All tables inherit `AuditableEntity` (audit columns and soft delete) and are `ICampusScoped`, unless noted. Enum values are stored as strings.

### `Vacancies`
| Column | Notes |
|---|---|
| CampusId | Stamped from the working campus |
| Title | nvarchar(150), required (e.g. "Mathematics Teacher") |
| Designation | enum `Teacher / Principal / Coordinator / Accountant / Clerk / Librarian / Driver / Guard / Cleaner / Other` |
| Category | enum `Teaching / NonTeaching` |
| Positions | int ≥ 1 |
| ClassIds, SubjectIds | Optional. Stored as a comma-separated list of ids (nvarchar(500)) for display and filtering only |
| Requirements | nvarchar(2000)? |
| ClosingDate | date? |
| Status | enum `Open / OnHold / Closed / Filled` |

### `Candidates`
| Column | Notes |
|---|---|
| CampusId, VacancyId | The candidate's campus is the vacancy's campus |
| FullName | nvarchar(150), required |
| Cnic | nvarchar(20)?, digits only. Unique per vacancy among non-deleted rows |
| Phone | nvarchar(50), required |
| Email, Qualification | nvarchar(256 / 200)? |
| ExperienceYears | decimal(4,1)? |
| CurrentSalary, ExpectedSalary | decimal(12,2)? |
| Source | enum `WalkIn / Referral / Email / JobPortal / Other` |
| CvPath, CvFileName | Stored with the student file-storage rules (§6) |
| Stage | enum `Applied / Shortlisted / Interview / Offered / Hired / Rejected / Withdrawn` |
| StageReason | nvarchar(500)? (required for Rejected and Withdrawn) |
| StaffId | int?, set on hire |

### `CandidateStageHistory` (append-only, no soft delete)
CandidateId, FromStage, ToStage, Reason, OccurredOn, ByUserId.

### `InterviewRounds`
| Column | Notes |
|---|---|
| CandidateId | |
| Name | nvarchar(60) (Screening, Demo class, …) |
| ScheduledAt | datetime |
| InterviewerUserId | Must be an active user who can access the candidate's campus |
| Result | enum `Scheduled / Pass / Fail / Hold` |
| Rating | int? 1–5 (required when Result ≠ Scheduled) |
| Notes | nvarchar(2000)? |

### `Offers` (one active offer per candidate)
CandidateId, Designation, EmploymentType (`Permanent / Contract / Visiting / PartTime`), OfferedSalary decimal(12,2), ProposedJoiningDate, Status (`Pending / Accepted / Declined / Withdrawn`), RespondedOn.

### `Staff` (basic in Part 1; Part 2 adds the rest)
| Column | Notes |
|---|---|
| CampusId | Home campus |
| EmployeeNo | `{CampusCode}-EMP-{0001}`, from `CampusSequences` key `EMP`. Unique per campus |
| FirstName, LastName | Split from the candidate's full name on hire (last word → LastName) |
| Cnic, Phone, Email | Copied from the candidate. Cnic is unique among non-deleted staff |
| Category, Designation | |
| EmploymentType, JoiningDate, BasicSalary | |
| Status | enum `Onboarding / Active / OnLeave / Resigned / Terminated`. Hire sets `Onboarding` |
| SourceCandidateId | int? |

## 4. Pipeline rules

**Allowed moves.** Anything else returns 400, listing the allowed next stages.

| From | To |
|---|---|
| Applied | Shortlisted, Rejected, Withdrawn |
| Shortlisted | Interview, Rejected, Withdrawn |
| Interview | Offered (only via making an offer), Rejected, Withdrawn |
| Offered | Hired (only via hire), Rejected, Withdrawn |
| Rejected, Withdrawn | Applied (reopen, with a reason) |
| Hired | — (final) |

**Automatic moves:**
- Scheduling the first interview round moves Shortlisted to Interview.
- Making an offer moves Interview to Offered. It requires at least one round with result `Pass`, otherwise 400 "Record a passed interview first."
- Declining or withdrawing the offer keeps the stage at Offered. The UI then offers Reject or a new offer.
- Every move writes a `CandidateStageHistory` row.

**Hire** (`POST /candidates/{id}/hire`) runs in one transaction:
1. The stage must be Offered, and the offer must be Accepted.
2. The candidate's CNIC must not belong to an active staff member (409 if it does).
3. Create the `Staff` row with the next employee number.
4. Set the candidate's stage to Hired and record the `StaffId`.
5. Write the history row.
6. When the vacancy's hired candidates reach `Positions`, set the vacancy to `Filled`.

A second hire on the same candidate returns 409. A unique filtered index on `Staff(SourceCandidateId)` guards against races.

**Vacancies:**
- Candidates can only be added to `Open` vacancies.
- A vacancy can be closed and reopened (`Open ↔ OnHold ↔ Closed`). `Filled` is set automatically, and reopening a Filled vacancy is allowed when positions are raised.

## 5. Permissions and menu

- **Menu:** a new group `staff-menu` ("Staff", icon `briefcase`, placed after Students) with three items:
  - `vacancies` (Vacancies, `/admin/hiring/vacancies`, `megaphone`)
  - `candidates` (Candidates, `/admin/hiring/candidates`, `user-search`)
  - `staff` (Staff, `/admin/staff`, `id-card`)
- **CampusLevel:** all three are CampusLevel, so CampusAdmin gets full rights automatically.
- **New-module grants** use the existing "grant when created" seeding rule:
  - Principal: full on all three
  - Teacher, Clerk, Accountant: none
- **Lookup lists** this module needs:
  - Interviewer picker: `GET /users?pick=true` returns id and name only. Readable with `candidates:Edit`, filtered to the working campus.
  - Classes and subjects pickers: readable with `vacancies:View` (extend the existing any-permission lists).

## 6. API

All responses use `ResponseDto<T>`, and everything is campus-filtered.

**Vacancies**
- `GET /api/vacancies?status=` returns rows with stage counts `{ applied, shortlisted, interview, offered, hired }`
- `GET /api/vacancies/{id}`, `POST /api/vacancies`, `PUT /api/vacancies/{id}`
- `POST /api/vacancies/{id}/status` with `{ status }` (Open / OnHold / Closed)

**Candidates**
- `GET /api/candidates?vacancyId=&stage=&q=&page=&pageSize=`. Stage can be `active` (everything except Rejected / Withdrawn / Hired).
- `GET /api/candidates/{id}` returns the profile, interviews, offer and history.
- `POST /api/candidates` (JSON), then `POST /api/candidates/{id}/cv` (multipart: PDF, DOC/DOCX, JPG or PNG, up to 10 MB, checked by magic bytes; DOCX is a ZIP signature and DOC is OLE).
- `PUT /api/candidates/{id}`
- `POST /api/candidates/{id}/move` with `{ stage, reason }`
- `GET /api/candidates/{id}/cv` downloads the CV.

**Interviews**
- `POST /api/candidates/{id}/interviews` with `{ name, scheduledAt, interviewerUserId }`
- `PUT /api/candidates/{id}/interviews/{roundId}` with `{ name, scheduledAt, interviewerUserId, result, rating, notes }`
- `DELETE /api/candidates/{id}/interviews/{roundId}` (only while the result is Scheduled)

**Offer and hire**
- `POST /api/candidates/{id}/offer` with `{ designation, employmentType, offeredSalary, proposedJoiningDate }`
- `POST /api/candidates/{id}/offer/accept`, `/decline`, `/withdraw`
- `POST /api/candidates/{id}/hire` with `{ joiningDate, employmentType, basicSalary }`. The values default from the offer, and the endpoint returns the new staff record.

**Staff (basic)**
- `GET /api/staff?q=&status=&page=&pageSize=`
- `GET /api/staff/{id}`

**Dashboard:** a "Hiring" KPI shows the number of open vacancies, with the hint "N interviews this week", and links to the candidates page.

## 7. UI

- **Vacancies:**
  - cards with title, designation, positions filled (e.g. "1/2"), closing date, status, and stage-count chips
  - a "New vacancy" dialog
  - clicking a vacancy opens the candidates board filtered to it
- **Candidates:**
  - a **Board** view, with columns Applied / Shortlisted / Interview / Offered / Hired. Each candidate card shows name, qualification, experience and the latest rating. A "Move to…" menu replaces drag and drop.
  - a **List** view (table)
  - a "Closed" tab for Rejected and Withdrawn candidates
  - a vacancy filter, search, and an "Add candidate" dialog with CV upload and a CNIC duplicate warning
- **Candidate profile:**
  - The header shows the stage badge and the stage-specific primary action: Shortlist, Schedule interview, Make offer, Accept/Decline, or **Hire**.
  - Tabs:
    - **Overview** (details, CV download)
    - **Interviews** (timeline of rounds with result badge, stars and notes; Schedule round; Record result)
    - **Offer**
    - **Activity** (stage history)
  - The **Hire dialog** is prefilled from the offer and ends with the toast "Hired · CITY-EMP-0001".
- **Staff (basic):** a table with employee number, name, designation, category, joining date and status. It is read-only in Part 1.
- **Dashboard:** the Hiring KPI.

## 8. Errors

| Case | Response |
|---|---|
| Candidate added to a non-Open vacancy | 400 |
| Same CNIC twice in one vacancy | 409 |
| Invalid stage move | 400, listing the allowed stages |
| Reject or withdraw without a reason | 400 |
| Offer without a passed interview | 400 "Record a passed interview first." |
| Hire without an accepted offer | 400 |
| Second or concurrent hire | 409 |
| CNIC already belongs to an active staff member, on hire | 409 |
| Other campus's vacancy, candidate, CV or staff | 404 |
| Wrong CV type or too large | 400 |
| Interviewer who cannot access the campus | 400 |
| Rating outside 1–5, or missing when there is a result | 400 |

## 9. Testing

- One migration, `HiringAndStaff`. Running the seed twice must change nothing.
- An end-to-end file `tests/e2e/hiring_e2e.py`, reusing `sms_client` and the helpers. It uses temp data and cleans up. Groups:
  - **`vacancies`:** create, update, status changes, stage counts, and a candidate refused on a closed vacancy
  - **`pipeline`:** the full happy path; an invalid jump is refused; reject needs a reason; reopen works
  - **`interviews`:** rounds, result and rating validation, interviewer campus check, and an offer refused without a Pass
  - **`hire`:** staff fields and the employee number format; the vacancy becomes Filled at the position count; concurrent hires create one staff member; an active staff member's CNIC is refused
  - **`hiring-isolation`:** a campus-A user gets 404 for campus-B vacancies, candidates, CVs and staff
  - **`hiring-permissions`:** Teacher and Clerk get 403 on every hiring endpoint; Principal is allowed
  - **`cv`:** upload and download; a wrong type is refused; a renamed executable is refused
- UI: `npm run build`, then a browser walk-through as a Campus Admin covering vacancy, candidate, interview rounds, offer, hire and the staff list, on desktop, mobile and dark mode.

## 10. Part 2 outline (separate plan later)

- **Direct onboarding:** create a staff member without a candidate.
- **Staff profile tabs:** Overview, Documents (upload as for students), Checklist, Teaching, Account.
- **Onboarding checklist:** default items are documents received, contract signed, login created and orientation done. Completing every item moves the staff member from `Onboarding` to `Active`.
- **Login account:** create a user linked to the staff member, with a role and `MustChangePassword`.
- **Teaching assignments:** (session, section, subject) per teacher, plus one class teacher per section.
- **Status lifecycle:** Active ↔ OnLeave, then Resigned or Terminated (with a date and a reason). Leaving disables the linked login.

## 11. Out of scope

A public job portal or candidate self-service, email or SMS notifications, interview calendar sync, payroll, attendance (next module), and staff on several campuses.
