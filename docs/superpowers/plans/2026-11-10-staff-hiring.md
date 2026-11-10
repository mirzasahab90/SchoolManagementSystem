# Staff Hiring (Part 1) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A campus can open vacancies, add candidates with CVs, run them through a stage pipeline with multi-round interviews, make and track offers, and hire. Hiring creates a basic staff record with an employee number. Everything is campus-isolated and hidden from Teacher, Clerk and Accountant.

**Architecture:**
- **Data:** every new entity is `AuditableEntity, ICampusScoped`, so the existing DbContext filter and save guard isolate campuses. `CandidateStageHistory` is append-only.
- **Pipeline rules** live in one place, `CandidatePipeline`, a static allowed-transition table, and `CandidateService` enforces them.
- **Hire** runs in a transaction, uses the existing `SequenceRepository` (key `EMP`), and is guarded by a unique filtered index on `Staff(SourceCandidateId)`.
- **CV files** reuse `IFileStorage` and extend `FileSignature` with DOC (OLE) and DOCX (ZIP).
- **UI** uses only the existing kit.

**Tech Stack:** .NET 10, ASP.NET Core, EF Core 10 (SQL Server), React 19, TypeScript, Tailwind v4, lucide-react, and Python 3 end-to-end scripts (stdlib).

**Spec:** `docs/superpowers/specs/2026-11-10-staff-hiring-design.md` (Part 1 only)

## Global Constraints

- **Git:** work on `main`. **No new branch, no commits.** Leave changes uncommitted.
- **Migration:** one migration, `HiringAndStaff`. If a later task changes the schema, regenerate it.
- **Wording:** user-visible text says "Main Campus", never "HQ".
- **Employee number:** `{CampusCode}-EMP-{0001}`, sequence key `EMP` in `CampusSequences`.
- **CV:** `.pdf .doc .docx .jpg .jpeg .png`, at most 10,485,760 bytes. Magic bytes must agree with the extension:
  - PDF `%PDF-`
  - DOC `D0 CF 11 E0 A1 B1 1A E1`
  - DOCX `PK\x03\x04`
  - JPG and PNG as in `FileSignature`
- **CNIC** is compared and stored digits-only.
- **Error copy (exact):**
  - "Record a passed interview first."
  - "A reason is required."
  - "This vacancy is not open."
  - "This candidate is already hired."
  - "This CNIC belongs to an active staff member."
- **End-to-end tests:**
  - New file `tests/e2e/hiring_e2e.py`, run as `python tests/e2e/hiring_e2e.py <group>`.
  - It reuses `sms_client` and imports `campus_admin`, `demo_campuses` and `role_detail` / `actions` from `academics_e2e`.
  - Groups clean up after themselves.
  - Add kinds `candidates` and `vacancies` to `sms_client.cleanup`, deleted before `users`.
- **Regression:** after each API task, `python tests/e2e/academics_e2e.py isolation seed review-fixes` and `python tests/e2e/students_e2e.py role-access student-isolation` must still pass. Wait 60 s between big runs (rate limiter).

## Review Focus

1. **Concurrent hire.** Two hire clicks on the same candidate must produce exactly one staff member and one 409, never two staff or a 500. Test: Task 5 `concurrent_hire_creates_one_staff`.
2. **Reopening into a closed vacancy.** Reopening a Rejected candidate whose vacancy is Closed or Filled must return 400 "This vacancy is not open." Test: Task 3 `reopen_needs_open_vacancy`.
3. **Deleting a decided round.** Deleting a round whose result is Pass or Fail must be refused (400), so an offer cannot lose its evidence. Test: Task 4 `cannot_delete_decided_round`.
4. **Inactive interviewer.** Scheduling a round with a deactivated user must return 400, and that user must not appear in the picker. Test: Task 4 `inactive_interviewer_refused`.
5. **Re-adding a deleted candidate.** After a candidate is deleted, adding the same CNIC to that vacancy must succeed. The uniqueness check ignores soft-deleted rows. Test: Task 3 `cnic_reusable_after_delete`.

