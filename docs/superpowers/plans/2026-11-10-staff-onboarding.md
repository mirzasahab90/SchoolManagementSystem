# Staff Onboarding (Part 2) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete staff members after hiring (or add them directly): full profile, documents and photo, a fixed checklist that activates them, a login account, teaching assignments with class teachers, and the leave / resign lifecycle.

**Architecture:**
- **Data:** `Staff` grows (profile, `UserId`, leaving fields, two manual checklist flags). New `StaffDocument` and `TeachingAssignment` are `AuditableEntity, ICampusScoped`. `Section` gains `ClassTeacherStaffId`. One migration.
- **Services:** `StaffService` becomes a partial class: `StaffService.cs` (search, profile, create, edit, delete), `.Files.cs` (photo, documents), `.Checklist.cs` (checklist + `TryActivateAsync`), `.Account.cs` (login), `.Teaching.cs` (assignments, class teacher), `.Lifecycle.cs` (status).
- **Login creation reuses `IUserService.CreateAsync`**, so every existing role-escalation and campus rule applies unchanged.
- **UI:** a staff profile page with tabs, built from the existing kit; Sections page shows teachers.

**Tech Stack:** .NET 10, ASP.NET Core, EF Core 10 (SQL Server), React 19, TypeScript, Tailwind v4, lucide-react, Python 3 e2e scripts (stdlib).

**Spec:** `docs/superpowers/specs/2026-11-10-staff-onboarding-design.md`

## Global Constraints

- **Git:** work on `main`. **No new branch, no commits.** Leave changes uncommitted.
- **Migration:** one migration, `StaffOnboarding`. If a later task changes the schema, regenerate it (remove + add).
- **Wording:** user-visible text says "Main Campus", never "HQ".
- **Employee number:** `{CampusCode}-EMP-{0000}`, sequence key `EMP` (shared with hire).
- **Files:** documents `.pdf .jpg .jpeg .png .webp` ≤ 10,485,760 bytes; photo `.jpg .jpeg .png .webp` ≤ 2,097,152 bytes; magic bytes must match (`FileSignature`); stored under `{campusId}/staff/{staffId}/` via `IFileStorage.SaveAsync(folder, …)`.
- **CNIC** digits only, 13 digits when given.
- **Bank account** masked everywhere as `****` + last 4 (`null` when empty); the full value only from `GET /api/staff/{id}/bank`.
- **Times** stored UTC and returned with Kind=Utc (use `Utc.Converter` from `HiringConfigurations.cs` for new `DateTime` columns).
- **Error copy (exact):**
  - "Select a campus."
  - "This CNIC belongs to an active staff member."
  - "This vacancy is not open."
  - "All positions are filled. Raise the positions first."
  - "This staff member already has a login."
  - "A reason is required."
  - "{Class} {Section} {Subject} is already taught by {Name}." (e.g. "Grade 6 A Mathematics is already taught by Sara Khan.")
- **E2E:** new file `tests/e2e/onboarding_e2e.py`, run as `python tests/e2e/onboarding_e2e.py <group>`. Reuse `sms_client`, `academics_e2e` helpers (`campus_admin`, `demo_campuses`) and `hiring_e2e` helpers (`make_vacancy`, `make_candidate`, `to_offer_accepted`, `interviewer`, `hire_body`, `rand_digits`, `PDF`). Add cleanup kind `staff` to `sms_client` (deleted after `candidates`, before `users`). Groups clean up after themselves; staff that cannot be deleted are resigned (`lifecycle`-style) at the end so their CNIC frees up.
- **Regression:** after each API task, `python tests/e2e/hiring_e2e.py hire hiring-isolation` and `python tests/e2e/students_e2e.py role-access` must pass. Wait 60 s between big runs (rate limiter 300/min).

## Review Focus

