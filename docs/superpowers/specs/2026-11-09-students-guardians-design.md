# Students and Guardians: Design

**Date:** 2026-11-09
**Status:** Draft. Awaiting review.
**Depends on:** campus isolation (`ICampusScoped`), Campus Admin and role ownership, and academic setup (sessions, classes, sections). All of these are on `main`.
**Repo rule:** work on `main` and do not commit (product owner's instruction).

## 1. Purpose and scope

Each campus admits and manages its own students. Guardians (parents) are school-wide records, so one parent can be linked to children on different campuses. Every student has one enrollment per session. The enrollment ties the student to a section, a roll number and a status. Attendance, fees and exams will all be built on enrollments later.

The work is delivered in two parts. They share this spec but get separate plans:

- **Part A (this plan):**
  - students, guardians and sibling links
  - one-step admission
  - profile with photo and documents
  - section change, leave / rejoin
  - search and lists
  - a dashboard KPI
- **Part B (later plan):**
  - campus transfer: campus A → campus B, keeping history
  - bulk promotion of a class into the next session

## 2. Decisions (agreed with the product owner)

| Topic | Decision |
|---|---|
| Admission flow | Direct admission. Campus staff fill one form and the student is enrolled at once. Inquiry and online applications are out of scope. |
| Guardians | A separate school-wide record. One guardian can have many children, on any campus. Siblings are students who share a guardian. |
| Model | A student plus one **enrollment per session**, with a history line for every change (option 1). |
| Lifecycle | Part A: section change, leave / withdraw, rejoin. Part B: campus transfer, promotion. |
| Files | A profile photo and documents (B-Form, birth certificate, previous result, leaving certificate, other). |
| Storage | Local server folder outside the web root. Cloud storage comes later. |
| UI | The existing Clean SaaS kit and app shell. |

## 3. Data model

All tables inherit `AuditableEntity` (audit columns and soft delete) unless noted.

### `Students` (`ICampusScoped`; CampusId is the student's current campus)
| Column | Type | Notes |
|---|---|---|
| CampusId | FK | Stamped from the working campus at admission |
| AdmissionNo | nvarchar(30) | Unique per campus. Generated as `{CampusCode}-{yyyy}-{0001}` unless typed |
| FirstName, LastName | nvarchar(100) | Required |
| Gender | enum `Male / Female / Other` | Required |
| DateOfBirth | date | Required, must be in the past |
| BFormNo | nvarchar(20)? | Unique among non-deleted rows when present |
| BloodGroup, Religion, Nationality | nvarchar(20/50/50)? | |
| Address | nvarchar(500)? | |
| Phone | nvarchar(50)? | |
| AdmissionDate | date | Required, defaults to today |
| PreviousSchool | nvarchar(200)? | |
| Notes | nvarchar(2000)? | |
| PhotoPath | nvarchar(300)? | Relative stored path |
| Status | enum `Active / Left` (Part B adds `TransferredOut / Graduated`) | |
| LeftOn | date? | |
| LeaveReason | nvarchar(500)? | |

### `Guardians` (school-wide, not campus-scoped)
| Column | Type | Notes |
|---|---|---|
| FullName | nvarchar(150) | Required |
| Cnic | nvarchar(20)? | Unique among non-deleted rows when present. Stored digits-only |
| Phone | nvarchar(50) | Required |
| AltPhone, Email, Occupation | | Optional |
| Address | nvarchar(500)? | |

A guardian is visible to a campus user only when it is linked to a student that the user can see. The lookup endpoint (§5) is the only exception.

### `StudentGuardians` (join, no audit)
StudentId, GuardianId (composite PK), Relation (`Father / Mother / Guardian / Other`), IsPrimary, IsEmergencyContact.

- Every student has at least one guardian.
- Every student has exactly one primary guardian.

### `Enrollments` (`ICampusScoped`)
| Column | Notes |
|---|---|
| StudentId, AcademicSessionId, SectionId, CampusId | The section must belong to the same campus and session |
| RollNo | int. Unique per (section) among active enrollments |
| Status | `Active / Left` (Part B adds `Promoted / TransferredOut`) |
| StartDate, EndDate? | |

- A student has at most one enrollment per session (unique on StudentId and AcademicSessionId).
- The "current" enrollment is the student's enrollment in the campus's effective current session.

### `EnrollmentHistory` (no soft delete; append-only)
- Columns: EnrollmentId, StudentId, Action (`Admitted / SectionChanged / Left / Rejoined`), FromSectionId?, ToSectionId?, Reason?, OccurredOn, ByUserId.
- Rows are written by the services, never edited.

### `StudentDocuments`
StudentId, Type (`BForm / BirthCertificate / PreviousResult / LeavingCertificate / Other`), FileName (original name), ContentType, SizeBytes, StoredPath, UploadedOn, UploadedBy.

### `CampusSequences` (no audit)
- Columns: CampusId, Key (e.g. `ADM-2026`), NextValue. PK is (CampusId, Key).
- Admission numbers come from an atomic `UPDATE ... OUTPUT` increment inside the admission transaction, so two admissions at once never get the same number.

## 4. Permissions and menu

- **Menu:** a new group **Students** (icon `users-round`, placed after Academics) with two pages:
  - `students` (Students, `/admin/students`, icon `graduation-cap`)
  - `guardians` (Guardians, `/admin/guardians`, icon `heart-handshake`)
- **CampusLevel:** both modules are `CampusLevel = true`, so CampusAdmin gets full rights automatically.
- **Default grants:**
  - Teacher: View on `students`
  - Clerk: View and Add on `students`; View and Edit on `guardians`
  - Principal: full on both

  The existing defaults seeder only fills roles that have no permissions yet, so these grants would never reach existing roles. Rule: when the seeder **creates** a module that did not exist before that run, it also adds that module's default grants to the HQ system roles. Later runs see the module already exists and add nothing, so HQ's edits are never overwritten.

## 5. API

All responses use `ResponseDto<T>`. Students, enrollments and documents are campus-filtered automatically.

**Students**
- `GET /api/students?q=&classId=&sectionId=&status=&page=1&pageSize=25` returns `{ items, total }`.
  - `q` matches name, admission number, B-Form, or a guardian's phone or CNIC.
  - Items: id, admissionNo, fullName, gender, photo flag, class, section, roll number, primary guardian name and phone, status, and campus code.
- `GET /api/students/{id}` returns the full profile, guardians (with relation and primary flag), the current enrollment, history, documents, and siblings.
  - A sibling on another campus shows only name and campus code, with no id link.
- `POST /api/students` (admission) is one transaction. Body:
  - the student fields
  - `admissionNo?`
  - `enrollment { sessionId?, sectionId, rollNo? }`
  - `guardians: [{ guardianId? | newGuardian{...}, relation, isPrimary, isEmergencyContact }]`

  The roll number defaults to the next free one in the section. The response is the profile.
- `PUT /api/students/{id}` edits the profile fields only.
- `POST /api/students/{id}/change-section` takes `{ sectionId, rollNo?, reason? }`. The new section must be in the same session and campus.
- `POST /api/students/{id}/leave` takes `{ leftOn, reason }`. It sets the student and the current enrollment to Left.
- `POST /api/students/{id}/rejoin` takes `{ sectionId?, rollNo? }`. It reactivates the student, using the same section when it still exists and is active.
- `POST /api/students/{id}/guardians` takes `{ guardianId? | newGuardian, relation, isPrimary, isEmergencyContact }`.
- `DELETE /api/students/{id}/guardians/{guardianId}` is refused when the guardian is the last one or the primary one.

**Guardians**
- `GET /api/guardians?q=&page=&pageSize=` lists guardians of visible students.
- `GET /api/guardians/{id}` returns the guardian with their children. Children on other campuses show name and campus code only.
- `PUT /api/guardians/{id}` edits the guardian.
- `GET /api/guardians/lookup?cnic=&phone=` finds exact matches only, for the admission form. It returns `{ id, fullName, childrenCount }` and nothing else.

**Files**
- `POST /api/students/{id}/photo` (multipart; JPG, PNG or WEBP, 2 MB max) replaces the photo.
- `GET /api/students/{id}/photo` streams the photo after the permission and campus checks.
- `POST /api/students/{id}/documents` (multipart: file, type; JPG, PNG, WEBP or PDF, 10 MB max).
- `GET /api/students/{id}/documents/{docId}` downloads with `Content-Disposition` set to the original name.
- `DELETE /api/students/{id}/documents/{docId}`.

**Storage rules**
- Files go in `App_Data/uploads/{campusId}/{studentId}/{guid}{ext}`. The root comes from config `Storage:UploadsPath`.
- The type is checked by extension and magic bytes.
- Nothing under the folder is served statically.
- Deleting a student soft-deletes the rows. The files stay on disk.

**Dashboard:** a "Students" KPI shows the active count in scope, with the hint "N admitted this month". It links to `/admin/students`.

## 6. UI

- **Students list:**
  - Search box and filters for class, section and status.
  - A table: avatar or photo with name and admission number, class-section, roll number, primary guardian and phone, status pill. On "All campuses" a campus column is added.
  - Pagination, and a **New admission** button.
- **New admission** (`/admin/students/new`) is a three-step stepper on one page:
  1. **Student:** details and photo picker with a preview.
  2. **Guardians:** typing a CNIC or phone runs the lookup and offers "Link existing: name (n children)". Otherwise the user fills in a new guardian. More guardians can be added. Each gets a relation and one radio for primary.
  3. **Class & section:**
     - session (the current one selected)
     - class
     - section, shown with its capacity ("A · 31/35")
     - roll number, with the next free one prefilled

  A summary closes the flow, then **Admit student** opens the profile with a toast showing the admission number.
- **Student profile** (`/admin/students/:id`):
  - The header shows photo, name, admission number, class-section, status and actions (Edit, Change section, Leave / Rejoin).
  - Tabs:
    - **Overview**
    - **Guardians**: cards, siblings, add or remove
    - **Documents**: drag-and-drop upload, list, download, delete
    - **History**: timeline
- **Guardians:** a list with search. The detail page shows editable fields and the children list.
- **Dashboard:** the Students KPI.

## 7. Errors

| Case | Response |
|---|---|
| Duplicate admission number or B-Form | 409 |
| Duplicate guardian CNIC on create | 409 "A guardian with this CNIC exists — link them instead." |
| No guardian, or not exactly one primary | 400 |
| Admission or section change into a Closed session or inactive section | 400 |
| Section from another campus or session | 400 |
| Roll number taken in the section | 409 |
| Student, document or guardian of another campus | 404 |
| Section change for a Left student | 400 "Rejoin the student first." |
| Wrong file type or file too large | 400, stating the allowed types and limit |
| Removing the last or the primary guardian | 400 |

## 8. Testing

- One migration, `StudentsAndGuardians`. Running the seeder twice must change nothing.
- End-to-end groups in `tests/e2e/` (temp data, cleaned up):
  - **`students`:** admission creates a student, guardian, enrollment and history in one call; the admission number format and sequence; duplicate B-Form; closed session; inactive section.
  - **`guardians`:** linking an existing guardian; siblings across campuses show limited fields; lookup returns only the minimal fields; the last guardian cannot be removed.
  - **`enrollment`:** section change writes history; duplicate roll number; leave and rejoin; changing the section of a Left student is refused.
  - **`student-isolation`:** a campus-A user cannot list, read, edit or download a campus-B student, document or guardian.
  - **`documents`:** photo and PDF upload and download; a wrong type or oversize file is refused; a renamed `.exe` is refused by the magic-byte check.
  - **`admission-concurrency`:** five parallel admissions on one campus get five distinct numbers.
- UI: `npm run build`, then a browser walk-through as HQ and as a Campus Admin. It covers admission, profile, section change, leave and rejoin, document upload and download, and the guardians page.

## 9. Part B outline (separate plan later)

- **Campus transfer.**
  - HQ only, or a user who holds both campuses.
  - The Active enrollment in A becomes `TransferredOut`, and a new enrollment opens in a B section.
  - The student's CampusId moves to B, and the admission number is kept.
  - A history line is written on both sides.
- **Promotion.**
  - Pick a session-to-session pair and a class.
  - Map each section to a target section.
  - Choose per student: promote, retain, or leave.
  - Old enrollments become `Promoted`; new ones are `Active`.
  - A dry-run preview comes first, as with section copy.

## 10. Out of scope

Inquiry and online applications, ID cards and printing, the parent portal, student login, bulk import from Excel, and cloud storage.
