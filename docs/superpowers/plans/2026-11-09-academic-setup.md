# Campus Admin and Academic Setup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give every campus a Campus Admin who runs the whole campus. Add HQ-default plus campus-owned roles, sessions, classes and subjects. Add campus sections, a My campus page and a first-sign-in password change.

**Architecture:**
- A new `ICampusOwnable` interface (`int? CampusId`, null = HQ) gets an EF read filter and a save guard, beside the existing `ICampusScoped` logic in `SchoolManagementSystemDbContext`. `Role`, `AcademicSession`, `SchoolClass` and `Subject` implement it. `Section` implements `ICampusScoped`.
- Services enforce the ownership rules (HQ-only writes for HQ rows, no permission escalation, HQ-assign-only roles) on top of the filter, using the pattern `RoleService`, `UserService` and `CampusService` already follow.
- The UI adds pages built only from the existing `sms-ui.tsx` and `modal.tsx` kit.

**Tech Stack:** .NET 10, ASP.NET Core, EF Core 10 (SQL Server), React 19, TypeScript, Tailwind v4, lucide-react, and Python 3 end-to-end scripts (stdlib only).

**Spec:** `docs/superpowers/specs/2026-11-09-academic-setup-design.md` (revision 2)

## Global Constraints

- Branch `feature/campus-admin-academics`, cut from `main`. Commit after every task.
- One migration for the whole feature: `CampusAdminAndAcademics`. Add it in Task 1. If a later task changes the schema, regenerate it rather than adding a second one.
- **Ownership:** `CampusId == null` means an HQ row.
  - Read: HQ on "All campuses" sees all rows. Anyone with a working campus sees HQ rows plus that campus's rows.
  - Write HQ row: HQ only, otherwise 403.
  - Write campus row: HQ, or a user who can access that campus.
- Codes are trimmed and stored upper-case. Names are trimmed. Uniqueness is checked across "HQ rows ∪ same-campus rows", and service checks return 409.
- Enums serialize as strings (`JsonStringEnumConverter` is already registered).
- **Error copy (exact):**
  - "This session is closed."
  - "Reopen the session first."
  - "Select a campus."
  - "You can only grant permissions you have."
  - "Only HQ can assign this role."
  - "Only HQ can change head-office items."
- **UI:** no new colours, fonts or primitives. Owner pill: `HQ` uses `tone="info"`, a campus code uses `tone="primary"`.
- **End-to-end tests:**
  - Run with `python tests/e2e/academics_e2e.py <group>`.
  - Base URL is env `SMS_API`, default `https://localhost:7150/api`. TLS verification is off for localhost only.
  - Admin credentials are read from `DatabaseSeeder.cs`.
  - Temporary passwords are generated with `secrets.token_urlsafe`.
  - Every group cleans up what it creates.
- No passwords are seeded or logged beyond the existing dev SuperAdmin.

## Review Focus

1. **Escalation through a campus role.** A Campus Admin who edits a campus role must not be able to tick `campuses` Delete or any permission they lack, and the server must refuse it even if the UI is bypassed. Test: Task 4 `cannot_grant_unheld_permission`.
2. **Assigning a role across campuses.** A campus-A role given to a user who also belongs to campus B must be refused, so the role's permissions never leak into B. Test: Task 4 `campus_role_only_for_its_campus_users`.
3. **Campus override of the current session.** When campus A sets its own current session, campus B and HQ must still see the HQ current session. Test: Task 6 `campus_current_overrides_only_that_campus`.
4. **A section using another campus's class.** A section on campus A that references a class owned by campus B must get 400, not succeed because the id exists. Test: Task 9 `foreign_class_or_session_refused`.
5. **Stale must-change-password.** After changing the password, `/auth/me` must return `mustChangePassword: false`, and the old password must stop working. Test: Task 3 `change_password_clears_flag`.

---

## File Map

**Domain**
- Create:
  - `Common/ICampusOwnable.cs`
  - `Entities/Academics/{AcademicEnums,AcademicSession,SchoolClass,Subject,ClassSubject,Section}.cs`
  - `DTOs/Academics/AcademicDtos.cs`
- Modify:
  - `Entities/Identity/Role.cs` (CampusId, HqAssignOnly)
  - `Entities/Identity/Module.cs` (CampusLevel)
  - `Constants/AppModules.cs`, `Constants/SystemRoles.cs` (`CampusAdmin`)
  - `DTOs/Org/RolePermissionDtos.cs`, `DTOs/Org/CampusDtos.cs`, `DTOs/Auth/AuthDtos.cs`

**Infrastructure**
- Create:
  - `Configurations/Academics/*Configuration.cs`
  - `Seeding/AcademicSeeder.cs`
  - `Seeding/RoleDefaultsSeeder.cs`
  - migration `CampusAdminAndAcademics`
- Modify:
  - `SchoolManagementSystemDbContext.cs`
  - `Configurations/Identity/RoleConfiguration.cs`
  - `Seeding/DatabaseSeeder.cs`

**Application**
- Create:
  - `Repository/{Session,Class,Subject,Section}Repository.cs`
  - `Interfaces/{IAcademicSessionService,IClassService,ISubjectService,ISectionService}.cs`
  - `Services/Academics/*.cs`
- Modify:
  - `Repository/{RoleRepository,UserRepository,CampusRepository,SchoolManagementSystemRepositories}.cs`
  - `Services/{Auth/AuthService,Roles/RoleService,Users/UserService,Org/CampusService,Dashboard/DashboardService}.cs`
  - `Utilities/Security/MenuBuilder.cs`
  - `Interfaces/IAuthService.cs`