1. **Leaving with a login:** after resign, the user's existing access token must stop working (middleware blocks inactive users) and refresh fails. Test: Task 7 `resign_blocks_login`.
2. **Role escalation through the Account tab:** a Campus Admin must not create a login with a role they could not assign on the Users page (e.g. CampusAdmin from another campus, a Main-Campus-only role). Test: Task 5 `account_role_escalation_refused`.
3. **Concurrent assignment of the same section subject:** two requests for the same (section, subject) to two teachers → one 200, one 409, never a 500 or two rows. Test: Task 6 `concurrent_assignment_one_teacher`.
4. **Rehire after resign:** the same CNIC can be hired / directly onboarded again once the old record is Resigned, and the old record's history (assignments) stays. Test: Task 7 `rehire_same_cnic`.
5. **Teaching staff with only a CNIC:** "Documents received" must stay open until a Degree is uploaded; non-teaching staff are done with the CNIC alone. Test: Task 4 `documents_rule_by_category`.

---

## File Map

**API (SchoolManagementSystem-API/)**
- Domain:
  - Modify `Entities/Hiring/HiringEntities.cs` (Staff columns; new `StaffDocumentType`, `StaffDocument`, `TeachingAssignment`), `Entities/Academics/Section.cs` (`ClassTeacherStaffId`, `ClassTeacher` nav)
  - Create `DTOs/Hiring/StaffDtos.cs`
- Infrastructure: modify `Configurations/Hiring/HiringConfigurations.cs` (new columns, entities, CNIC index filter), section configuration (FK), DbContext DbSets; migration `StaffOnboarding`
- Application:
  - Modify `Repository/HiringRepositories.cs` (`StaffRepository` additions; `TeachingRepository`), `Repository/SchoolManagementSystemRepositories.cs`, `Repository/SectionRepository.cs` (class teacher in `SectionDto`)
  - Split `Services/Hiring/StaffService.cs` into partials listed above; modify `Services/Hiring/CandidateService.Hire.cs`, `Services/Hiring/VacancyService.cs`
  - Modify `Interfaces/IHiringServices.cs` (`IStaffService`)
- Api: modify `Controllers/HiringControllers.cs` (`StaffController`), `Controllers/SectionsController.cs`

**Tests:** create `tests/e2e/onboarding_e2e.py`; modify `tests/e2e/sms_client.py`

**UI (SchoolManagementSystem-UI/src/)**
- Modify `lib/types.ts`, `App.tsx` (`staff/:id`), `pages/staff/list.tsx`, `pages/academics/sections.tsx`, `pages/hiring/candidate.tsx` ("View staff" → profile)
- Create `pages/staff/profile.tsx`, `pages/staff/staff-form.tsx` (add / edit dialog), `pages/staff/tabs.tsx` (Documents, Checklist, Teaching, Account tabs)

---

### Task 1: Schema, direct onboarding, profile and edit

