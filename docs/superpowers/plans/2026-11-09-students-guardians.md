# Students and Guardians (Part A) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Campus staff can admit students in one step, link school-wide guardians (with siblings), keep a photo and documents, change section, mark a student as left or rejoined, and search students, all isolated per campus.

**Architecture:**
- **Data:**
  - `Student`, `Enrollment` and `StudentDocument` are `ICampusScoped`, so the existing DbContext filter and save guard isolate campuses.
  - `Guardian` is school-wide. Services expose it only through students the caller can see, plus a minimal exact-match lookup.
  - Admission runs in one transaction. Its admission number comes from a `CampusSequences` counter that is incremented atomically.
- **Files** live on local disk behind an `IFileStorage` abstraction and are only streamed after permission and campus checks.
- **UI:** new pages built only from `sms-ui.tsx` and `modal.tsx`.

**Tech Stack:** .NET 10, ASP.NET Core, EF Core 10 (SQL Server), React 19, TypeScript, Tailwind v4, lucide-react, and Python 3 end-to-end scripts (stdlib only).

**Spec:** `docs/superpowers/specs/2026-11-09-students-guardians-design.md` (Part A; Part B is out of scope here)

## Global Constraints

- **Git:** work directly on `main`. **No new branch, no commits** (product owner's standing instruction). Leave all changes uncommitted.
- **Migration:** one migration for this plan, `StudentsAndGuardians`. If a later task changes the schema, regenerate it; do not add a second migration.
- **Enums** serialize as strings (already configured).
- **Admission number** format is `{CampusCode}-{yyyy}-{0001}`: the year of `AdmissionDate` and a 4-digit zero-padded counter. The sequence key is `ADM-{yyyy}`.
- **File limits:**
  - photo: `.jpg/.jpeg/.png/.webp`, at most 2 MB (2,097,152 bytes)
  - document: the same types plus `.pdf`, at most 10 MB (10,485,760 bytes)
  - the type is checked by extension **and** magic bytes
- **Storage:** root comes from config `Storage:UploadsPath`, default `App_Data/uploads` under the content root. Path is `{root}/{campusId}/{studentId}/{guid}{ext}`. Nothing is served statically.
- **Phone and CNIC** are compared digits-only, both when matching and when storing CNIC.
- **Error copy (exact):**
  - "Rejoin the student first."
  - "A guardian with this CNIC exists — link them instead."
  - "Pick a section."
  - "A student must keep at least one guardian."
  - "Choose another primary guardian first."
- **End-to-end tests:**
  - New file `tests/e2e/students_e2e.py`, run as `python tests/e2e/students_e2e.py <group>`.
  - It reuses `sms_client` and imports `campus_admin`, `create_section`, `demo_campuses`, `class_id` and `hq_current_id` from `academics_e2e`.
  - Every group cleans up after itself.
- **Regression:** after each API task, the existing suite (`python tests/e2e/academics_e2e.py isolation seed sections review-fixes password-gate`) must still pass.
- **UI:** reuse the kit and CSS variables only. No new primitives except where a task names one.

## Review Focus

1. **Adding a second primary guardian.** A user adds a guardian with `isPrimary: true` to a student who already has one. They expect the old primary to be demoted automatically, not an error. Test: Task 3 `new_primary_demotes_old`.
2. **Rejoining after the old section is gone.** A student left, then their section was deactivated, or the effective session changed. Rejoin without `sectionId` must return 400 "Pick a section." and must not re-enroll silently into a dead section. Test: Task 4 `rejoin_needs_section_when_old_unusable`.
3. **Searching a phone typed differently.** Searching `03001234567` must find a guardian saved as `0300-1234567`, and the other way round. Test: Task 2 `search_phone_ignores_formatting`.
4. **A typed admission number colliding with the counter.** If staff type `MAIN-2026-0007` by hand, the generator must skip 0007 later rather than fail with a 409. Test: Task 2 `generator_skips_typed_numbers`.
5. **Files of a deleted student.** After a student is soft-deleted, downloading their photo or documents must return 404. Test: Task 5 `deleted_student_files_404`.

---

## File Map

**API**
- **Domain**
  - Create: `Entities/Students/{StudentEnums,Student,Guardian,StudentGuardian,Enrollment,EnrollmentHistory,StudentDocument,CampusSequence}.cs`, `DTOs/Students/StudentDtos.cs`, `Abstractions/IFileStorage.cs`
  - Modify: `Constants/AppModules.cs`
- **Infrastructure**
  - Create: `Configurations/Students/*Configuration.cs`, migration `StudentsAndGuardians`
  - Modify: DbContext (DbSets), `Seeding/DatabaseSeeder.cs` (menus), `Seeding/RoleDefaultsSeeder.cs` (new-module grants)
- **Application**
  - Create:
    - `Repository/{Student,Guardian,Enrollment,Sequence}Repository.cs`
    - `Interfaces/{IStudentService,IGuardianService,IStudentFileService}.cs`
    - `Services/Students/{StudentService,GuardianService,StudentFileService,PhoneNormalizer,FileSignature}.cs`
  - Modify: `Repository/SchoolManagementSystemRepositories.cs`, `Services/Dashboard/DashboardService.cs`
- **Api**
  - Create: `Controllers/{Students,Guardians}Controller.cs`, `Infrastructure/LocalFileStorage.cs`
  - Modify: `Startup.cs`, `appsettings.json` (`Storage:UploadsPath`)

**Tests**
- Create: `tests/e2e/students_e2e.py`
- Modify: `tests/e2e/sms_client.py` (multipart `upload` and binary `download` helpers)

**UI**
- Create:
  - `src/pages/students/{list,admission,profile}.tsx`
  - `src/pages/guardians/{list,detail}.tsx`
  - `src/components/student-photo.tsx`
- Modify: `src/lib/types.ts`, `src/lib/api.ts` (blob download helper), `src/App.tsx`, `src/components/app-shell.tsx`, `src/pages/dashboard.tsx`

---

### Task 1: Schema, migration, menu and seed

**Files:** Domain entities, enums, DTOs and constants; Infrastructure configurations, DbSets, seeders and migration; `tests/e2e/students_e2e.py` (group `schema`).

**Interfaces (produces):**

**Enums**
- `Gender { Male, Female, Other }`
- `StudentStatus { Active, Left }`
- `GuardianRelation { Father, Mother, Guardian, Other }`
- `EnrollmentStatus { Active, Left }`
- `EnrollmentAction { Admitted, SectionChanged, Left, Rejoined }`
- `DocumentType { BForm, BirthCertificate, PreviousResult, LeavingCertificate, Other }`

**Entities** (columns, lengths and uniqueness exactly as spec §3)
- `Student : AuditableEntity, ICampusScoped`: navigations `Campus`, `Guardians` (`ICollection<StudentGuardian>`), `Enrollments`, `Documents`. A computed `FullName` property is not mapped.
- `Guardian : AuditableEntity`: navigation `Students` (`ICollection<StudentGuardian>`).
- `StudentGuardian`: composite PK (StudentId, GuardianId).
- `Enrollment : AuditableEntity, ICampusScoped`: navigations `Student`, `AcademicSession`, `Section`.
- `EnrollmentHistory : Entity`: `int? ByUserId`, `DateTime OccurredOn`.
- `StudentDocument : AuditableEntity, ICampusScoped`. Its CampusId is copied from the student, so the campus filter covers documents too.
- `CampusSequence`: PK (CampusId, Key), with `int NextValue`.

**Indexes and constraints**
- Unique, filtered on `[IsDeleted] = 0`:
  - Students (CampusId, AdmissionNo)
  - Students BFormNo, additionally filtered on `[BFormNo] IS NOT NULL`
  - Guardians Cnic, additionally filtered on `[Cnic] IS NOT NULL`
  - Enrollments (StudentId, AcademicSessionId)
  - Enrollments (SectionId, RollNo), additionally filtered on `[Status] = 'Active'`
- All FKs use Restrict, except StudentGuardian → Student, which cascades.

**DbSets:** `Students`, `Guardians`, `StudentGuardians`, `Enrollments`, `EnrollmentHistory`, `StudentDocuments`, `CampusSequences`.

**`AppModules`**
- `StudentsMenu = "students-menu"` (the group; it needs its own key because the page key is `students`)
- `Students = "students"`
- `Guardians = "guardians"`

**DTOs in `StudentDtos.cs`** (every later task uses these names)
- `PagedResult<T>(IReadOnlyList<T> Items, int Total)`
- `StudentListItemDto(int Id, string AdmissionNo, string FullName, Gender Gender, bool HasPhoto, string? ClassName, string? SectionName, int? RollNo, string? GuardianName, string? GuardianPhone, StudentStatus Status, string CampusCode)`
- `StudentDetailDto(int Id, int CampusId, string CampusCode, string AdmissionNo, string FirstName, string LastName, string FullName, Gender Gender, DateOnly DateOfBirth, string? BFormNo, string? BloodGroup, string? Religion, string? Nationality, string? Address, string? Phone, DateOnly AdmissionDate, string? PreviousSchool, string? Notes, bool HasPhoto, StudentStatus Status, DateOnly? LeftOn, string? LeaveReason)`
- `SaveStudentRequest(string FirstName, string LastName, Gender Gender, DateOnly DateOfBirth, string? BFormNo, string? BloodGroup, string? Religion, string? Nationality, string? Address, string? Phone, DateOnly AdmissionDate, string? PreviousSchool, string? Notes)`
- `SaveGuardianRequest(string FullName, string? Cnic, string Phone, string? AltPhone, string? Email, string? Occupation, string? Address)`
- `GuardianLinkRequest(int? GuardianId, SaveGuardianRequest? NewGuardian, GuardianRelation Relation, bool IsPrimary, bool IsEmergencyContact)`
- `AdmissionEnrollmentRequest(int? SessionId, int SectionId, int? RollNo)`
- `AdmissionRequest(SaveStudentRequest Student, string? AdmissionNo, AdmissionEnrollmentRequest Enrollment, IReadOnlyList<GuardianLinkRequest> Guardians)`
- `StudentGuardianDto(int GuardianId, string FullName, string? Cnic, string Phone, string? AltPhone, string? Email, string? Occupation, GuardianRelation Relation, bool IsPrimary, bool IsEmergencyContact)`
- `EnrollmentDto(int Id, int SessionId, string SessionName, int ClassId, string ClassName, int SectionId, string SectionName, int RollNo, EnrollmentStatus Status, DateOnly StartDate, DateOnly? EndDate)`
- `EnrollmentHistoryDto(int Id, EnrollmentAction Action, string? FromSection, string? ToSection, string? Reason, DateTime OccurredOn, string? ByUserName)`
- `StudentDocumentDto(int Id, DocumentType Type, string FileName, string ContentType, long SizeBytes, DateTime UploadedOn)`
- `SiblingDto(int? StudentId, string FullName, string CampusCode, string? ClassName, string? SectionName)`. `StudentId` is null when the sibling is on a campus the caller cannot access.
- `StudentProfileDto(StudentDetailDto Student, IReadOnlyList<StudentGuardianDto> Guardians, EnrollmentDto? Enrollment, IReadOnlyList<EnrollmentHistoryDto> History, IReadOnlyList<StudentDocumentDto> Documents, IReadOnlyList<SiblingDto> Siblings)`
- `ChangeSectionRequest(int SectionId, int? RollNo, string? Reason)`
- `LeaveRequest(DateOnly LeftOn, string Reason)`
- `RejoinRequest(int? SectionId, int? RollNo)`
- `GuardianListItemDto(int Id, string FullName, string? Cnic, string Phone, int ChildrenCount)`
- `GuardianDetailDto(int Id, string FullName, string? Cnic, string Phone, string? AltPhone, string? Email, string? Occupation, string? Address, IReadOnlyList<SiblingDto> Children)`
- `GuardianLookupDto(int Id, string FullName, int ChildrenCount)`

**Seed and menu**
- **Menu:**
  - group `students-menu` ("Students", icon `users-round`, order 5)
  - its children `students` ("Students", `/admin/students`, icon `graduation-cap`, 1, CampusLevel) and `guardians` ("Guardians", `/admin/guardians`, icon `heart-handshake`, 2, CampusLevel)
  - Settings moves to order 6
- **New-module grants:** in `DatabaseSeeder.SeedAndSyncModulesAsync`, remember which module keys were **created** in this run (the `Upsert` helper returns whether it created the row). Pass that set to `RoleDefaultsSeeder.GrantNewModulesAsync(db, IReadOnlySet<string> createdKeys, ct)`. For each created key, add these default grants to the HQ system roles, and to nothing else:
  - Teacher: `students` View
  - Clerk: `students` View and Add; `guardians` View and Edit
  - Principal: both Full

  CampusAdmin is already covered by the CampusLevel top-up.

- [ ] **Step 1: Write group `schema`.** It asserts:
  - HQ `/auth/me` top-level keys are `dashboard, campuses, academics, students-menu, settings`.
  - The `students-menu` children are `students, guardians`.
  - `GET /roles/{Teacher}` has View on `students`.
  - `GET /roles/{Clerk}` has View and Add on `students`, and View and Edit on `guardians`.
  - `GET /roles/{CampusAdmin}` has full rights on both.
- [ ] **Step 2: Run it.** `python tests/e2e/students_e2e.py schema` → FAIL, because the menu has no `students-menu`.
- [ ] **Step 3: Implement** the entities, configurations, DbSets, constants, DTOs and seeding as above.
- [ ] **Step 4: Create the migration.** `dotnet ef migrations add StudentsAndGuardians --project SchoolManagementSystem-API/SchoolManagementSystem.Infrastructure --startup-project SchoolManagementSystem-API/SchoolManagementSystem.Infrastructure` → 7 new tables. No existing table is altered except by data.
- [ ] **Step 5: Verify.** Restart the API twice, then:
  - `python tests/e2e/students_e2e.py schema` → PASS.
  - After the second start, Teacher still has exactly one `students` grant, so seeding added nothing twice.
  - The regression suite still PASSES.

### Task 2: Admission, list and profile API

**Files:** `SequenceRepository.cs`, `StudentRepository.cs`, `EnrollmentRepository.cs`, `SectionRepository.cs` + `AcademicDtos.cs` (ActiveStudents), `GuardianRepository.cs` (only `GetByIdsVisibleAsync` and `FindByCnicAsync` for now), `PhoneNormalizer.cs`, `IStudentService.cs`, `StudentService.cs`, `StudentsController.cs`, aggregate, DI; groups `students` and `admission-concurrency`.

**Interfaces:**
- **Consumes:** Task 1 DTOs; `SessionRepository.GetEffectiveCurrentAsync`, `SessionRepository.GetTrackedAsync`.
- **Produces:**
  - `PhoneNormalizer.Digits(string? value) -> string` (digits only; empty for null)
  - `SequenceRepository.NextAsync(int campusId, string key, CancellationToken) -> Task<int>`. Atomic, and must run inside the caller's transaction:
    ```sql
    UPDATE CampusSequences SET NextValue = NextValue + 1 OUTPUT deleted.NextValue WHERE CampusId = @c AND [Key] = @k;
    -- no row: INSERT (CampusId, Key, NextValue) VALUES (@c, @k, 2) and return 1;
    -- on a PK violation (another insert won the race) run the UPDATE again.
    ```
  - `StudentRepository`:
    - `SearchAsync(string? q, int? classId, int? sectionId, StudentStatus? status, int page, int pageSize, CancellationToken) -> Task<PagedResult<StudentListItemDto>>`. Filtered by campus; ordered by FullName. `q` matches name, AdmissionNo, BFormNo, or a guardian's phone or CNIC digits. Class and section come from the student's current Active enrollment.
    - `GetTrackedAsync(int id, CancellationToken)`
    - `AdmissionNoTakenAsync(int campusId, string no, int? excludeId, CancellationToken)`
    - `BFormTakenAsync(string bform, int? excludeId, CancellationToken)` (ignores filters, `!IsDeleted`)
  - `EnrollmentRepository`:
    - `GetCurrentAsync(int studentId, CancellationToken) -> Task<Enrollment?>` (the latest Active enrollment, else the latest)
    - `RollTakenAsync(int sectionId, int rollNo, int? excludeEnrollmentId, CancellationToken)`
    - `NextRollAsync(int sectionId, CancellationToken) -> Task<int>` (max Active roll + 1, starting at 1)
    - `CountActiveInSectionAsync(int sectionId, CancellationToken)`
    - `AddHistory(EnrollmentHistory)`
  - `GuardianRepository`:
    - `FindByCnicAsync(string cnicDigits, CancellationToken) -> Task<Guardian?>` (ignores filters)
    - `GetTrackedAsync(int id, CancellationToken)` (ignores filters, `!IsDeleted`; the service decides visibility)
  - `IStudentService`:
    - `SearchAsync(...) -> Result<PagedResult<StudentListItemDto>>`
    - `GetProfileAsync(int id) -> Result<StudentProfileDto>`
    - `AdmitAsync(AdmissionRequest) -> Result<StudentProfileDto>`
    - `UpdateAsync(int id, SaveStudentRequest) -> Result<StudentProfileDto>`
  - `SectionDto` (academics) gains a last parameter `int ActiveStudents = 0`. `SectionRepository.ListAsync` projects the count of Active enrollments in the section. The UI shows it as section fill.
  - Controller `api/students`:
    - `GET` with query `q, classId, sectionId, status, page = 1, pageSize = 25` (pageSize at most 100)
    - `GET {id}`
    - `POST` (permission `students` Add)
    - `PUT {id}` (permission `students` Edit)

**Admission rules**, in order, inside one transaction:
1. **Campus:** `ActiveCampusId`; if it is null → 400 "Select a campus."
2. **Section:** must exist, be visible, belong to that campus, and be active. Its session must not be Closed (400 "This session is closed.").
3. **Session:** if `SessionId` is given, it must equal the section's session.
4. **Student fields:** names are required; DOB must be before today.
5. **B-Form:** a duplicate → 409.
6. **Guardians:** the list must be non-empty with exactly one `IsPrimary`, else 400 "A student must keep at least one guardian." or "Choose exactly one primary guardian."
   - A `NewGuardian` whose CNIC digits match an existing guardian → 409 "A guardian with this CNIC exists — link them instead."
   - A `GuardianId` must exist.
7. **Admission number:**
   - A typed number that is taken → 409.
   - Otherwise generate with `NextAsync(campus, "ADM-" + year)` and format it. If the generated number is already taken (it was typed earlier), keep calling `NextAsync` until a free one comes up.
8. **Roll number:** `RollNo` if given (taken → 409), otherwise `NextRollAsync`.
9. **Save:** the student, guardians, links, the Active enrollment (StartDate = AdmissionDate), and history `Admitted`, all in one `SaveChangesAsync`, then commit.

- [ ] **Step 1: Write group `students`.** Helpers: `admit(token, campus, section_id, **overrides) -> (status, body)` builds a valid `AdmissionRequest` with one new primary Father guardian, and tracks `students` and `guardians` for cleanup. Tests:
  - `admit_creates_everything`: 200. `admissionNo` matches `^MAIN-\d{4}-\d{4}$`. The profile has 1 guardian (primary), the enrollment is Active in that section with `rollNo >= 1`, and the history is `["Admitted"]`.
  - `sequence_increments`: two admissions in a row get consecutive numbers.
  - `generator_skips_typed_numbers`: admit with `admissionNo = "MAIN-<year>-<n+1>"`, where n is the last generated number. The next auto admission gets `n+2`.
  - `duplicate_bform_409`.
  - `closed_session_400`: uses a temp HQ session that is Closed, plus a section created for it before closing.
  - `inactive_section_400`.
  - `no_primary_400`: two guardians with `isPrimary` false.
  - `existing_cnic_new_guardian_409`.
  - `list_and_search`: the new student is found by `q` set to their admission number, last name, and the guardian phone.
  - `search_phone_ignores_formatting`: a guardian saved as `0300-12345xx`; searching the digits finds them.
  - `update_profile`: PUT changes the address → 200.
- [ ] **Step 2: Write group `admission-concurrency`.** 5 threads admit at the same time on campus A. All return 200 with 5 distinct admission numbers.
- [ ] **Step 3: Run both groups.** → FAIL (404).
- [ ] **Step 4: Implement** as above. The controller wires `pageSize = Math.Clamp(pageSize, 1, 100)`.
- [ ] **Step 5: Run both groups and the regression suite.** → PASS.

### Task 3: Guardians and student–guardian links

**Files:** `GuardianRepository.cs` (complete), `IGuardianService.cs`, `GuardianService.cs`, `GuardiansController.cs`, `StudentService` (guardian link methods), `StudentsController` (link routes); group `guardians`.

**Interfaces:**
- `IGuardianService`:
  - `SearchAsync(string? q, int page, int pageSize) -> Result<PagedResult<GuardianListItemDto>>`. Only guardians linked to at least one student visible to the caller; HQ on "All campuses" sees all.
  - `GetAsync(int id) -> Result<GuardianDetailDto>`. 404 unless linked to a visible student. Children on campuses the caller cannot access get `StudentId = null`.
  - `UpdateAsync(int id, SaveGuardianRequest) -> Result<GuardianDetailDto>`. The same visibility rule; a duplicate CNIC → 409.
  - `LookupAsync(string? cnic, string? phone) -> Result<IReadOnlyList<GuardianLookupDto>>`. Exact digits match only, needs at least one parameter, returns at most 5 rows.
- `IStudentService` additions:
  - `AddGuardianAsync(int studentId, GuardianLinkRequest) -> Result<StudentProfileDto>`. When `IsPrimary` is set, the existing primary is demoted. Linking a guardian already linked → 409.
  - `RemoveGuardianAsync(int studentId, int guardianId) -> Result<StudentProfileDto>`. Last guardian → 400 "A student must keep at least one guardian."; primary guardian → 400 "Choose another primary guardian first."
- **Routes:**
  - `GET/PUT api/guardians/{id}` and `GET api/guardians` (permission `guardians`)
  - `GET api/guardians/lookup` (permission `students` Add)
  - `POST api/students/{id}/guardians` and `DELETE api/students/{id}/guardians/{guardianId}` (permission `students` Edit)
- **Siblings** in `StudentProfileDto`: other non-deleted students who share any guardian. Computed with `IgnoreQueryFilters`, mapping `StudentId` to null when `!CanAccessCampus`.

- [ ] **Step 1: Write group `guardians`.** It covers:
  - `link_existing_guardian_makes_siblings`: admit S1 with a new guardian G, then S2 linking `guardianId = G`. S1's profile lists S2 as a sibling with a StudentId.
  - `cross_campus_sibling_limited`: HQ admits S3 on campus B linked to G. A campus-A admin sees S3 as a sibling with `studentId` null and campus code `EAST`. `GET /guardians/{G}` as the campus-A admin shows S3 without an id. `GET /students/{S3}` as the campus-A admin → 404.
  - `lookup_minimal`: lookup by G's CNIC returns exactly the keys `id, fullName, childrenCount`. Lookup with no parameters → 400.
  - `new_primary_demotes_old`: add a Mother with `isPrimary: true` → 200, and exactly one primary, the Mother.
  - `cannot_remove_last_or_primary`: removing the primary → 400; after linking a second guardian, removing the non-primary → 200; removing the only remaining one → 400.
  - `guardian_hidden_without_visible_student`: a guardian linked only to campus-B students → `GET /guardians/{id}` as the campus-A admin → 404, and it is not in their list.
- [ ] **Step 2: Run it.** → FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run `guardians`, `students` and the regression suite.** → PASS.

### Task 4: Section change, leave and rejoin

**Files:** `StudentService.cs`, `StudentsController.cs`, `EnrollmentRepository.cs`; group `enrollment`.

**Interfaces:**
- `IStudentService` additions, each returning `Result<StudentProfileDto>`:
  - `ChangeSectionAsync(int id, ChangeSectionRequest)`
  - `LeaveAsync(int id, LeaveRequest)`
  - `RejoinAsync(int id, RejoinRequest)`
- **Routes:** `POST api/students/{id}/change-section`, `POST api/students/{id}/leave`, `POST api/students/{id}/rejoin`, all with permission `students` Edit.

**Rules**
- **ChangeSection:**
  - A student with status Left → 400 "Rejoin the student first."
  - The target section must be in the same campus and session as the current enrollment, and active. A Closed session → 400.
  - Roll number: the given one (taken → 409), otherwise the next free one.
  - History `SectionChanged`, with from/to and the reason.
- **Leave:**
  - `LeftOn` must not be before AdmissionDate.
  - `Reason` is required.
  - The student and the current enrollment become Left, with `EndDate = LeftOn`.
  - History `Left`.
- **Rejoin:**
  - Only for students with status Left.
  - The target section is `SectionId`, or else the last enrollment's section **if** it is active and in the campus's effective current session. Otherwise → 400 "Pick a section."
  - If the target session equals the last enrollment's session, reactivate that enrollment (moving it if the section differs). Otherwise create a new enrollment.
  - The student becomes Active, and `LeftOn` and `LeaveReason` are cleared.
  - History `Rejoined`.

- [ ] **Step 1: Write group `enrollment`.** It covers:
  - `change_section_writes_history`: admit into G4-A, change to G4-B → 200; the enrollment section is B; the last history row is `SectionChanged` with from A and to B.
  - `change_section_roll_taken_409`.
  - `change_to_other_campus_section_400`.
  - `leave_then_change_section_400`: the message contains "Rejoin the student first.".
  - `leave_and_rejoin_same_section`: leave → status Left; rejoin `{}` → Active in the same section; history ends with `Left, Rejoined`.
  - `rejoin_needs_section_when_old_unusable`: leave, deactivate the old section (as HQ), rejoin `{}` → 400 "Pick a section."; rejoin with another active section → 200. Restore the section afterwards.
  - `leave_before_admission_400`.
- [ ] **Step 2: Run it.** → FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run `enrollment`, `students` and the regression suite.** → PASS.

### Task 5: Photo and documents

**Files:** `Abstractions/IFileStorage.cs`, `Api/Infrastructure/LocalFileStorage.cs`, `Services/Students/FileSignature.cs`, `IStudentFileService.cs`, `StudentFileService.cs`, `StudentsController.cs`, `appsettings.json`, `Startup.cs`, `sms_client.py` (helpers); group `documents`.

**Interfaces:**
- `IFileStorage`:
  - `Task<string> SaveAsync(int campusId, int studentId, string extension, Stream content, CancellationToken)` returns the relative path
  - `Task<Stream?> OpenAsync(string relativePath, CancellationToken)`
  - `Task DeleteAsync(string relativePath, CancellationToken)`

  `LocalFileStorage` resolves paths under `Storage:UploadsPath` and refuses any path that escapes the root.
- `FileSignature.Detect(ReadOnlySpan<byte> header) -> string?` returns `".jpg"`, `".png"`, `".webp"`, `".pdf"` or null. Magic bytes:
  - JPEG `FF D8 FF`
  - PNG `89 50 4E 47 0D 0A 1A 0A`
  - WEBP `RIFF....WEBP`
  - PDF `%PDF-`
- `IStudentFileService`:
  - `SetPhotoAsync(int studentId, IFormFile file) -> Result`
  - `GetPhotoAsync(int studentId) -> Result<(Stream Content, string ContentType)>`
  - `UploadDocumentAsync(int studentId, DocumentType type, IFormFile file) -> Result<StudentDocumentDto>`
  - `GetDocumentAsync(int studentId, int docId) -> Result<(Stream Content, string ContentType, string FileName)>`
  - `DeleteDocumentAsync(int studentId, int docId) -> Result`
- **Routes:**
  - `POST/GET api/students/{id}/photo`
  - `POST api/students/{id}/documents` (form fields `file`, `type`)
  - `GET/DELETE api/students/{id}/documents/{docId}`
  - Uploads need `students` Edit; downloads need `students` View.
  - Set `[RequestSizeLimit(11_000_000)]` on the upload actions.
- **Validation:** the extension must be allowed **and** `FileSignature.Detect` must agree with it (`.jpeg` counts as `.jpg`). PDF is not allowed for the photo. Size limits are as in Global Constraints. On failure → 400 "Only JPG, PNG, WEBP or PDF files up to 10 MB." (for the photo: "Only JPG, PNG or WEBP images up to 2 MB.").
- **`sms_client` additions:**
  - `upload(path, fields: dict, file_field: str, filename: str, content: bytes, content_type: str, token, campus=None)` builds a multipart body by hand
  - `download(path, token, campus=None) -> (status, bytes, headers)`

- [ ] **Step 1: Write group `documents`.** Tiny valid PNG and PDF bytes are built inline. It covers:
  - `photo_upload_and_fetch`: the profile has `hasPhoto` true, and GET photo returns the same bytes with `image/png`.
  - `document_upload_list_download_delete`: the PDF appears in the profile `documents`, downloads byte-identical with a Content-Disposition containing the original name, and is gone after delete.
  - `renamed_exe_refused`: bytes `MZ...` uploaded as `x.pdf` → 400.
  - `photo_pdf_refused`.
  - `oversize_refused`: 2,097,153 bytes of PNG header plus padding sent as the photo → 400.
  - `deleted_student_files_404`: upload a document, then delete the student via `DELETE /students/{id}`. That endpoint does not exist yet, so add it in this task with permission `students` Delete; it soft-deletes the student and their enrollments. The photo and document downloads → 404.
- [ ] **Step 2: Run it.** → FAIL.
- [ ] **Step 3: Implement**, including `DELETE api/students/{id}`. Make sure the `App_Data/` folder stays ignored by git.
- [ ] **Step 4: Run `documents` and the regression suite.** → PASS.

### Task 6: Cross-campus isolation sweep and dashboard KPI

**Files:** `DashboardService.cs`, plus any fix the tests reveal; groups `student-isolation` and `dashboard-students`.

- [ ] **Step 1: Write group `student-isolation`.** Setup: a campus-B student (with a document and a guardian), and a campus-A Campus Admin. As the campus-A admin, every one of these must return 404 and change nothing:
  - `GET /students/{b}`, `PUT`, `DELETE`
  - `change-section`, `leave`, `rejoin`
  - `POST /students/{b}/guardians`
  - `GET /students/{b}/photo`
  - `GET /students/{b}/documents/{doc}`
  - `GET /guardians/{gB}`

  Also: the list with `q` set to B's admission number returns 0 items. HQ on "All campuses" lists both, with campus codes.
- [ ] **Step 2: Write group `dashboard-students`.** The KPI id `students` has a value equal to the Active count from `GET /students?status=Active` (its total), and the hint matches `^\d+ admitted this month$`. With the campus-A header, the count matches A's total.
- [ ] **Step 3: Run both.** → the dashboard group FAILS (no KPI). The isolation group must pass; any failure is a real bug, so fix it in the owning service.
- [ ] **Step 4: Implement** the KPI: `new("students", "Students", active.ToString(), $"{admittedThisMonth} admitted this month", "primary", "/admin/students")`, inserted after `academics`.
- [ ] **Step 5: Run every group in `students_e2e.py` plus the full `academics_e2e.py` suite.** → all PASS.

### Task 7: UI foundation and Students list

**Files:** `src/lib/types.ts`, `src/lib/api.ts`, `src/App.tsx`, `src/components/app-shell.tsx`, `src/components/student-photo.tsx`, `src/pages/dashboard.tsx`, `src/pages/students/list.tsx`.

**Interfaces (produces):**
- TS types mirroring every Task 1 DTO, using the same camelCase names, and the enum string unions.
- Label maps: `genderLabel`, `relationLabel`, `documentTypeLabel`.
- `downloadBlob(url: string): Promise<{ blob: Blob; fileName: string | null }>` in `api.ts`. It uses the axios instance with `responseType: "blob"` and reads the file name from `Content-Disposition`.
- `StudentPhoto({ studentId, name, hasPhoto, size })` loads `/students/{id}/photo` as an object URL when `hasPhoto`, and otherwise falls back to `Avatar`. It revokes the URL on unmount.
- **Routes:** `students`, `students/new`, `students/:id`, `guardians`, `guardians/:id`.
- `iconMap` adds `users-round` and `heart-handshake`. The dashboard `kpiStyle` adds `students` (GraduationCap, info).

- [ ] **Step 1: Students list page.**
  - **Header:** a "New admission" button when the user can do `students` Add and has a working campus. On "All campuses" the button is disabled with the title "Pick a campus first".
  - **Toolbar:**
    - `SearchField` with the placeholder "Name, admission no., B-Form or guardian phone", debounced 300 ms
    - class `Select`, section `Select` (sections of the chosen class in the effective session), and status `Select` (Active default, Left, All)
  - **Table columns:** Student (`StudentPhoto` with name and admission number in mono), Class (class + section), Roll, Guardian (name and phone), Status pill, and Campus (only on "All campuses").
  - Clicking a row navigates to the profile.
  - **Pagination:** a footer with "Showing x–y of total" and Prev/Next.
  - **Empty state** with a `GraduationCap` icon.
- [ ] **Step 2: Verify.**
  - `npm --prefix SchoolManagementSystem-UI run build` exits with 0.
  - In the browser, as HQ: the sidebar shows the Students group, and the list renders (empty or populated).
  - As a Campus Admin, search finds a student created through the API.
  - No console errors.

### Task 8: Admission stepper

**Files:** `src/pages/students/admission.tsx`.

- [ ] **Step 1: Build the page** (`/admin/students/new`).
  - A three-step stepper header ("Student", "Guardians", "Class & section") with completed steps ticked. Back/Next validate the current step. Everything stays on one page.
  - **Step 1, Student:**
    - fields first name, last name, gender, date of birth, B-Form, blood group, religion, nationality, address, phone, admission date (defaults to today), previous school, notes
    - a photo picker with a preview (client-side check: type and ≤ 2 MB)
  - **Step 2, Guardians:**
    - Each guardian card has relation, a CNIC field and a phone field.
    - On blur of CNIC or phone, call `/guardians/lookup`. A match shows an inline banner "Link existing: {name} ({n} children)" with **Link** / **Ignore**. Linking collapses the card to the linked guardian's name.
    - Unlinked cards collect full name, alt phone, email and occupation.
    - A radio picks the primary guardian, and a checkbox marks the emergency contact.
    - "+ Add guardian" adds another card. At least one guardian is required.
  - **Step 3, Class & section:**
    - session (defaults to the effective current one)
    - class
    - section, each option showing "A · 31/35" from `section.activeStudents` and `section.capacity` (`GET /sections?sessionId=`)
    - roll number (placeholder "Next free")
  - **Summary card**, then **Admit student**:
    1. `POST /students`
    2. if a photo was picked, `POST /students/{id}/photo`
    3. toast "Admitted · {admissionNo}"
    4. navigate to the profile
  - Server errors show inline at the top of the step they belong to.
- [ ] **Step 2: Verify.**
  - The build exits with 0.
  - **In the browser, as a Campus Admin:** admit a student with a new guardian and a photo. The profile opens and the admission number is shown.
  - Admit a sibling by typing the same guardian CNIC: the link banner appears; link it, admit, and the first student's profile shows the sibling.
  - Clean up through the API.

### Task 9: Student profile

**Files:** `src/pages/students/profile.tsx`.

- [ ] **Step 1: Build the page** (`/admin/students/:id`).
  - **Header:** large `StudentPhoto` (click to replace when the user can do Edit), name, admission number (mono), "Class – Section · Roll n" badge, status pill. Actions: Edit, Change section, and Leave school (or Rejoin when Left).
  - **Tabs:**
    - **Overview:** grouped read-only fields. Edit opens a `Modal` with the same fields as admission step 1.
    - **Guardians:**
      - cards (name, relation, primary/emergency pills, phone as a `tel:` link, CNIC, email)
      - a Remove action on non-primary guardians, with confirm
      - "Add guardian" (the same card UI as admission, lookup included)
      - a Siblings list: links for siblings with an id; others show "{name} · {campus code}"
    - **Documents:**
      - a drag-and-drop zone with a type `Select` (client checks type and ≤ 10 MB)
      - a list showing type pill, file name, size and date, with Download (via `downloadBlob`, saved with the original name) and Delete (confirm)
    - **History:** a vertical timeline, newest first, with an icon per action, the from → to sections, the reason, the date and who did it.
  - **Dialogs:**
    - Change section: section `Select` within the same class and session first, then other classes; roll number; reason.
    - Leave: date (default today) and a required reason.
    - Rejoin: section `Select`, prefilled with the old section when it is usable.
- [ ] **Step 2: Verify.**
  - The build exits with 0.
  - **In the browser, on the student admitted in Task 8:**
    - change the section, and the history shows it
    - upload a PDF, download it, delete it
    - leave, then rejoin
    - add and remove a second guardian
    - check that dark mode looks right
  - No console errors.

### Task 10: Guardians pages, dashboard and final check

**Files:** `src/pages/guardians/list.tsx`, `src/pages/guardians/detail.tsx`.

- [ ] **Step 1: Guardians list.** Search, then a table with name, CNIC (masked as `•••••-•••••••-X` except the last digit), phone and children count. A row click opens the detail page.
- [ ] **Step 2: Guardian detail.** An editable form (Save when the user can do `guardians` Edit), then the children list: `StudentPhoto`, name, campus code and class. Linked when there is a `studentId`, plain text otherwise.
- [ ] **Step 3: Verify.**
  - The build exits with 0.
  - The browser walk-through as HQ and as a Campus Admin covers the guardian list, detail, editing the phone, and the dashboard Students card linking to the list.
  - Run every e2e group in both files → all PASS.
  - Remove the temporary data created during the walk-through.