**Api**
- Create: `Controllers/{AcademicSessions,Classes,Subjects,Sections}Controller.cs`
- Modify: `Controllers/AuthController.cs`, `Startup.cs`

**Tests**
- Create: `tests/e2e/sms_client.py`, `tests/e2e/academics_e2e.py`

**UI**
- Create:
  - `src/components/owner-pill.tsx`
  - `src/pages/change-password.tsx`
  - `src/pages/my-campus.tsx`
  - `src/pages/academics/{sessions,classes,subjects,sections}.tsx`
- Modify:
  - `src/lib/types.ts`, `src/lib/auth.tsx`, `src/App.tsx`
  - `src/components/app-shell.tsx`
  - `src/pages/{dashboard,campuses,users,roles}.tsx`

---

### Task 1: Schema, ownership filter and migration

**Files:** the Domain and Infrastructure files above, except the seeders. Also `tests/e2e/sms_client.py` and `tests/e2e/academics_e2e.py` (group `schema`).

**Interfaces (produces):**

**Interface and new columns**
- `ICampusOwnable { int? CampusId { get; set; } }`
- `Role` gets `int? CampusId`, `Campus? Campus` and `bool HqAssignOnly`, and implements `ICampusOwnable`
- `Module` gets `bool CampusLevel`

**Enums**
- `SessionStatus { Planned, Active, Closed }`
- `ClassStage { PrePrimary, Primary, Middle, Secondary, HigherSecondary }`
- `SubjectType { Theory, Practical, Activity }`
- `SectionShift { Morning, Afternoon, Evening }`

**Entities** (fields as spec §5.1)
- `AcademicSession`, `SchoolClass` (table `Classes`) and `Subject`: `AuditableEntity, ICampusOwnable`
- `ClassSubject`: PK (ClassId, SubjectId)
- `Section`: `AuditableEntity, ICampusScoped`

**Indexes**
- All filtered on `[IsDeleted] = 0`:
  - Roles `(CampusId, Name)` unique. This replaces the global `Name` unique index.
  - AcademicSessions `(CampusId, Name)` unique
  - AcademicSessions `(CampusId)` unique, additionally filtered on `[IsCurrent] = 1`
  - Classes `(CampusId, Code)` unique
  - Subjects `(CampusId, Code)` unique
  - Sections `(CampusId, AcademicSessionId, ClassId, Name)` unique
- All FKs use Restrict.

**DbContext**
- DbSets: `AcademicSessions`, `Classes`, `Subjects`, `ClassSubjects`, `Sections`.
- `ConfigureFilters<TEntity>` gains an ownable branch:
  ```
  EF.Property<int?>(e,"CampusId") == null
    || CampusFilterDisabled
    || CampusFilterIds.Contains(EF.Property<int?>(e,"CampusId")!.Value)
  ```
  It is combined with soft delete for auditable types.
- `GuardCampusScopedChanges` gains an ownable pass, for Added/Modified/Deleted rows when `UserId != null`:
  - Added with null CampusId by a non-HQ user → stamp `ActiveCampusId` (throw `CampusAccessException("Select a campus.")` if null)
  - Null CampusId and not HQ → `CampusAccessException("Only HQ can change head-office items.")`
  - Non-null and `!CanAccessCampus` → `CampusAccessException("You do not have access to this campus.")`

**Constants and DTOs**
- `AppModules`: `MyCampus = "my-campus"`, `Academics = "academics"`, `AcademicSessions = "academic-sessions"`, `Classes = "classes"`, `Subjects = "subjects"`, `Sections = "sections"`
- `SystemRoles.CampusAdmin = "CampusAdmin"`
- `AcademicDtos.cs`, where `OwnerFields` means `int? CampusId, string? CampusCode`:
  - `AcademicSessionDto(int Id, string Name, DateOnly StartDate, DateOnly EndDate, SessionStatus Status, bool IsCurrent, int? CampusId, string? CampusCode, int SectionCount)`
  - `AcademicSessionListDto(IReadOnlyList<AcademicSessionDto> Items, int? EffectiveCurrentId)`
  - `SaveAcademicSessionRequest(string Name, DateOnly StartDate, DateOnly EndDate, SessionStatus Status, int? CampusId = null)`
  - `SubjectDto(int Id, string Code, string Name, SubjectType Type, bool IsActive, int? CampusId, string? CampusCode, int ClassCount)`
  - `SaveSubjectRequest(string Code, string Name, SubjectType Type, bool IsActive = true, int? CampusId = null)`
  - `ClassDto(int Id, string Code, string Name, ClassStage Stage, int DisplayOrder, bool IsActive, int? CampusId, string? CampusCode, int SubjectCount, int SectionCount)`
  - `ClassSubjectDto(int SubjectId, string Code, string Name, SubjectType Type, bool IsOptional, int DisplayOrder)`
  - `ClassDetailDto(ClassDto Class, IReadOnlyList<ClassSubjectDto> Subjects)`
  - `SaveClassRequest(string Code, string Name, ClassStage Stage, int DisplayOrder, bool IsActive = true, int? CampusId = null)`
  - `SaveClassSubjectItem(int SubjectId, bool IsOptional)`
  - `SectionDto(int Id, int CampusId, string CampusCode, string CampusName, int AcademicSessionId, string SessionName, int ClassId, string ClassCode, string ClassName, int ClassOrder, string Name, SectionShift Shift, int? Capacity, string? Room, bool IsActive)`
  - `SaveSectionRequest(int ClassId, int? AcademicSessionId, string Name, SectionShift Shift, int? Capacity, string? Room, bool IsActive = true, int? CampusId = null)`
  - `CopySectionsRequest(int FromSessionId, int ToSessionId, bool DryRun)`
  - `CopySectionsResultDto(int Copied, int Skipped)`