**Interfaces:**
- Enum `StaffDocumentType { Cnic, Degree, ExperienceLetter, Contract, Photo, Other }`.
- `Staff` new properties exactly as spec §3 (`Gender?`, `DateOnly? DateOfBirth`, `Address`, `PhotoPath`, `EmergencyName`, `EmergencyRelation`, `EmergencyPhone`, `Qualification`, `decimal? ExperienceYears`, `PreviousEmployer`, `BankName`, `BankAccount`, `int? UserId` + `User? User`, `DateOnly? LeftOn`, `LeaveReason`, `bool ContractSigned`, `DateTime? ContractSignedOn`, `bool OrientationDone`, `DateTime? OrientationDoneOn`, collections `Documents`, `Assignments`).
- `StaffDocument` and `TeachingAssignment` as spec §3. `Section.ClassTeacherStaffId` (int?) + `Staff? ClassTeacher`.
- Indexes: `Staff(UserId)` unique filtered `[UserId] IS NOT NULL AND [IsDeleted] = 0`; `Staff(Cnic)` unique filter changed to `[IsDeleted] = 0 AND [Cnic] IS NOT NULL AND [Status] NOT IN ('Resigned','Terminated')`; `TeachingAssignment(SectionId, SubjectId)` unique filtered `[EndedOn] IS NULL AND [IsDeleted] = 0`; `TeachingAssignment(StaffId)`.
- DTOs (`DTOs/Hiring/StaffDtos.cs`):
  - `SaveStaffRequest(string FirstName, string LastName, string? Cnic, string Phone, string? Email, Gender? Gender, DateOnly? DateOfBirth, string? Address, string? EmergencyName, string? EmergencyRelation, string? EmergencyPhone, StaffCategory Category, StaffDesignation Designation, EmploymentType EmploymentType, DateOnly JoiningDate, decimal BasicSalary, string? Qualification, decimal? ExperienceYears, string? PreviousEmployer, string? BankName, string? BankAccount)` — on PUT a null `BankAccount` keeps the stored value, `""` clears it.
  - `StaffProfileDto(StaffDetailDto Staff, StaffPersonalDto Personal, ChecklistDto Checklist, IReadOnlyList<StaffDocumentDto> Documents, IReadOnlyList<AssignmentDto> Assignments, IReadOnlyList<ClassTeacherDto> ClassTeacherOf, StaffAccountDto? Account, IReadOnlyList<StaffStatus> AllowedStatuses)`
  - `StaffPersonalDto(Gender? Gender, DateOnly? DateOfBirth, string? Address, bool HasPhoto, string? EmergencyName, string? EmergencyRelation, string? EmergencyPhone, string? Qualification, decimal? ExperienceYears, string? PreviousEmployer, string? BankName, string? BankAccountMasked, DateOnly? LeftOn, string? LeaveReason)`
  - `StaffBankDto(string? BankName, string? BankAccount)`
  - `StaffDocumentDto`, `ChecklistDto`, `StaffAccountDto`, `AssignmentDto` and `ClassTeacherDto` are declared here in Task 1 with the exact fields listed in Tasks 3–6; Task 1 fills the profile with empty lists / computed defaults, and the later tasks fill them in.
  - Spec deviation (decided here): per-subject teachers of a section come from a new `GET /api/sections/{id}/teachers` (Task 6) instead of growing `GET /api/sections/{id}`, so the existing section DTO only gains the class-teacher fields.
- `IStaffService` additions: `GetProfileAsync(int id)`, `CreateAsync(SaveStaffRequest)`, `UpdateAsync(int id, SaveStaffRequest)`, `DeleteAsync(int id)`, `GetBankAsync(int id)`; `GetAsync` stays for Part 1 callers.
- Routes: `POST /api/staff` (Add), `PUT /api/staff/{id}` (Edit), `DELETE /api/staff/{id}` (Delete), `GET /api/staff/{id}` now returns `StaffProfileDto` (View), `GET /api/staff/{id}/bank` (Edit). `GET /api/staff` gains `category`.

**Rules:** spec §4 "Direct onboarding" and "Delete". Validation: names ≤ 100, phone ≤ 50, email ≤ 256, address ≤ 300, emergency fields as spec, qualification / employer ≤ 200, bank name ≤ 100, bank account ≤ 34 (letters and digits only after removing spaces), experience 0–99, salary > 0, date of birth age 16–80, joining date not more than 1 year ahead.

- [ ] **Step 1: Write group `direct`** in `onboarding_e2e.py` (+ cleanup kind `staff`):
  - create as campus admin → 200, `employeeNo` matches `^{code}-EMP-\d{4}$`, status `Onboarding`, `sourceCandidateId` absent
  - no working campus → 400 "Select a campus."
  - CNIC of active staff → 409 "This CNIC belongs to an active staff member."; 12-digit CNIC → 400
  - `bank_masked`: profile `personal.bankAccountMasked == "****" + last4`, full value never in `GET /staff/{id}`; `GET /staff/{id}/bank` returns it
  - edit with `bankAccount: null` keeps it; `""` clears it
  - list `?category=NonTeaching` excludes a Teaching member
  - delete a fresh direct member → 200; delete a hired member → 400