---

## File Map

**API**
- **Domain**
  - Create: `Entities/Hiring/HiringEntities.cs` (enums plus `Vacancy`, `Candidate`, `CandidateStageHistory`, `InterviewRound`, `Offer`, `Staff`), `DTOs/Hiring/HiringDtos.cs`
  - Modify: `Constants/AppModules.cs`
- **Infrastructure**
  - Create: `Configurations/Hiring/HiringConfigurations.cs`, migration `HiringAndStaff`
  - Modify: DbContext (DbSets), `Seeding/DatabaseSeeder.cs` (menu), `Seeding/RoleDefaultsSeeder.cs` (Principal grants)
- **Application**
  - Create:
    - `Repository/HiringRepositories.cs` (`VacancyRepository`, `CandidateRepository`, `StaffRepository`)
    - `Services/Hiring/{CandidatePipeline,VacancyService,CandidateService,CandidateService.Interviews,CandidateService.Hire,StaffService}.cs`
    - `Interfaces/{IVacancyService,ICandidateService,IStaffService}.cs`
  - Modify:
    - `Services/Students/StudentFileService.cs` (`FileSignature` adds `.doc` / `.docx` detection and content types)
    - `Repository/SchoolManagementSystemRepositories.cs`
    - `Services/Dashboard/DashboardService.cs`
    - `Repository/UserRepository.cs` (`PickAsync`)
- **Api**
  - Create: `Controllers/{Vacancies,Candidates,Staff}Controller.cs`
  - Modify: `Controllers/UsersController.cs` (`?pick=true`), `Controllers/{Classes,Subjects}Controller.cs` (add `vacancies:View` to any-permission lists), `Startup.cs`

**Tests**
- Create: `tests/e2e/hiring_e2e.py`
- Modify: `tests/e2e/sms_client.py` (cleanup kinds)

**UI**
- Create: `src/pages/hiring/{vacancies,candidates,candidate}.tsx`, `src/pages/staff/list.tsx`
- Modify: `src/lib/types.ts`, `src/App.tsx`, `src/components/app-shell.tsx` (icons), `src/pages/dashboard.tsx`

---

### Task 1: Schema, migration, menu and grants

**Interfaces (produces):**
- **Enums:**
  - `StaffDesignation { Teacher, Principal, Coordinator, Accountant, Clerk, Librarian, Driver, Guard, Cleaner, Other }`
  - `StaffCategory { Teaching, NonTeaching }`
  - `VacancyStatus { Open, OnHold, Closed, Filled }`
  - `CandidateStage { Applied, Shortlisted, Interview, Offered, Hired, Rejected, Withdrawn }`
  - `CandidateSource { WalkIn, Referral, Email, JobPortal, Other }`
  - `InterviewResult { Scheduled, Pass, Fail, Hold }`
  - `EmploymentType { Permanent, Contract, Visiting, PartTime }`
  - `OfferStatus { Pending, Accepted, Declined, Withdrawn }`
  - `StaffStatus { Onboarding, Active, OnLeave, Resigned, Terminated }`
- **Entities** with the columns of spec §3. Navigations:
  - `Candidate.Vacancy`, `Candidate.Interviews`, `Candidate.Offers`
  - `InterviewRound.Interviewer` (`User`)
  - `Staff.SourceCandidate`
- **Indexes,** all filtered on `[IsDeleted] = 0`:
  - unique `Candidates(VacancyId, Cnic)`, additionally filtered on `[Cnic] IS NOT NULL`
  - unique `Staff(CampusId, EmployeeNo)`
  - unique `Staff(Cnic)`, additionally filtered on `[Cnic] IS NOT NULL`
  - unique `Staff(SourceCandidateId)`, additionally filtered on `[SourceCandidateId] IS NOT NULL`