**`tests/e2e/sms_client.py`**
- `call(method, path, body=None, token=None, campus=None) -> (status, json|str)`
- `login(email, password) -> dict` (the full auth result)
- `admin_token() -> str`
- `check(name, cond, detail="")`
- `summary() -> int`
- `temp_password() -> str`
- `make_user(token, *, campus_ids, role_names, hq=False, must_change=False, campus=None) -> (user_id, email, password)` (tracked for cleanup; `campus` is the X-Campus-Id the creator sends)
- `cleanup(token)`: deletes tracked users, roles, sessions, classes, subjects and sections in dependency order

- [ ] **Step 1: Create the branch and test harness.** `git checkout -b feature/campus-admin-academics`. Write the harness. Port the 23 campus-isolation checks from the previous feature into group `isolation` (same assertions, using `sms_client`). Group `schema` asserts:
  - HQ `GET /roles` → 200, and every item has a `campusId` key
  - `GET /academic-sessions` → 200 (endpoint exists in Task 6, so this FAILS for now; keep it)
- [ ] **Step 2: Run `python tests/e2e/academics_e2e.py schema`.** Expected: FAIL (no `campusId` on roles).
- [ ] **Step 3: Implement.** Add the entities, configurations, filter and guard, constants and DTOs. Add `int? CampusId, string? CampusCode, bool HqAssignOnly` to `RoleListItemDto` and project them in `RoleRepository.ListAsync`.
- [ ] **Step 4: Create the migration.** If needed, run `dotnet tool install --global dotnet-ef --version 10.0.12`. Then `dotnet ef migrations add CampusAdminAndAcademics --project SchoolManagementSystem-API/SchoolManagementSystem.Infrastructure --startup-project SchoolManagementSystem-API/SchoolManagementSystem.Infrastructure`. Expected: it adds Roles.CampusId and HqAssignOnly, Modules.CampusLevel and the five new tables, and swaps the Roles name index. Nothing is dropped.
- [ ] **Step 5: Build and restart, then run the test.** `dotnet build SchoolManagementSystem-API/SchoolManagementSystem-API.sln` → 0 errors. Restart the API (migrations run on startup). Run `schema`: the roles assertion PASSES. Run `python tests/e2e/academics_e2e.py isolation`: 23/23 PASS, so there are no regressions.
- [ ] **Step 6: Commit** `feat: campus ownership filter and academics schema`

### Task 2: Seed menus, CampusAdmin role, default permissions and academic data

**Files:** `DatabaseSeeder.cs`, `RoleDefaultsSeeder.cs`, `AcademicSeeder.cs`; test group `seed`.

**Interfaces:**
- `RoleDefaultsSeeder.SeedAsync(db, ct)`
- `AcademicSeeder.SeedAsync(db, ct)`

Both are called from `DatabaseSeeder.SeedAsync` after the campuses are seeded.

- [ ] **Step 1: Write group `seed`.** It asserts:
  - **Menu:** `/auth/me` (HQ, no campus) top-level keys in order are `dashboard, campuses, academics, settings`. `my-campus` is hidden on "All campuses". `academics` has children `academic-sessions, classes, subjects, sections`. With header campus A: `my-campus` is present at position 2.
  - **Roles:** `GET /roles` has `CampusAdmin` with `hqAssignOnly == true` and `campusId == null`. `GET /roles/{CampusAdmin}` has View/Add/Edit/Delete on `users`, `roles`, `sections`, `my-campus`, and View+Edit (no Add/Delete) on `campuses`. `Teacher` has View on `sections`.
  - **Sessions:** `/academic-sessions` items contain `2026-27` with `isCurrent`, `campusId == null` and `effectiveCurrentId` equal to its id. (This depends on Task 6, so it stays failing until then.)
  - **Seeding twice:** restarting the API leaves the role count unchanged.
- [ ] **Step 2: Run it.** Expected: FAIL.
- [ ] **Step 3: Implement the seeders.**
  - **Modules:** upsert `my-campus` ("My campus", `/admin/my-campus`, `school`, order 2, CampusLevel) and the `academics` group (`graduation-cap`, order 4) with its children: Sessions `calendar-range` 1, Classes `layers` 2, Subjects `book-open` 3, Sections `layout-grid` 4, all CampusLevel. Reorder Campuses 3 and Settings 5. Set CampusLevel on `dashboard`, `users` and `roles`.
  - **CampusAdmin role:** `IsSystem`, `HqAssignOnly`. Set `HqAssignOnly` on SuperAdmin too.
  - **`RoleDefaultsSeeder`:**
    - Grants per spec §4.2, only to roles that currently have zero permissions.
    - CampusAdmin additionally gets, on every run, full rights on every `CampusLevel` module it lacks, so future modules are granted automatically.
  - **`AcademicSeeder`:** per spec §10, each part only if its table is empty.
  - **Menu hiding:** `MenuBuilder.Build` gains the parameters `bool isHq, int? activeCampusId`. It drops `my-campus` when `isHq && activeCampusId == null`, and drops `campuses` when `!isHq`. `AuthService.BuildCurrentUserAsync` passes them.
- [ ] **Step 4: Restart the API twice and run `seed`.** Expected: menu and role assertions PASS. The session assertion is still pending Task 6.
- [ ] **Step 5: Commit** `feat: seed campus admin role, default permissions and academic data`

### Task 3: Change password

**Files:** `IAuthService.cs`, `AuthService.cs`, `AuthController.cs`, `AuthDtos.cs`; test group `password`.