- [ ] **Step 2: Run it.** → FAIL (405 / 400).
- [ ] **Step 3: Implement** entities, configuration, DbSets, migration `StaffOnboarding`, DTOs, service, controller. Rebuild after the migration.
- [ ] **Step 4: Run `direct`, plus the regression set.** → PASS.

### Task 2: Hire rules and rehire

**Interfaces:** no new signatures. `CandidateService.HireAsync` and `VacancyService.SetStatusAsync` change behaviour.

**Rules:** spec §4 "Hire (Part 1) changes". Copy `Qualification`, `ExperienceYears` from the candidate on hire.

- [ ] **Step 1: Write group `hire-rules`:**
  - `hire_full_vacancy_refused`: positions 1, two accepted candidates; first hire 200, second 400 "All positions are filled. Raise the positions first."
  - `hire_closed_vacancy_refused`: accepted candidate, vacancy Closed → 400 "This vacancy is not open."
  - `reopen_needs_free_position`: Filled → OnHold → Open → 400
  - hired staff profile has `personal.qualification` and `experienceYears` from the candidate
- [ ] **Step 2: Run it.** → FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run `hire-rules`, all `hiring_e2e` groups.** → PASS.

### Task 3: Photo and documents

**Interfaces:**
- `StaffDocumentDto(int Id, StaffDocumentType Type, string FileName, string ContentType, long SizeBytes, DateTime UploadedOn)`
- `IStaffService`: `SetPhotoAsync(int id, IFormFile)`, `GetPhotoAsync(int id) -> Result<(Stream, string ContentType)>`, `UploadDocumentAsync(int id, StaffDocumentType, IFormFile) -> Result<StaffDocumentDto>`, `GetDocumentAsync(int id, int docId) -> Result<(Stream, string, string)>`, `DeleteDocumentAsync(int id, int docId)`
- Routes as spec §5; uploads `[RequestSizeLimit(11_000_000)]`; uploads and deletes need Edit, downloads View.
- Every upload / delete calls `TryActivateAsync` (Task 4); in Task 3 it is a no-op stub.

- [ ] **Step 1: Write group `documents`:** upload PDF CNIC → listed in profile; download byte-identical with file name; `MZ` renamed `.pdf` → 400; `.docx` → 400 (not allowed for staff); 10,485,761 bytes → 400; delete → gone; photo PNG → `hasPhoto`; photo 2,097,153 bytes → 400; photo GET returns bytes.
- [ ] **Step 2: Run it.** → FAIL.
- [ ] **Step 3: Implement** (`StaffService.Files.cs`), mirroring `StudentFileService`.
- [ ] **Step 4: Run `documents` + regression.** → PASS.

### Task 4: Checklist and activation

**Interfaces:**
- `ChecklistDto(bool DocumentsReceived, IReadOnlyList<StaffDocumentType> MissingDocuments, bool ContractSigned, DateTime? ContractSignedOn, bool LoginCreated, bool OrientationDone, DateTime? OrientationDoneOn, int DoneCount)` (`DoneCount` 0–4)
- `IStaffService.SetChecklistAsync(int id, string item, bool done) -> Result<StaffProfileDto>`; `item` is `contract` or `orientation` (anything else → 400).
- `private Task TryActivateAsync(Staff staff, CancellationToken ct)` — if `Status == Onboarding` and all four are done, set `Active`. Called after documents (Task 3), checklist, account (Task 5).
- Route `POST /api/staff/{id}/checklist/{item}` `{ done }` (Edit).

- [ ] **Step 1: Write group `checklist`:**
  - new teaching member: `documentsReceived` false, `missingDocuments == ["Cnic","Degree"]`
  - `documents_rule_by_category`: teaching member with only CNIC → still missing `["Degree"]`; non-teaching member with CNIC → `documentsReceived` true
  - posting `documents` or `login` → 400
  - tick contract and orientation → `doneCount` 3 (non-teaching, CNIC uploaded, no login), status still `Onboarding`
  - (activation completes in Task 5 once a login exists: assert there)
  - untick after Active (Task 5 group) keeps `Active`