- **`AppModules`:** `StaffMenu = "staff-menu"`, `Vacancies = "vacancies"`, `Candidates = "candidates"`, `Staff = "staff"`.
- **Menu:**
  - group `staff-menu` ("Staff", `briefcase`, order 6) with children:
    - `vacancies` (Vacancies, `/admin/hiring/vacancies`, `megaphone`, 1)
    - `candidates` (Candidates, `/admin/hiring/candidates`, `user-search`, 2)
    - `staff` (Staff, `/admin/staff`, `id-card`, 3)
  - all three children are CampusLevel
  - Settings moves to order 7
- **`RoleDefaultsSeeder.Defaults`:** Principal gets Full on all three. The "grant when created" path applies it to existing databases.
- **DTOs** (`HiringDtos.cs`), used by every later task:
  - `StageCountsDto(int Applied, int Shortlisted, int Interview, int Offered, int Hired)`
  - `VacancyDto(int Id, int CampusId, string CampusCode, string Title, StaffDesignation Designation, StaffCategory Category, int Positions, int Hired, IReadOnlyList<int> ClassIds, IReadOnlyList<int> SubjectIds, string? Requirements, DateOnly? ClosingDate, VacancyStatus Status, StageCountsDto Counts)`
  - `SaveVacancyRequest(string Title, StaffDesignation Designation, StaffCategory Category, int Positions, IReadOnlyList<int>? ClassIds, IReadOnlyList<int>? SubjectIds, string? Requirements, DateOnly? ClosingDate)`
  - `VacancyStatusRequest(VacancyStatus Status)`
  - `CandidateListItemDto(int Id, int VacancyId, string VacancyTitle, string FullName, string Phone, string? Qualification, decimal? ExperienceYears, CandidateStage Stage, int? LatestRating, bool HasCv, string CampusCode)`
  - `SaveCandidateRequest(int VacancyId, string FullName, string? Cnic, string Phone, string? Email, string? Qualification, decimal? ExperienceYears, decimal? CurrentSalary, decimal? ExpectedSalary, CandidateSource Source)`
  - `MoveCandidateRequest(CandidateStage Stage, string? Reason)`
  - `InterviewRoundDto(int Id, string Name, DateTime ScheduledAt, int InterviewerUserId, string InterviewerName, InterviewResult Result, int? Rating, string? Notes)`
  - `ScheduleInterviewRequest(string Name, DateTime ScheduledAt, int InterviewerUserId)`
  - `UpdateInterviewRequest(string Name, DateTime ScheduledAt, int InterviewerUserId, InterviewResult Result, int? Rating, string? Notes)`
  - `OfferDto(int Id, StaffDesignation Designation, EmploymentType EmploymentType, decimal OfferedSalary, DateOnly ProposedJoiningDate, OfferStatus Status, DateTime? RespondedOn)`
  - `MakeOfferRequest(StaffDesignation Designation, EmploymentType EmploymentType, decimal OfferedSalary, DateOnly ProposedJoiningDate)`
  - `StageHistoryDto(CandidateStage? FromStage, CandidateStage ToStage, string? Reason, DateTime OccurredOn, string? ByUserName)`
  - `CandidateDetailDto(int Id, int CampusId, string CampusCode, int VacancyId, string VacancyTitle, string FullName, string? Cnic, string Phone, string? Email, string? Qualification, decimal? ExperienceYears, decimal? CurrentSalary, decimal? ExpectedSalary, CandidateSource Source, CandidateStage Stage, string? StageReason, bool HasCv, string? CvFileName, int? StaffId, string? ExistingStaffWarning)`
  - `CandidateProfileDto(CandidateDetailDto Candidate, IReadOnlyList<InterviewRoundDto> Interviews, OfferDto? Offer, IReadOnlyList<StageHistoryDto> History, IReadOnlyList<CandidateStage> AllowedMoves)`
  - `HireRequest(DateOnly JoiningDate, EmploymentType EmploymentType, decimal BasicSalary)`
  - `StaffListItemDto(int Id, string EmployeeNo, string FullName, StaffDesignation Designation, StaffCategory Category, DateOnly JoiningDate, StaffStatus Status, string CampusCode)`
  - `StaffDetailDto(int Id, int CampusId, string CampusCode, string EmployeeNo, string FirstName, string LastName, string FullName, string? Cnic, string Phone, string? Email, StaffCategory Category, StaffDesignation Designation, EmploymentType EmploymentType, DateOnly JoiningDate, decimal BasicSalary, StaffStatus Status, int? SourceCandidateId)`
  - `UserPickDto(int Id, string FullName)`
  - The existing `PagedResult<T>` is reused.