**Interfaces:**
- `ChangePasswordRequest(string CurrentPassword, string NewPassword)`
- `IAuthService.ChangePasswordAsync(int userId, ChangePasswordRequest, ct) -> Result`
- `POST /api/auth/change-password` (authorized, no module permission)

- [ ] **Step 1: Write the group `password` tests.**
  - `change_password_clears_flag`:
    1. HQ creates a user with password P1 via `make_user(..., must_change=True)`, which sends `mustChangePassword: true` (added in Step 3).
    2. Log in with P1. Expect `user.mustChangePassword == True`.
    3. Change P1 → P2: 200. `/auth/me` returns `mustChangePassword == False`. Logging in with P1 gives 401, logging in with P2 gives 200.
  - `wrong_current_or_weak_refused`: wrong current password → 400; a new password of 7 characters → 400; new password equal to current → 400.
- [ ] **Step 2: Run the tests.** Expected: FAIL (404).
- [ ] **Step 3: Implement.**
  - Add `bool MustChangePassword = false` as the last parameter of `SaveUserRequest`. On create, set the flag to `request.MustChangePassword || password blank`.
  - `ChangePasswordAsync` checks, in order: verify the current password → 400 "Current password is incorrect."; length ≥ 8; differs from the current one. Then it hashes the new password, clears the flag, saves, and revokes all of the user's refresh tokens via `TokenService.RevokeAllAsync`. Other devices are signed out at their next refresh; the current tab keeps its access token.
- [ ] **Step 4: Run `password`.** Expected: PASS.
- [ ] **Step 5: Commit** `feat: change password endpoint and first sign-in flag`

### Task 4: Role ownership and safe role assignment

**Files:** `RoleService.cs`, `RoleRepository.cs`, `UserService.cs`, `RolePermissionDtos.cs`; test group `roles-ownership`.

**Interfaces:**
- `CreateRoleRequest(string Name, string? Description, int? CampusId = null)`
- `RoleDetailDto` gains `int? CampusId, string? CampusCode, bool HqAssignOnly`
- `RoleRepository`:
  - `NameTakenAsync(string name, int? campusId, int? excludeId, ct)`: true if HQ or the same campus already has the name
  - `GetByIdsWithOwnerAsync(ids, ct)`
- `GET /roles` is now filtered by ownership through the EF filter. HQ on "All campuses" sees all roles.

- [ ] **Step 1: Write the group `roles-ownership` tests.** Setup: HQ creates campus-A user `ca` with role `CampusAdmin` and campus [A].
  - `campus_admin_creates_campus_role`: `ca` (header A) creates role `QA Coach` → 200 with `campusCode == A`. HQ with header B: `GET /roles` does not include it.
  - `cannot_edit_hq_role`: `ca` PUTs the `Teacher` role → 403.
  - `cannot_grant_unheld_permission`: `ca` PUTs `QA Coach` with `campuses.delete = true` → 403 "You can only grant permissions you have.". With only `sections` View/Add → 200.
  - `campus_role_only_for_its_campus_users`: `ca` creates user U on [A] with `QA Coach` → 200. HQ creates user V on [A, B] and assigns `QA Coach` → 403.
  - `hq_assign_only_roles`: `ca` creates a user with `CampusAdmin` → 403 "Only HQ can assign this role.". `ca` cannot see the `SuperAdmin` role in assignable roles (`GET /roles?assignable=true`).
  - `campus_role_name_cannot_shadow_hq`: `ca` creates role `Teacher` → 409.
- [ ] **Step 2: Run the tests.** Expected: FAIL.
- [ ] **Step 3: Implement.**
  - **`RoleService.CreateAsync`:**
    - The owner is `request.CampusId` (HQ only), or `null` for HQ on "All campuses", or `ActiveCampusId` for a campus user. Campus users need `roles` Add.
    - Remove the blanket HQ-only rule and replace it with the ownership rules: HQ rows HQ-only, campus rows need `CanAccessCampus`.
  - **`UpdateAsync`:**
    - Same ownership check, plus the existing "role you hold" and SuperAdmin rules.
    - For non-HQ actors, every granted cell must be within the actor's own `UserAccessDto` (403 otherwise). `HqAssignOnly` is never settable through the API.
  - **`DeleteAsync`:** the same ownership check.
  - **`GET /roles?assignable=true`:** hides `HqAssignOnly` roles for non-HQ users.
  - **`UserService.ValidateAsync`:**
    - `HqAssignOnly` roles need HQ.
    - A campus role (`CampusId != null`) needs `request.CampusIds` to be exactly that one campus, and `IsHq` false.
- [ ] **Step 4: Run `roles-ownership`, then the `isolation` group.** Expected: all PASS.
- [ ] **Step 5: Commit** `feat: campus-owned roles with escalation and assignment guards`

### Task 5: Campus admins on campuses

**Files:** `CampusDtos.cs`, `CampusRepository.cs`, `CampusService.cs`; test group `campus-admin`.

**Interfaces:**
- `NewCampusAdminRequest(string FirstName, string LastName, string Email, string Password)`
- `SaveCampusRequest` gains `NewCampusAdminRequest? Admin = null` as its last parameter
- `CampusAdminRefDto(int Id, string FullName, string Email)`
- `CampusDto` gains `IReadOnlyList<CampusAdminRefDto> Admins` as its last parameter. These are active users holding `CampusAdmin` with that campus.

