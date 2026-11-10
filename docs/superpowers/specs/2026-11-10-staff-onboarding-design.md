# Staff: Onboarding (Part 2) — Design

Follows `2026-11-10-staff-hiring-design.md` (Part 1, built). Part 1 creates a basic `Staff` row with status `Onboarding` when a candidate is hired. Part 2 completes the staff member: full profile, documents, a fixed onboarding checklist, a login account, teaching assignments and the status lifecycle. Staff can also be onboarded directly, without hiring.

## 1. Goals

- A campus can add staff directly (old staff, referrals) or complete hired staff.
- Every staff member has a full profile: personal, emergency contact, qualification, bank, documents.
- A fixed four-item checklist drives `Onboarding → Active`.
- A staff member can get a login (new or linked) from their profile, without role escalation.
- Teachers get (section, subject) assignments in the current session, and each section can have one class teacher.
- Leaving (Resigned / Terminated) disables the login and frees the teacher's classes.
- Two Part 1 review items are settled: rehiring a former staff member, and hiring beyond a vacancy's positions.

## 2. Decisions (agreed)

| Topic | Decision |
|---|---|
| Centre of the model | **Staff**. The login is optional (`Staff.UserId`); assignments hang off `StaffId`, so a teacher without a login can still be assigned. |
| Checklist | Fixed four items for every campus: Documents received, Contract signed, Login created, Orientation done. |
| Documents received | Ticked automatically when the required documents are uploaded: CNIC copy for everyone, plus a Degree / certificate for Teaching staff. |
| Login created | Ticked automatically when a login is created or linked. |
| Contract signed, Orientation done | Ticked (and unticked) by hand. |
| Activation | When all four items are done, status moves `Onboarding → Active` automatically. |
| Login | From the profile: create a new user (email, temporary password, role, `MustChangePassword`) or link an existing user of the campus who is not linked to other staff. |
| Teaching assignment | Current-session (section, subject) pairs per teacher. One teacher per (section, subject). A teacher can have any number of pairs across classes and subjects. The subject must be in that class's subject list. |
| Class teacher | One per section (`Section.ClassTeacherStaffId`). |
| Leaving | Resigned / Terminated with a date and reason: login deactivated, assignments ended (kept as history), class-teacher slots cleared. |
| Rehire | The same CNIC may be hired again once the earlier staff record is Resigned or Terminated. |
| Over-hire | Hire is refused when the vacancy is not Open or its positions are all hired: "All positions are filled. Raise the positions first." / "This vacancy is not open." |
| Profile fields | Personal (gender, date of birth, address, photo), emergency contact, qualification and experience, bank account (masked on screens). |

## 3. Data model

All new tables inherit `AuditableEntity` (audit columns and soft delete) and are `ICampusScoped` unless noted. Enums are stored as strings.

### `Staff` (new columns)