- [ ] **Step 1: Write group `schema`** in `hiring_e2e.py`. It asserts:
  - the HQ top menu equals `dashboard, campuses, academics, students-menu, staff-menu, settings`
  - `staff-menu` children are `vacancies, candidates, staff`
  - Principal has full rights on all three
  - CampusAdmin has full rights on all three
  - Teacher, Clerk and Accountant have none

  Update the expected menu in `academics_e2e.seed` and `students_e2e.schema` to include `staff-menu`.
- [ ] **Step 2: Run it.** → FAIL (no `staff-menu`).
- [ ] **Step 3: Implement** the entities, configurations, DbSets, constants, menu and grants. Generate the migration with `dotnet ef migrations add HiringAndStaff --project …Infrastructure --startup-project …Infrastructure`. Rebuild the API after adding it, so `--no-build` runs include it.
- [ ] **Step 4: Restart the API twice and run `schema`, plus the two updated seed groups.** → PASS. The Principal grant count does not grow on the second start.

### Task 2: Vacancies API

**Interfaces:**
- `VacancyRepository`:
  - `ListAsync(VacancyStatus? status, CancellationToken)` → `List<VacancyDto>`, with counts computed in SQL and `Hired` = count of Hired candidates
  - `GetTrackedAsync(int id, CancellationToken)`
- `IVacancyService`:
  - `ListAsync(VacancyStatus? status)`
  - `GetAsync(int id)`
  - `CreateAsync(SaveVacancyRequest)`
  - `UpdateAsync(int id, SaveVacancyRequest)`
  - `SetStatusAsync(int id, VacancyStatusRequest)`
- **Controller** `api/vacancies`: View for GET, Add for POST, Edit for PUT and `POST {id}/status`.

**Rules:**
- **Create:**
  - It needs `ActiveCampusId` (400 "Select a campus.").
  - Title is required, and `Positions` must be at least 1.
  - Class and subject ids must be visible to the request (400 otherwise).
- **Update:** `Positions` cannot drop below `Hired` (400).
- **SetStatus:**
  - Allowed targets are Open, OnHold and Closed. `Filled` cannot be set by hand (400).
  - Reopening a Filled vacancy needs `Positions > Hired` (400 otherwise).

- [ ] **Step 1: Write group `vacancies`.** It covers:
  - create as a campus admin → 200 with `status == "Open"` and all counts 0
  - update the title and positions
  - `positions_must_be_positive`: `positions: 0` → 400. Positions below the hired count are tested in Task 5, once hiring exists.
  - set OnHold, then Closed, then Open → 200
  - set Filled → 400
  - list filtered by `status=Open` contains it
- [ ] **Step 2: Run it.** → FAIL (404).
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run it, plus the regression suite.** → PASS.

### Task 3: Candidates, pipeline moves and CVs

**Interfaces:**
- `CandidatePipeline.Allowed(CandidateStage from) -> IReadOnlyList<CandidateStage>`, the manual moves from spec §4:
  - Applied → Shortlisted, Rejected, Withdrawn
  - Shortlisted → Interview, Rejected, Withdrawn
  - Interview → Rejected, Withdrawn
  - Offered → Rejected, Withdrawn
  - Rejected / Withdrawn → Applied

  Offered and Hired are reached only through the offer and hire endpoints.