- [ ] **Step 1: Write the group `campus-admin` tests.**
  - `create_campus_with_admin`: HQ POSTs campus `QAC` with an admin → 200, and `admins` has 1 entry with that email.
  - `admin_first_login_and_scope`:
    - The admin logs in → `mustChangePassword == True`, campuses == [QAC], menu has `my-campus`, no `campuses` menu.
    - After a password change, `GET /campuses/{QAC}` → 200. PUT with a changed name → 200. PUT with a changed code → 403.
    - `POST /users` with the Teacher role on [QAC] → 200. With the CampusAdmin role → 403. With `isHq: true` → 403.
  - `admin_email_conflict`: POST another campus whose admin email already exists → 409, and no campus is created (the transaction rolls back).
  - Cleanup deletes the users and then the campus.
- [ ] **Step 2: Run the tests.** Expected: FAIL.
- [ ] **Step 3: Implement.**
  - **`CreateAsync` with `Admin`:**
    - Validate first: names and email required, password ≥ 8, email not taken.
    - Create the campus and the admin user (`CampusAdmin` role, `MustChangePassword = true`, `UserCampuses = [new campus, primary]`) in one `SaveChangesAsync`.
    - Hash with the injected `IPasswordHasher<User>`, then call `permissionChecker.Invalidate()`.
  - **Admins projection:** add it to `CampusRepository.ListAsync`.
- [ ] **Step 4: Run `campus-admin`.** Expected: PASS.
- [ ] **Step 5: Commit** `feat: appoint campus admin when creating a campus`

### Task 6: Academic sessions API

**Files:** `SessionRepository.cs`, `IAcademicSessionService.cs`, `AcademicSessionService.cs`, `AcademicSessionsController.cs`, repositories aggregate, `Startup.cs`; test group `sessions`.