| Column | Type / rule |
|---|---|
| Gender | `Gender?` (reuses the students' enum `Male / Female / Other`) |
| DateOfBirth | date? (in the past, age 16–80 when set) |
| Address | nvarchar(300)? |
| PhotoPath | nvarchar(300)? (JPG / PNG / WEBP, 2 MB, magic bytes) |
| EmergencyName, EmergencyRelation, EmergencyPhone | nvarchar(150)? / nvarchar(50)? / nvarchar(50)? |
| Qualification | nvarchar(200)? (copied from the candidate on hire) |
| ExperienceYears | decimal(4,1)? (copied from the candidate on hire) |
| PreviousEmployer | nvarchar(200)? |
| BankName | nvarchar(100)? |
| BankAccount | nvarchar(34)? (account number or IBAN; stored as typed, shown masked `****` + last 4) |
| UserId | int?, FK `Users`. Unique filtered index `[UserId] IS NOT NULL AND [IsDeleted] = 0` |
| LeftOn | date? |
| LeaveReason | nvarchar(500)? |
| ContractSigned, OrientationDone | bool (manual checklist items) |
| ContractSignedOn, OrientationDoneOn | datetime2? |

**Index change:** the unique `Staff(Cnic)` index becomes `[IsDeleted] = 0 AND [Cnic] IS NOT NULL AND [Status] NOT IN ('Resigned','Terminated')`, so it matches the hire check and allows rehire.

The checklist's two automatic items are computed, not stored:
- **DocumentsReceived** = a non-deleted `Cnic` document exists, and for `Category = Teaching` a non-deleted `Degree` document exists.
- **LoginCreated** = `UserId` is set.

### `StaffDocument`

| Column | Type / rule |
|---|---|
| StaffId | int, FK |
| Type | enum `Cnic / Degree / ExperienceLetter / Contract / Photo / Other` |
| FileName | nvarchar(255) |
| ContentType | nvarchar(100) |
| SizeBytes | bigint |
| StoredPath | nvarchar(300), under `{campusId}/staff/{staffId}/` |
| UploadedOn, UploadedBy | datetime2 (UTC), int? |

Allowed files: `.pdf .jpg .jpeg .png .webp`, at most 10,485,760 bytes, magic bytes must match the extension (existing `FileSignature`).

### `TeachingAssignment`

| Column | Type / rule |
|---|---|
| StaffId | int, FK |
| AcademicSessionId | int, FK (the section's session) |
| SectionId | int, FK |
| SubjectId | int, FK |
| StartedOn | date |
| EndedOn | date? (set when ended; ended rows are history) |
| EndReason | nvarchar(200)? |

Unique filtered index `(SectionId, SubjectId)` where `[EndedOn] IS NULL AND [IsDeleted] = 0`: one current teacher per section subject.

### `Section` (new column)

`ClassTeacherStaffId` int?, FK `Staff`. A staff member may be class teacher of more than one section.

## 4. Rules

**Direct onboarding** (`POST /api/staff`):
- Needs a working campus ("Select a campus.").
- Required: first name, last name, phone, category, designation, employment type, joining date, basic salary > 0.
- CNIC (13 digits when given) must not belong to active staff on any campus (409 "This CNIC belongs to an active staff member.").
- Employee number is the next `{CampusCode}-EMP-{0000}` (sequence key `EMP`, as in Part 1). Status `Onboarding`, `SourceCandidateId` null.

**Hire (Part 1) changes:**
- Copies `Qualification` and `ExperienceYears` from the candidate.
- Refuses when the vacancy is not Open (400 "This vacancy is not open.") or hired ≥ positions (400 "All positions are filled. Raise the positions first.").
- Reopening a vacancy (any status → Open) requires positions > hired.

**Checklist:**
- `POST /api/staff/{id}/checklist/{item}` with `{ done }` for `contract` and `orientation` only (the other two are automatic → 400 if posted).
- After any change that can affect the checklist (document upload or delete, login create or link, manual tick), if the status is `Onboarding` and all four are done, the status becomes `Active`.
- Unticking or deleting a document does not move an `Active` member back to `Onboarding`.

**Login account:**
- Needs `staff:Edit` and `users:Add`.
- Create: `{ email, password, roleId }`. The role must be one the caller may assign (same rule as the Users page, `/roles?assignable=true`). The user is created active, with `MustChangePassword = true`, assigned to the staff member's campus only, `IsHq = false`, first/last name and phone copied from the staff member.
- Link: `{ userId }`. The user must be active, belong to the staff member's campus, not be a Main Campus user, and not be linked to other non-deleted staff.
- A staff member with a login → 409 "This staff member already has a login."
- Email taken → 409 (existing users rule).
- Not allowed when status is Resigned / Terminated.
- Unlinking is out of scope (manage on the Users page).

**Teaching assignments:**
- Only `Category = Teaching` and status `Onboarding / Active / OnLeave` (400 otherwise).
- Add `{ sectionId, subjectIds[] }`: the section must be in the campus's current session and active; each subject must be in the section's class subject list (400 "Subject X is not taught in Grade 6."); a pair already held by another teacher → 409 "Grade 6-A Mathematics is already taught by {name}."; a pair already held by this teacher is ignored.
- End `DELETE /api/staff/{id}/assignments/{assignmentId}`: sets `EndedOn = today`, `EndReason = "Removed"`.
- Class teacher `PUT /api/sections/{id}/class-teacher` `{ staffId | null }` (needs `sections:Edit` or `staff:Edit`): staff must be Teaching, on the section's campus, not left.

**Status lifecycle** (`POST /api/staff/{id}/status` `{ status, date, reason }`):

| From | To |
|---|---|
| Onboarding | Resigned, Terminated (Active only via the checklist) |
| Active | OnLeave, Resigned, Terminated |
| OnLeave | Active, Resigned, Terminated |
| Resigned, Terminated | — (final; rehire creates a new staff record) |

- Resigned / Terminated need a date (not before joining, not in the future beyond 31 days) and a reason (400 "A reason is required.").
- Leaving, in one transaction: set `LeftOn`, `LeaveReason`; deactivate the linked user (if any) and revoke their refresh tokens; end all current assignments (`EndReason = "Left"`); clear `ClassTeacherStaffId` on their sections.

**Delete** (`DELETE /api/staff/{id}`, `staff:Delete`): only directly onboarded staff (`SourceCandidateId` null) in `Onboarding` with no login and no assignments; otherwise 400.

## 5. API

All responses use `ResponseDto<T>`, and everything is campus-filtered (another campus's staff → 404).

- `GET /api/staff?q=&status=&category=&page=&pageSize=` (Part 1, adds `category`)
- `GET /api/staff/{id}` → `StaffProfileDto` (detail with masked bank account, checklist, documents, current assignments, class-teacher sections, account summary)
- `POST /api/staff` (Add), `PUT /api/staff/{id}` (Edit; the bank account is sent in full on edit only), `DELETE /api/staff/{id}` (Delete)
- `POST /api/staff/{id}/photo`, `GET /api/staff/{id}/photo`
- `POST /api/staff/{id}/documents` (multipart `type`, `file`), `GET /api/staff/{id}/documents/{docId}`, `DELETE /api/staff/{id}/documents/{docId}`
- `POST /api/staff/{id}/checklist/{item}` `{ done }`
- `POST /api/staff/{id}/status`
- `POST /api/staff/{id}/account` (create), `POST /api/staff/{id}/account/link`
- `GET /api/staff/{id}/assignments/options` → current-session sections with their class subjects and current teacher per subject (for the Assign dialog)
- `POST /api/staff/{id}/assignments`, `DELETE /api/staff/{id}/assignments/{assignmentId}`
- `PUT /api/sections/{id}/class-teacher`
- `GET /api/sections/{id}` and the sections list gain `classTeacherName` and, on detail, per-subject `teacherName`.

The full bank account is returned only by `GET /api/staff/{id}/bank` (`staff:Edit`) for the edit form; every other response masks it.

## 6. Permissions and menu

- No new modules. `staff` (CampusLevel) already exists: Principal and CampusAdmin full; Teacher, Clerk, Accountant none.
- Login creation needs `staff:Edit` + `users:Add` (two attributes, AND).
- Class teacher endpoint: `[HasAnyPermission("sections:Edit", "staff:Edit")]`.
- Assignment options read sections/subjects through the staff endpoint, so `staff:Edit` is enough.

## 7. UI

- **Staff list:** "Add staff" (needs `staff:Add` and a working campus), status and category filters, rows open the profile. The Part 1 read-only note is removed.
- **Add / Edit staff dialog:** sections Basic, Personal, Emergency contact, Job & salary, Qualification, Bank. Designation sets the category (Teacher, Principal, Coordinator → Teaching), as on vacancies.
- **Staff profile** (`/admin/staff/:id`):
  - Header: photo (click to change), name, employee no. (mono), designation, status pill, campus code; actions Edit, Mark on leave / Back from leave, Resign / Terminate (dialog with date and reason), Delete (when allowed).
  - **Overview:** details grouped; bank account masked.
  - **Documents:** upload with type, list, download, delete; shows which required documents are missing.
  - **Checklist:** four rows with done state, who/when, a progress bar; Contract and Orientation have toggles; automatic rows explain themselves ("Upload CNIC and Degree", "Create a login on the Account tab").
  - **Teaching** (Teaching staff): current assignments grouped by section, "Assign classes" dialog (pick section → tick subjects; subjects held by others show "Taught by X" and are disabled), end assignment, class-teacher sections.
  - **Account:** no login → "Create login" (email, temporary password with generator, role select from assignable roles, defaulting from designation: Teacher → Teacher, Accountant → Accountant, Clerk → Clerk, Principal → Principal) or "Link existing user" (campus users without staff); with a login → email, role names, active state, "Open in Users".
- **Sections page:** class teacher shown on each section, with a "Set class teacher" select; section detail lists subjects with their teacher or "No teacher".
- **Candidate profile (Part 1):** after hire, "View staff" opens the new staff profile.

## 8. Errors

| Case | Response |
|---|---|
| Direct onboarding without a working campus | 400 "Select a campus." |
| CNIC of active staff (onboarding or hire) | 409 "This CNIC belongs to an active staff member." |
| Hire on a non-Open vacancy / positions full | 400 |
| Manual tick of an automatic checklist item | 400 |
| Second login for a staff member | 409 "This staff member already has a login." |
| Role the caller may not assign | 403 |
| Link a user of another campus / Main Campus / already linked | 400 / 400 / 409 |
| Assignment for non-teaching or left staff | 400 |
| Subject not in the class | 400 |
| Section subject already taught by another teacher | 409 naming the teacher |
| Section not in the current session | 400 |
| Leaving without date or reason | 400 |
| Status move not in the table | 400 listing allowed moves |
| Delete not allowed | 400 |
| Another campus's staff, document, photo, assignment | 404 |
| Wrong file type or too large | 400 |

## 9. Testing

- One migration, `StaffOnboarding`. Seeding twice changes nothing.
- E2E file `tests/e2e/onboarding_e2e.py` (reuses `sms_client`, cleans up). Groups:
  - **`direct`:** add staff, employee number format, CNIC conflict with active staff, edit, masked bank account vs `/bank`
  - **`documents`:** upload / download / delete, wrong type, size, photo
  - **`checklist`:** automatic items (CNIC only is not enough for teaching staff), manual toggles, auto-activation, no fallback to Onboarding
  - **`account`:** create login (MustChangePassword, campus, role), unassignable role → 403, link existing, second login → 409, login works and must change password
  - **`assignments`:** multiple classes and subjects for one teacher, duplicate pair → 409 naming the teacher, subject not in class → 400, non-teaching → 400, end assignment, options endpoint, class teacher set / clear, section shows teachers
  - **`lifecycle`:** OnLeave and back, resign needs date and reason, leaving deactivates login (login → 401/403), ends assignments, clears class teacher; rehire same CNIC succeeds
  - **`hire-rules`:** hire on full or closed vacancy refused; reopen needs positions > hired; qualification copied on hire
  - **`onboarding-isolation`** and **`onboarding-permissions`** (Teacher / Clerk / Accountant 403; Principal 200; login creation needs users:Add)
- Regression: all hiring groups and both older suites.
- UI: `npm run build`; browser walk-through as Campus Admin (direct onboarding → documents → checklist → login → assignments → Active → resign), mobile and dark mode.

## 10. Out of scope

Payroll and salary history, staff attendance (next module), teacher-only views ("my classes"), staff on several campuses, timetable / periods, unlinking a login from the profile, document expiry reminders.