- `CandidateRepository`:
  - `SearchAsync(int? vacancyId, string? stage, string? q, int page, int pageSize, CancellationToken)` → `PagedResult<CandidateListItemDto>`. A `stage` of `"active"` excludes Rejected, Withdrawn and Hired. `q` matches name, phone digits, CNIC digits and email.
  - `GetTrackedAsync(int id, CancellationToken)` (includes Vacancy, Interviews, Offers)
  - `CnicTakenInVacancyAsync(int vacancyId, string cnic, int? excludeId, CancellationToken)`
  - `ActiveStaffWithCnicAsync(string cnic, CancellationToken) -> string?` (the employee number, ignoring campus filters)
  - `GetHistoryAsync(int candidateId, CancellationToken)`
- `ICandidateService`:
  - `SearchAsync`
  - `GetProfileAsync(int id) -> Result<CandidateProfileDto>` (sets `ExistingStaffWarning` to `"Already on staff as {EmployeeNo}"` when the CNIC matches active staff; `AllowedMoves` comes from the pipeline)
  - `CreateAsync(SaveCandidateRequest)`
  - `UpdateAsync(int id, SaveCandidateRequest)` (the vacancy cannot change once the stage is beyond Applied)
  - `MoveAsync(int id, MoveCandidateRequest)`
  - `DeleteAsync(int id)` (Applied, Rejected or Withdrawn only; anything else → 400)
  - `SetCvAsync(int id, IFormFile file)`
  - `GetCvAsync(int id) -> Result<(Stream, string ContentType, string FileName)>`
- **Controller** `api/candidates`:
  - `GET` (q, vacancyId, stage, page, pageSize ≤ 100)
  - `GET {id}`
  - `POST` (Add)
  - `PUT {id}` (Edit)
  - `DELETE {id}` (Delete)
  - `POST {id}/move` (Edit)
  - `POST {id}/cv` (Edit, `[RequestSizeLimit(11_000_000)]`)
  - `GET {id}/cv` (View)
- `FileSignature.Detect` also returns `.doc` (OLE header) and `.docx` (ZIP header). `ContentType` maps:
  - `.doc` → `application/msword`
  - `.docx` → `application/vnd.openxmlformats-officedocument.wordprocessingml.document`

**Rules:**
- **Create:** the vacancy must be Open (400 "This vacancy is not open."); a duplicate CNIC in the vacancy → 409; the stage starts at Applied; write a history row (from null to Applied).
- **Move:**
  - The target must be in `Allowed(from)`. Otherwise → 400 "Allowed next stages: …".
  - Rejected and Withdrawn need a reason (400 "A reason is required.").
  - A reopen to Applied needs a reason and an Open vacancy.
  - Every move writes history.

- [ ] **Step 1: Write group `pipeline`** (moves without interviews). It covers:
  - add a candidate → Applied with history `[Applied]`
  - `allowed_moves_listed`: the profile's `allowedMoves == ["Shortlisted","Rejected","Withdrawn"]`
  - Applied → Interview → 400; Applied → Shortlisted → 200
  - reject without a reason → 400; with a reason → 200; reopen → Applied
  - `reopen_needs_open_vacancy`: reject, close the vacancy, reopen → 400 "This vacancy is not open."
  - `cnic_reusable_after_delete`: the same CNIC twice → 409; delete the first, add again → 200
  - add to a Closed vacancy → 400
  - `existing_staff_warning` (CNIC of an active staff member; uses Task 5, so it stays pending until then)
- [ ] **Step 2: Write group `cv`.** It covers:
  - upload a PDF → `hasCv` true; download it byte-identical with `Content-Disposition` carrying the file name
  - DOCX (PK header) → 200
  - `MZ` bytes renamed to `.pdf` → 400
  - a `.txt` → 400
  - 10,485,761 bytes → 400
- [ ] **Step 3: Run both.** → FAIL.
- [ ] **Step 4: Implement.**
- [ ] **Step 5: Run both, plus the regression suite.** → PASS (except `existing_staff_warning`, which is marked as waiting for Task 5).

### Task 4: Interviews and offers