**Interfaces:**
- **`SessionRepository`:**
  - `ListAsync(ct) -> List<AcademicSessionDto>` (filtered; newest first; SectionCount counts sections visible to the request)
  - `GetTrackedAsync(id, ct)`
  - `GetEffectiveCurrentAsync(int? campusId, ct) -> AcademicSession?` (the campus's own current session, else the HQ one)
  - `NameTakenAsync(name, int? campusId, int? excludeId, ct)`
  - `OverlapsAsync(start, end, int? campusId, int? excludeId, ct)` (same owner only)
  - `HasSectionsAsync(id, ct)`
- **`IAcademicSessionService`:** `ListAsync() -> Result<AcademicSessionListDto>`, `CreateAsync`, `UpdateAsync`, `DeleteAsync`, `SetCurrentAsync(int id)`
- **Controller:** `api/academic-sessions`, permission `AppModules.AcademicSessions`. `set-current` uses Edit.

- [ ] **Step 1: Write the group `sessions` tests.**
  - `hq_crud`:
    - Create `QA-2099` (2099-04-01..2100-03-31, Planned) → 200 with `campusId == null`.
    - Duplicate → 409. Overlapping HQ session → 400. End before start → 400.
  - `edit_session_keeps_own_dates`: PUT with the same dates and a new status → 200.
  - `campus_session_same_dates_allowed`: campus admin of A creates `QA-A` with the same dates → 200 with `campusCode == A`, because overlap only applies within the same owner.
  - `campus_current_overrides_only_that_campus`: `set-current` on `QA-A` (as campus A) → 200. Then:
    - With header A: `effectiveCurrentId == QA-A`.
    - With header B: `effectiveCurrentId` == the HQ `2026-27` id.
    - HQ list: `2026-27` still `isCurrent`.
  - `set_current_rejects_closed`: close `QA-2099`, then `set-current` → 400 "Reopen the session first.".
  - `cannot_change_hq_session`: campus admin PUTs `2026-27` → 403.
  - `delete_current_or_used_refused`: deleting `2026-27` → 400.
- [ ] **Step 2: Run the tests.** Expected: FAIL (404).
- [ ] **Step 3: Implement** following the ownership rules (Global Constraints) and spec §5.3.
  - The owner on create follows the same rule as roles (Task 4).
  - `SetCurrentAsync` clears `IsCurrent` only on sessions of the **same owner**, then sets the target to `IsCurrent` and `Active`.
  - `effectiveCurrentId` uses `currentUser.ActiveCampusId`.
- [ ] **Step 4: Run `sessions` and `seed`.** Expected: PASS.
- [ ] **Step 5: Commit** `feat: academic sessions API with campus override`

### Task 7: Subjects API

**Files:** `SubjectRepository.cs`, `ISubjectService.cs`, `SubjectService.cs`, `SubjectsController.cs`, aggregate, `Startup.cs`; test group `subjects`.

**Interfaces:**
- `SubjectRepository`:
  - `ListAsync(ct)` (filtered, by Name)
  - `GetTrackedAsync`
  - `CodeTakenAsync(code, int? campusId, int? excludeId, ct)`
  - `IsUsedAsync(id, ct)`
  - `GetByIdsAsync(ids, ct)` (filtered)
- `ISubjectService`: `ListAsync`, `CreateAsync`, `UpdateAsync`, `DeleteAsync`
- Controller: `api/subjects`

- [ ] **Step 1: Write the group `subjects` tests.**
  - `hq_crud`: create `qa1` → `code == "QA1"`; duplicate → 409; delete → 200.
  - `campus_subject_scoped`: campus A admin creates `QAA` → 200. With header B it is not listed. HQ "All campuses" lists it with `campusCode == A`.
  - `same_code_two_campuses_ok`: campus B admin creates `QAA` → 200.
  - `campus_code_cannot_shadow_hq`: campus A admin creates `ENG` → 409.
  - `used_subject_not_deletable`: HQ deletes `ENG` → 400.
  - `cannot_change_hq_subject`: campus admin PUTs `ENG` → 403.
- [ ] **Step 2: Run the tests.** Expected: FAIL.
- [ ] **Step 3: Implement** as in Task 6. Deleting a used subject → 400 "Subject is used by {n} class(es). Deactivate it instead."
- [ ] **Step 4: Run `subjects`.** Expected: PASS.
- [ ] **Step 5: Commit** `feat: subjects API with campus ownership`

### Task 8: Classes and class subjects API

**Files:** `ClassRepository.cs`, `IClassService.cs`, `ClassService.cs`, `ClassesController.cs`, aggregate, `Startup.cs`; test group `classes`.

**Interfaces:**
- Consumes `SubjectRepository.GetByIdsAsync`.
- **`ClassRepository`:**
  - `ListAsync(ct)` (filtered, by DisplayOrder)
  - `GetDetailAsync(id, ct)`
  - `GetTrackedWithSubjectsAsync(id, ct)`
  - `CodeTakenAsync(code, int? campusId, int? excludeId, ct)`
  - `IsUsedAsync(id, ct)`
  - `GetForSectionAsync(id, ct) -> SchoolClass?` (filtered; used by Task 9)
- **`IClassService`:** `ListAsync`, `GetAsync`, `CreateAsync`, `UpdateAsync`, `DeleteAsync`, `SetSubjectsAsync(int id, IReadOnlyList<SaveClassSubjectItem>) -> Result<ClassDetailDto>`
- **Controller:** `api/classes`. `PUT {id}/subjects` uses Edit.

- [ ] **Step 1: Write the group `classes` tests.**
  - `hq_crud`: create `qa9` → 200; duplicate → 409; delete → 200.
  - `set_subjects_replaces_list`: PUT `[ENG(false), ART(true)]` → that order, with optional flags. PUT `[MATH(false)]` → only MATH. An unknown id → 400. A duplicate id → 400.
  - `campus_class_with_campus_subject`: campus A admin creates class `QAK` and maps HQ `ENG` plus its own `QAS` → 200.
  - `campus_class_rejects_other_campus_subject`: campus A maps campus B's subject id → 400.
  - `cannot_change_hq_class_subjects`: campus admin PUTs subjects of `G1` → 403.
  - `used_class_not_deletable`: HQ deletes `G1` → 400.
- [ ] **Step 2: Run the tests.** Expected: FAIL.
- [ ] **Step 3: Implement** with the ownership rules.
  - A mapped subject must be HQ-owned or owned by the class's campus, otherwise 400 "One or more subjects are invalid.".
  - The replace-list approach is the same as `RoleService.UpdateAsync`.
- [ ] **Step 4: Run `classes`.** Expected: PASS.
- [ ] **Step 5: Commit** `feat: classes and class subjects API with campus ownership`

### Task 9: Sections API with copy and closed lock

**Files:** `SectionRepository.cs`, `ISectionService.cs`, `SectionService.cs`, `SectionsController.cs`, aggregate, `Startup.cs`; test group `sections`.

**Interfaces:**
- Consumes `SessionRepository.GetTrackedAsync/GetEffectiveCurrentAsync` and `ClassRepository.GetForSectionAsync`.
- **`SectionRepository`:**
  - `ListAsync(int sessionId, int? classId, ct)` (filtered; order by campus name, class order, name)
  - `GetTrackedAsync`
  - `ExistsAsync(campusId, sessionId, classId, name, int? excludeId, ct)`
  - `ListForCopyAsync(campusId, sessionId, ct)`
  - `CountAsync(int sessionId, ct)` (used by Task 10)
- **`ISectionService`:** `ListAsync(int? sessionId, int? classId)`, `CreateAsync`, `UpdateAsync`, `DeleteAsync`, `CopyAsync`
- **Controller:** `api/sections`. `POST copy` uses Add.

- [ ] **Step 1: Write the group `sections` tests.** The campus user is campus A's admin.
  - `create_and_list`: POST `{classId: G6, name: "QA", shift: "Afternoon", capacity: 30}` → 200 with `campusId == A` and `sessionName == "2026-27"`. GET returns only A rows. A duplicate → 409.
  - `campus_user_cannot_touch_other_campus_section`: PUT and DELETE a campus B section id → 404, and it is unchanged.
  - `campus_user_cannot_create_on_other_campus`: `campusId: B` → 403.
  - `hq_all_campuses_needs_campus`: HQ with no header and no `campusId` → 400 "Select a campus.".
  - `foreign_class_or_session_refused`: on campus A, a class owned by campus B → 400. A session owned by campus B → 400.
  - `inactive_class_refused` → 400.
  - `update_into_duplicate_is_409`.
  - `copy_dry_run_then_copy`: from the current session to a temp HQ Planned session. The dry run's `copied == n`. The real run gives `copied == n`. A second run gives `copied 0, skipped n`.
  - `copy_rejects_closed_or_same_session`: from == to → 400. Copying into a Closed session → 400 "This session is closed.".
  - `closed_session_locks_sections`: PUT and DELETE in a closed session → 400.
- [ ] **Step 2: Run the tests.** Expected: FAIL.
- [ ] **Step 3: Implement `SectionService`.**
  - **Campus:** `request.CampusId ?? ActiveCampusId`; null → 400 "Select a campus."; no access → 403.
  - **Session:** `request.AcademicSessionId ?? effective current(campus)`. It must be HQ-owned or owned by that campus (400 otherwise). Closed → 400.
  - **Class:** via `GetForSectionAsync`. It must be active, and HQ-owned or owned by the same campus (400 otherwise).
  - **Name:** a duplicate → 409.
  - **Update and Delete:** use the filtered `GetTrackedAsync` (null → 404) and apply the closed lock.
  - **Copy:**
    - Needs `ActiveCampusId` (400 "Select a campus." otherwise), `from != to`, and a target that is not Closed and is HQ-owned or owned by this campus.
    - Copies active sections whose class is active, skipping existing names.
    - With `DryRun` it returns counts without saving.
- [ ] **Step 4: Run `sections`, then every group in order.** `for g in isolation schema seed password roles-ownership campus-admin sessions subjects classes sections; do python tests/e2e/academics_e2e.py $g || break; done`. Expected: all PASS.
- [ ] **Step 5: Commit** `feat: sections API with session copy and closed-session lock`

### Task 10: Dashboard KPI

**Files:** `DashboardService.cs`; test group `dashboard`.

- [ ] **Step 1: Write the test.** KPI id `academics`:
  - HQ on "All campuses": value `"2026-27"`, hint `^\d+ sections$`, href `/admin/academics/sections`.
  - With header A: the count equals the number of `GET /sections` rows for A.
- [ ] **Step 2: Run the test.** Expected: FAIL.
- [ ] **Step 3: Implement.** Insert the KPI as the second card: `new("academics", "Current session", current?.Name ?? "Not set", $"{count} sections", "primary", "/admin/academics/sections")`, using `GetEffectiveCurrentAsync(ActiveCampusId)` and `SectionRepository.CountAsync`.
- [ ] **Step 4: Run `dashboard`.** Expected: PASS.
- [ ] **Step 5: Commit** `feat: current session dashboard KPI`

### Task 11: UI foundation (types, routes, icons, owner pill, password screen)

**Files:** `src/lib/types.ts`, `src/lib/auth.tsx`, `src/App.tsx`, `src/components/app-shell.tsx`, `src/components/owner-pill.tsx`, `src/pages/change-password.tsx`, `src/pages/dashboard.tsx`.

**Interfaces (produces):**
- **TS types** mirroring the DTOs from Tasks 1, 3, 4 and 5, with the same camelCase names:
  - `AcademicSession`, `AcademicSessionList`, `Subject`, `SchoolClass`, `ClassDetail`, `ClassSubject`, `Section`, `CopySectionsResult`
  - `CampusAdminRef`, and `RoleListItem` with `campusId`, `campusCode` and `hqAssignOnly`
  - The four enum string unions, plus `stageLabel` and `shiftLabel` maps
- **`OwnerPill({ campusCode }: { campusCode?: string | null })`:** renders `HQ` with `tone="info"` and a `Lock` icon when the code is null, or the code with `tone="primary"` otherwise.
- **`useAuth().changePassword(current: string, next: string): Promise<void>`:** posts to the endpoint, then calls `refreshMe`.
- **Routes:** `my-campus`, `academics/sessions`, `academics/classes`, `academics/subjects`, `academics/sections`. `Protected` renders `<ChangePasswordPage forced />` instead of the shell when `user.mustChangePassword`.
- **`iconMap`** adds `school`, `graduation-cap`, `calendar-range`, `layers`, `book-open` and `layout-grid`.
- **`UserMenu`** gets a "Change password" row that opens `ChangePasswordPage` in a `Modal`.
- **Dashboard `kpiStyle`** adds `academics` (GraduationCap, primary).

- [ ] **Step 1: Implement.**
  - Use placeholder `PageHeader` components for pages built in later tasks.
  - `ChangePasswordPage` has the same centred layout and width as the login form. Its fields are Current, New and Confirm, with a show/hide toggle. Validation: ≥ 8 characters, and confirm must match, shown inline before submitting. On success it shows a toast, "Password updated".
- [ ] **Step 2: Verify.**
  - `npm --prefix SchoolManagementSystem-UI run build` exits with 0.
  - In the browser, as HQ: the sidebar shows Dashboard, Campuses, Academics (4 items) and Settings. Selecting a campus adds My campus.
  - A user created with must-change sees only the password screen. After changing the password, the app loads.
  - No console errors.
- [ ] **Step 3: Commit** `feat(ui): academics routes, owner pill and password change`

### Task 12: My campus, campus admins, users and roles UI

**Files:** `src/pages/my-campus.tsx`, `src/pages/campuses.tsx`, `src/pages/users.tsx`, `src/pages/roles.tsx`.

- [ ] **Step 1: My campus page.**
  - **Header:** the campus name, and the code as a mono chip.
  - **Three panels:**
    - **Profile:** the campus form fields from `campuses.tsx`. Code and Active are shown read-only. Saves through `PUT /campuses/{activeCampusId}`.
    - **Team:** counts per role from `GET /users`, plus an "Open users" link.
    - **Academics:** the effective current session from `GET /academic-sessions`, and class and section counts. Links to Sections.
  - On "All campuses": an `EmptyState` saying "Pick a campus in the top bar".
- [ ] **Step 2: Campuses page.**
  - Each card footer shows the admin: `Avatar` plus name, or "No admin yet" in muted text.
  - The add dialog gets a `FormSection` titled "Campus admin (optional)", with a `Switch` "Create the campus admin now" that reveals First name, Last name, Email and Temporary password. A hint reads "They will be asked to change it on first sign-in."
- [ ] **Step 3: Users page.**
  - Load roles from `/roles?assignable=true`.
  - Role cards show `OwnerPill`, and an "HQ only" `StatusPill` for `hqAssignOnly`.
  - When any campus role is checked, campus choices other than that role's campus are disabled, with the description "Campus role · {code} only".
- [ ] **Step 4: Roles page.**
  - Add an Owner column with `OwnerPill`.
  - The Add button shows for campus users too. On HQ "All campuses", the add dialog has an Owner `Select` (HQ or a campus).
  - **Matrix:** for non-HQ users, cells for permissions the user lacks are disabled, with the title "You don't have this permission". This uses `useAuth().can`.
  - HQ roles are read-only for campus users, with a lock banner.
- [ ] **Step 5: Verify.**
  - `npm --prefix SchoolManagementSystem-UI run build` exits with 0.
  - Browser walk-through:
    1. As HQ, create campus `QAU` with an admin.
    2. Sign in as that admin. You get the password screen. Change the password. My campus shows QAU, and the Campuses menu is hidden.
    3. Create role `QA Coach` with sections View. The `campuses` Delete cell is disabled.
    4. Create a Teacher user.
    5. Sign out. As HQ, delete the QAU users and the campus.
  - No console errors.
- [ ] **Step 6: Commit** `feat(ui): my campus page, campus admins and campus-owned roles`

### Task 13: Sessions and Subjects pages

**Files:** `src/pages/academics/sessions.tsx`, `src/pages/academics/subjects.tsx`.

- [ ] **Step 1: Sessions page.**
  - The table copies the structure of `roles.tsx`. Columns: Session (name, plus "Current" `StatusPill primary` when `id == effectiveCurrentId`, plus "HQ current" muted text on the HQ current row when the campus overrides it), Owner (`OwnerPill`), Dates (`1 Apr 2026 – 31 Mar 2027`), Status (Planned neutral, Active success, Closed warning), Sections, and Actions.
  - **Actions:** Edit, "Set as current" (hidden when already current for that owner, or Closed), and Delete via `ConfirmDialog`. Rows the user cannot write show a lock and no actions.
  - **Dialog:** Name, Start, End and Status. On HQ "All campuses" there is also an Owner select.
- [ ] **Step 2: Subjects page.**
  - Columns: Subject (mono code chip and name), Owner, Type (Theory info, Practical warning, Activity success), "Used in N classes", Status, and Actions.
  - Search and a status filter, as on the Users page.
  - **Dialog:** Code, Name, Type, and an Active switch, plus an Owner select on HQ "All campuses".
- [ ] **Step 3: Verify.**
  - The build exits with 0.
  - In the browser, as HQ: `2026-27` shows as Current with an HQ pill, and there are 12 subjects.
  - As a campus admin: add a campus subject and it shows the campus pill. HQ subjects show a lock.
  - Clean up what you created. No console errors.
- [ ] **Step 4: Commit** `feat(ui): sessions and subjects pages`

### Task 14: Classes page

**Files:** `src/pages/academics/classes.tsx`.

- [ ] **Step 1: Build the page.**
  - **Table** (by DisplayOrder): #, Class (mono code and name), Owner, Stage, Subjects, Sections, Status, Actions.
  - **Row click** opens a wide dialog with two parts:
    - "Details" `FormSection`: Code, Name, Stage, Display order, Active.
    - "Subjects" `FormSection`: a `ChoiceCard` per active subject the class may use (HQ subjects, plus the class campus's own subjects). The card description is code · type, the badge is `OwnerPill`, and each checked card has an "Optional" `Switch`.
  - **Save:** PUT the class, then PUT the subjects, in list order.
  - **Read-only** (HQ class for a campus user): everything is disabled, with a lock banner reading "Head-office class. Create your own class to change its subjects."
- [ ] **Step 2: Verify.**
  - The build exits with 0.
  - In the browser: G5 shows 9 subjects with Art optional. As HQ, untick PE and save; reopening shows PE unticked. Restore it.
  - As a campus admin, G5 is read-only.
  - No console errors.
- [ ] **Step 3: Commit** `feat(ui): classes page with subject mapping`

### Task 15: Sections page and final verification

**Files:** `src/pages/academics/sections.tsx`.

- [ ] **Step 1: Build the page.**
  - **Top bar:**
    - Session `Select`. It defaults to `effectiveCurrentId` and labels the current one "· Current".
    - Class filter.
    - "Copy from last session" (outline) and "Add section" (primary). Both are hidden when the session is Closed or the user lacks `sections` Add.
  - **Closed session:** a banner with a `Lock` icon: "This session is closed. Sections are read-only."
  - **Body:** one `panel` card per class, in class order. Each card header shows the name, stage pill, count and an "+ Add" `IconButton`. Sections appear as chips (name semibold; shift · capacity · room as muted `text-xs`). On "All campuses" each chip also shows a mono campus-code tag. Clicking a chip opens edit.
  - **No sections at all:** `EmptyState` with the `LayoutGrid` icon and the copy action.
  - **Dialog:** Class (classes usable by the campus, active only), Name, Shift, Capacity, Room, Active. On "All campuses" there is a required Campus `Select`. Existing sections get a Delete button via `ConfirmDialog`.
  - **Copy flow:**
    1. Pick a source session (default: the newest one before the target).
    2. Dry run, then show a `Modal` reading "{copied} sections will be copied, {skipped} already exist".
    3. Confirm, then toast and reload.
    4. On "All campuses" the button is disabled, with the title "Pick a campus first".
- [ ] **Step 2: Verify.**
  - The build exits with 0.
  - **Browser:**
    - As HQ on "All campuses", G1–G5 chips have MAIN and EAST tags.
    - Switched to Main Campus, only MAIN chips appear. Adding Grade 3 C shows a chip; delete it.
    - Copying into a temp Planned session shows the dry-run count. Cancel, and delete the temp session.
    - The dashboard KPI links to Sections.
    - Dark mode looks right on all new pages.
    - No console errors.
- [ ] **Step 3: Run the full end-to-end suite** (the Task 9 loop plus `dashboard`). Expected: every group PASSES.
- [ ] **Step 4: Commit** `feat(ui): campus sections page with session copy`