- [ ] **Step 2: Run it.** → FAIL.
- [ ] **Step 3: Implement** (`StaffService.Checklist.cs`); wire `TryActivateAsync` into document upload / delete.
- [ ] **Step 4: Run `checklist`, `documents`.** → PASS.

### Task 5: Login account

**Interfaces:**
- `StaffAccountDto(int UserId, string Email, IReadOnlyList<string> Roles, bool IsActive, bool MustChangePassword)`
- `CreateStaffLoginRequest(string Email, string Password, int RoleId)`, `LinkStaffLoginRequest(int UserId)`
- `IStaffService.CreateLoginAsync(int id, CreateStaffLoginRequest)`, `LinkLoginAsync(int id, LinkStaffLoginRequest)` → `Result<StaffProfileDto>`
- `CreateLoginAsync` builds `SaveUserRequest(FirstName, LastName, Email, Phone, IsActive: true, IsHq: false, RoleIds: [RoleId], CampusIds: [staff.CampusId], Password, MustChangePassword: true)` and calls `IUserService.CreateAsync` (inject `IUserService` into `StaffService`); its errors (403 / 409 / 400) are returned unchanged. Then sets `staff.UserId`, saves, `TryActivateAsync`.
- `LinkLoginAsync`: user active, not Main Campus, assigned to `staff.CampusId`, not linked to other non-deleted staff (409 "This user is already linked to another staff member."), else 400 "Pick an active user of this campus."
- Routes: `POST /api/staff/{id}/account` and `/account/link`, each with `[HasPermission(staff, Edit)]` **and** `[HasPermission(users, Add)]`.

- [ ] **Step 1: Write group `account`:**
  - create login with the Teacher role → `account.mustChangePassword` true, `loginCreated` true; logging in with it returns `mustChangePassword` true; the user's campus is the staff's campus
  - with CNIC + contract + orientation done (non-teaching) → status becomes `Active`; untick orientation → stays `Active`
  - second login → 409 "This staff member already has a login."
  - `account_role_escalation_refused`: Campus Admin of A assigning a Main-Campus-only role or a campus-B role → 403
  - email already used → 409
  - link: an unlinked Teacher user of the campus → 200; a user of campus B → 400; a user linked to other staff → 409
  - Principal user (has `staff:Edit`, `users:Add`) → 200; a custom role with `staff:Edit` but not `users:Add` → 403
- [ ] **Step 2: Run it.** → FAIL.
- [ ] **Step 3: Implement** (`StaffService.Account.cs`).
- [ ] **Step 4: Run `account`, `checklist`, `students_e2e.py role-access`, `academics_e2e.py roles-ownership campus-admin`.** → PASS.

### Task 6: Teaching assignments and class teacher

**Interfaces:**
- `AssignmentDto(int Id, int SectionId, string ClassName, string SectionName, int SubjectId, string SubjectName, DateOnly StartedOn)`
- `ClassTeacherDto(int SectionId, string ClassName, string SectionName)`
- `AssignmentOptionsDto(int SessionId, string SessionName, IReadOnlyList<OptionSectionDto> Sections)`; `OptionSectionDto(int SectionId, string ClassName, string SectionName, IReadOnlyList<OptionSubjectDto> Subjects)`; `OptionSubjectDto(int SubjectId, string SubjectName, int? TeacherStaffId, string? TeacherName)`
- `AssignRequest(int SectionId, IReadOnlyList<int> SubjectIds)`; `ClassTeacherRequest(int? StaffId)`
- `IStaffService`: `GetAssignmentOptionsAsync(int id)`, `AssignAsync(int id, AssignRequest)`, `EndAssignmentAsync(int id, int assignmentId)` → profile; `SetClassTeacherAsync(int sectionId, ClassTeacherRequest) -> Result<SectionDto>`.
- `SectionDto` gains trailing optional `int? ClassTeacherStaffId = null, string? ClassTeacherName = null`. New `GET /api/sections/{id}/teachers` → `IReadOnlyList<OptionSubjectDto>` for that section (`[HasAnyPermission("sections:View","students:View","staff:View")]`).
- Routes: `GET /api/staff/{id}/assignments/options` (Edit), `POST /api/staff/{id}/assignments` (Edit), `DELETE /api/staff/{id}/assignments/{assignmentId}` (Edit), `PUT /api/sections/{id}/class-teacher` (`[HasAnyPermission("sections:Edit","staff:Edit")]`).
- Current session = `Sessions.GetEffectiveCurrentAsync(staff.CampusId)`; no current session → 400 "Set a current session first."