**Interfaces:**
- `UserRepository.PickAsync(int campusId, CancellationToken) -> List<UserPickDto>`: active users who are Main Campus users or are assigned to that campus, ordered by name.
- `UsersController GET ?pick=true` uses `[HasAnyPermission("users:View", "candidates:Edit")]`. It returns `UserPickDto[]` for the working campus and requires one.
- `ICandidateService` additions:
  - `ScheduleInterviewAsync(int id, ScheduleInterviewRequest)`
  - `UpdateInterviewAsync(int id, int roundId, UpdateInterviewRequest)`
  - `DeleteInterviewAsync(int id, int roundId)`
  - `MakeOfferAsync(int id, MakeOfferRequest)`
  - `RespondOfferAsync(int id, OfferStatus response)` (Accepted, Declined or Withdrawn)

  Each returns `Result<CandidateProfileDto>`.
- **Routes** (Edit permission):
  - `POST {id}/interviews`, `PUT {id}/interviews/{roundId}`, `DELETE {id}/interviews/{roundId}`
  - `POST {id}/offer`, `POST {id}/offer/accept`, `POST {id}/offer/decline`, `POST {id}/offer/withdraw`

**Rules:**
- **Schedule** needs the stage to be Shortlisted or Interview; a Shortlisted candidate moves to Interview, with history. The interviewer must be active and allowed on the candidate's campus (400 otherwise).
- **Update:** a result other than Scheduled needs a rating from 1 to 5 (400 otherwise).
- **Delete:** only while the result is Scheduled (400 otherwise).
- **Make offer:**
  - The stage must be Interview, and there must be at least one Pass round (400 "Record a passed interview first.").
  - Any Pending offer is withdrawn first.
  - The stage becomes Offered, with history.
  - The salary must be greater than 0.
- **Respond:** only a Pending offer; `RespondedOn` is set to now.

- [ ] **Step 1: Write group `interviews`.** It covers:
  - schedule on a Shortlisted candidate → stage Interview
  - `inactive_interviewer_refused`: a deactivated user → 400, and absent from `GET /users?pick=true`
  - an interviewer from another campus only → 400
  - a result of Pass without a rating → 400; with rating 4 → 200
  - `cannot_delete_decided_round` → 400
  - deleting a Scheduled round → 200
  - an offer before any Pass → 400 "Record a passed interview first."; after a Pass → stage Offered, offer Pending
  - accept → Accepted; accepting again → 400
- [ ] **Step 2: Run it.** → FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run it, plus the regression suite.** → PASS.

### Task 5: Hire and basic staff

**Interfaces:**
- `StaffRepository`:
  - `SearchAsync(string? q, StaffStatus? status, int page, int pageSize, CancellationToken)` → `PagedResult<StaffListItemDto>`
  - `GetDetailAsync(int id, CancellationToken)`
  - `CnicActiveAsync(string cnic, CancellationToken)` (ignores filters; active means not Resigned or Terminated)
- `ICandidateService.HireAsync(int id, HireRequest) -> Result<StaffDetailDto>`
- `IStaffService`: `SearchAsync`, `GetAsync`
- **Routes:**
  - `POST api/candidates/{id}/hire` (permissions `candidates:Edit` **and** `staff:Add`, so the action carries two `[HasPermission]` attributes)
  - `GET api/staff`, `GET api/staff/{id}` (staff View)

**Hire algorithm** (one transaction):
1. Load the candidate (filtered).
2. Stage Hired or `StaffId` set → 409 "This candidate is already hired."
3. Stage must be Offered, with the latest offer Accepted (400 otherwise).
4. A CNIC that matches active staff → 409 "This CNIC belongs to an active staff member."
5. Take `n = Sequences.NextAsync(campusId, "EMP")` and build `EmployeeNo = $"{code}-EMP-{n:0000}"`, skipping numbers already taken.
6. Split the name: the last word is LastName (or "-" if there is only one word), the rest is FirstName.
7. Add the Staff record: status Onboarding, `SourceCandidateId` = the candidate, designation from the offer, and the category from the vacancy.
8. Set the candidate's stage to Hired and record the `StaffId`, with history.
9. If the vacancy's hired count reaches `Positions`, set the vacancy to Filled.
10. Save and commit. A unique-index race surfaces as 409 through the existing handler.