**Rules:** spec §4 "Teaching assignments". The DB unique index turns a race into 409 through the existing handler.

- [ ] **Step 1: Write group `assignments`:**
  - one teacher gets Grade 6-A Mathematics, Grade 7-B Science and Grade 8-A Physics (three sections, different subjects) → profile lists 3
  - second teacher for Grade 6-A Mathematics → 409 with message "… is already taught by {name}."
  - subject not in the class → 400; non-teaching staff → 400; repeating own pair → no duplicate
  - options endpoint shows the first teacher's name on Grade 6-A Mathematics
  - `concurrent_assignment_one_teacher`: two threads, two teachers, same pair → codes `[200, 409]`, one current row
  - end assignment → gone from profile, pair free again
  - class teacher set → `GET /sections` shows `classTeacherName`; non-teaching staff as class teacher → 400; clear with null → 200
  - `GET /sections/{id}/teachers` lists subjects with teacher or null
  (Use `section_in` from `students_e2e` for sections; pick subjects from `GET /classes/{id}` subjects.)
- [ ] **Step 2: Run it.** → FAIL.
- [ ] **Step 3: Implement** (`StaffService.Teaching.cs`, `TeachingRepository`, section repo / controller).
- [ ] **Step 4: Run `assignments`, `academics_e2e.py sections`, regression.** → PASS.

### Task 7: Status lifecycle and rehire

**Interfaces:**
- `StaffStatusRequest(StaffStatus Status, DateOnly? Date, string? Reason)`
- `IStaffService.SetStatusAsync(int id, StaffStatusRequest) -> Result<StaffProfileDto>`; `AllowedStatuses` in the profile follows the spec §4 table (Onboarding → Resigned, Terminated; Active → OnLeave, Resigned, Terminated; OnLeave → Active, Resigned, Terminated).
- Leaving, in one transaction: `LeftOn`, `LeaveReason`; linked user `IsActive = false` + `Tokens.RevokeAllRefreshTokensAsync`; current assignments `EndedOn = date`, `EndReason = "Left"`; `Sections.ClassTeacherStaffId = null` where it was this member.
- Route `POST /api/staff/{id}/status` (Edit).

- [ ] **Step 1: Write group `lifecycle`:**
  - Active → OnLeave → Active → 200s; Onboarding → OnLeave → 400 listing allowed moves
  - resign without reason → 400 "A reason is required."; date before joining → 400
  - `resign_blocks_login`: member with login and assignments + class teacher → resign → old access token `GET /auth/me` is refused (401/403), login with password fails, profile assignments empty, section `classTeacherName` absent
  - final: Resigned → Active → 400
  - `rehire_same_cnic`: direct onboarding with the resigned member's CNIC → 200
- [ ] **Step 2: Run it.** → FAIL.
- [ ] **Step 3: Implement** (`StaffService.Lifecycle.cs`).
- [ ] **Step 4: Run every `onboarding_e2e` group so far, plus regression.** → PASS.

### Task 8: Isolation and permissions