- [ ] **Step 1: Write group `hire`.** It covers:
  - the full flow, then hire → 200 with `employeeNo` matching `^CITY-EMP-\d{4}$` (use the campus code dynamically), status Onboarding, and the salary from the request
  - the candidate's stage becomes Hired
  - `vacancy_filled_at_positions`: with `positions = 1`, the vacancy becomes Filled; reopening it → 400
  - hiring twice → 409
  - `concurrent_hire_creates_one_staff`: two threads → status codes `[200, 409]`, and the staff list has exactly one row for that candidate
  - `active_staff_cnic_refused`: a second candidate with the same CNIC in another vacancy, taken to Offered and Accepted, then hired → 409
  - the profile of a candidate with that CNIC shows `existingStaffWarning` (this completes the pending Task 3 check)
  - `GET /staff?q=<name>` finds the new staff member
- [ ] **Step 2: Run it.** → FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run `hire` and `pipeline` (now fully), plus the regression suite.** → PASS.

### Task 6: Isolation, permissions and dashboard

- [ ] **Step 1: Write group `hiring-isolation`.** Set up a campus-B vacancy and candidate (with a CV), plus campus-B staff (hired). As a campus-A admin, every one of these → 404 and changes nothing:
  - GET/PUT vacancy and its status
  - GET/PUT/DELETE candidate
  - move
  - interviews
  - offer
  - hire
  - CV download
  - GET staff
  - the lists with `q` return none of them
- [ ] **Step 2: Write group `hiring-permissions`.** As Teacher, Clerk and Accountant users: every list and detail endpoint → 403. As a Principal user: `GET /vacancies` → 200.
- [ ] **Step 3: Write group `dashboard-hiring`.** The KPI id `hiring` has a value equal to the count of Open vacancies, and the hint matches `^\d+ interviews? this week$`. It is absent for a Teacher user.
- [ ] **Step 4: Run them.** The dashboard group fails. Isolation and permission failures are real bugs: fix them in the owning service.
- [ ] **Step 5: Implement** the KPI, shown only when the user has `vacancies:View`. "This week" means Monday to Sunday (UTC) of the current week, counting rounds whose result is Scheduled.
- [ ] **Step 6: Run all `hiring_e2e` groups, plus both full older suites.** → all PASS.

### Task 7: UI foundation and Vacancies page

**Files:** `types.ts` (every DTO above as TS types, plus label maps `designationLabel`, `stageLabel`, `stageOrder`), `App.tsx` routes (`hiring/vacancies`, `hiring/candidates`, `hiring/candidates/:id`, `staff`), `app-shell.tsx` icons (`briefcase`, `megaphone`, `user-search` → `UserSearch`, `id-card` → `IdCard`), dashboard `kpiStyle.hiring` (Briefcase, warning), `pages/hiring/vacancies.tsx`.

- [ ] **Step 1: Vacancies page.**
  - Header with a "New vacancy" button (needs Add and a working campus).
  - Status filter tabs: Open, On hold, Closed, Filled, All.
  - One card per vacancy:
    - title and designation pill
    - "hired/positions filled" with a progress bar
    - closing date, shown with a warning tone when it has passed
    - stage chips (Applied n · Shortlisted n · Interview n · Offered n)
    - a status pill
    - a menu with Edit and Put on hold / Close / Reopen
  - Clicking a card opens `/admin/hiring/candidates?vacancyId=…`.
  - The dialog has title, designation, category (auto-set from the designation: Teacher, Principal and Coordinator are Teaching), positions, classes (multi `ChoiceCard`), subjects, requirements and closing date.
- [ ] **Step 2: Verify.** `npm run build` exits with 0. In the browser as a Campus Admin: create, edit and close a vacancy. No console errors.

### Task 8: Candidates board and list

**Files:** `pages/hiring/candidates.tsx`.

- [ ] **Step 1: Build the page.**
  - **Toolbar:** vacancy `Select` (from the query string), search, a Board/List toggle (remembered in `localStorage`, inside try/catch), and an "Add candidate" button.
  - **Board:** 5 columns (Applied, Shortlisted, Interview, Offered, Hired), each scrolling horizontally on mobile. Each card shows name, vacancy title (when not filtered), qualification, experience, latest rating as stars, and a CV icon.
    - The card menu "Move to…" lists `allowedMoves`. Fetch the profile on open, or apply the static client copy of the pipeline table.
    - Reject and Withdraw ask for a reason in a small modal.
    - Clicking a card opens the profile.
  - **Closed tab:** Rejected and Withdrawn, with their reason.
  - **List view:** table columns Candidate, Vacancy, Stage pill, Rating, Phone, Source.
  - **Add candidate dialog:**
    - vacancy (Open vacancies only), name, CNIC, phone, email, qualification, experience, current/expected salary, source, CV file (client-side type and 10 MB checks)
    - on CNIC blur, show a warning when it matches staff, using the created profile's `existingStaffWarning` after save as a toast
- [ ] **Step 2: Verify.** The build exits with 0. In the browser: add two candidates with CVs, move one to Shortlisted, reject one with a reason, and check the Closed tab. Check mobile width (375 px) and that the board does not overflow the page.

### Task 9: Candidate profile (interviews, offer, hire)

**Files:** `pages/hiring/candidate.tsx`.

- [ ] **Step 1: Build the page.**
  - **Header:** name, vacancy, stage pill, a CV download button, and the primary action for the stage:
    - Applied → Shortlist
    - Shortlisted → Schedule interview
    - Interview → Make offer (disabled with a tooltip when there is no Pass round)
    - Offered and Pending → Accept / Decline
    - Offered and Accepted → **Hire**
  - The secondary menu lists the remaining `allowedMoves`.
  - **Tabs:**
    - **Overview:** details, plus the existing-staff warning banner
    - **Interviews:**
      - a timeline of rounds (name, date/time, interviewer, result pill, ★ rating, notes)
      - "Schedule round" dialog: name preset `Select` (Screening, Demo class, Principal, Final, Other → text input), date-time input, and interviewer `Select` from `/users?pick=true`
      - "Record result" dialog: result, a star rating input (5 buttons), and notes
      - delete only on Scheduled rounds
    - **Offer:**
      - the current offer card
      - a "Make offer" dialog with designation, employment type, salary and joining date, prefilled from the vacancy and the expected salary
    - **Activity:** the stage history timeline
  - **Hire dialog:** joining date, employment type and salary, prefilled from the offer. On success, toast "Hired · {employeeNo}" and link to `/admin/staff`.
- [ ] **Step 2: Verify.** The build exits with 0. In the browser, run the full flow on one candidate: shortlist, schedule two rounds, record Pass with 4★, make an offer, accept, hire. Then check that the staff list shows the new person. Check dark mode and mobile.

### Task 10: Staff list, dashboard and final check

**Files:** `pages/staff/list.tsx`, `pages/dashboard.tsx`.

- [ ] **Step 1: Staff list.**
  - Search and a status filter.
  - Table columns: Employee no. (mono), Name with avatar, Designation, Category, Joining date, Status pill (`Onboarding` warning, `Active` success).
  - A read-only note: "Full profiles and onboarding are coming next."
- [ ] **Step 2: Dashboard.** Add the Hiring KPI icon and accent. Quick actions are not changed.
- [ ] **Step 3: Final verification.**
  - The build exits with 0.
  - Run every `hiring_e2e` group, both older full suites, and `students_e2e.role-access` → all PASS.
  - Browser walk-through as Campus Admin (Main Campus wording, no "HQ" text), plus a Teacher login that sees no Staff menu.
  - Clean up temp data.