- [ ] **Step 1: Write group `onboarding-isolation`:** a campus-B member (photo, document, login, assignment). As a campus-A admin, every one of these → 404 and changes nothing: GET / PUT / DELETE staff, bank, photo, document download / delete / upload, checklist, account create / link, options, assign, end assignment, status. Class-teacher PUT on a campus-B section → 404; campus-A section with a campus-B staff id → 400. Lists show none of B.
- [ ] **Step 2: Write group `onboarding-permissions`:** Teacher / Clerk / Accountant → 403 on list, profile, create; Principal → 200 on create and login creation.
- [ ] **Step 3: Run them; fix any real bug in the owning service.**
- [ ] **Step 4: Run all onboarding groups, all hiring groups, both older full suites.** → PASS.

### Task 9: UI — staff list, add / edit, profile (overview, documents, checklist, status)

**Files:** `lib/types.ts` (types for every DTO above; `staffDocumentTypeLabel`, `genderLabel` reuse), `App.tsx` (`staff/:id`), `pages/staff/list.tsx`, `pages/staff/staff-form.tsx`, `pages/staff/profile.tsx`, `pages/staff/tabs.tsx`.

- [ ] **Step 1: Build.**
  - **List:** "Add staff" (needs `staff:Add` + working campus), category filter, rows open the profile, remove the Part 1 read-only note.
  - **Form dialog (add / edit):** sections Basic, Personal, Emergency contact, Job & salary, Qualification, Bank. Designation auto-sets category (Teacher, Principal, Coordinator → Teaching). Edit loads the full bank value from `/bank`.
  - **Profile header:** photo (click to change, as student), name, employee no. (mono), designation, status pill, campus code; actions Edit, Mark on leave / Back from leave, Resign / Terminate (dialog: date + reason), Delete when allowed (`ConfirmDialog`).
  - **Tabs:** Overview (grouped details, masked bank), Documents (type select + upload, list, download, delete, "Missing: CNIC, Degree" hint), Checklist (4 rows, progress bar `doneCount/4`, toggles for Contract / Orientation, explanations for automatic rows).
- [ ] **Step 2: Verify.** `npm run build` exits 0. Browser as Campus Admin: add a non-teaching member, upload CNIC, tick contract + orientation; edit; mobile 375 px (no page overflow) and dark mode; no console errors.

### Task 10: UI — Account and Teaching tabs, Sections page, candidate link

**Files:** `pages/staff/tabs.tsx`, `pages/academics/sections.tsx`, `pages/hiring/candidate.tsx`.

- [ ] **Step 1: Build.**
  - **Account tab:** no login → "Create login" (email prefilled from staff, temp password with a generate button, role `Select` from `/roles?assignable=true` defaulting by designation: Teacher → Teacher, Principal → Principal, Accountant → Accountant, Clerk → Clerk) and "Link existing user" (`/users?pick=true` minus already linked); hidden without `users:Add`. With a login: email, roles, active state, "Open in Users".
  - **Teaching tab** (Teaching only): assignments grouped by section with remove buttons; "Assign classes" dialog from `/assignments/options` (pick section → subject checkboxes; subjects held by others disabled with "Taught by X"); class-teacher sections list.
  - **Sections page:** class teacher on each section card with a "Set class teacher" select (needs `sections:Edit` or `staff:Edit`); section detail shows subject → teacher or "No teacher" from `/sections/{id}/teachers`.
  - **Candidate profile:** "View staff" opens `/admin/staff/{staffId}`.
- [ ] **Step 2: Verify.** Build exits 0. Browser: full flow on one teacher — direct onboarding → CNIC + Degree → contract + orientation → create login → status Active → assign Grade 6-A Maths and Grade 7-B Science → class teacher of 6-A → Sections page shows them → resign → login disabled, assignments gone. Mobile and dark mode. Teacher login sees no Staff menu.

### Task 11: Final check

- [ ] Run every `onboarding_e2e` group, every `hiring_e2e` group, both older full suites → all PASS.
- [ ] `npm run build` → 0.
- [ ] Clean temp data (API cleanup; resign leftovers so CNICs free up).
