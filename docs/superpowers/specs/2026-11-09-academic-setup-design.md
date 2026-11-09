# Campus Admin and Academic Setup: Design

**Date:** 2026-11-09
**Status:** Draft, revision 2: adds Campus Admin and HQ + campus ownership. Awaiting review.
**Depends on:** campus isolation layer (`CampusScope`, `ICampusScoped`, HQ checks) and the Clean SaaS UI kit, both merged on `main`.

## 1. Purpose

Each campus runs as its own school under head office (HQ). HQ appoints one **Campus Admin** per campus. When that admin signs in, they run **everything about their campus**: its profile, its team, its own roles, academic setup, and later its students, attendance, fees and exams. They never see another campus's data.

HQ keeps **school-wide defaults** (sessions, classes, subjects, roles) that every campus gets. A campus can add its **own** items on top of those defaults, but cannot change HQ's.

### Module roadmap

Each module gets its own spec, plan and implementation, and builds on the ones before it:

1. **Campus Admin, role ownership and academic setup** (this spec)
2. Students and guardians: admission into a section, admission number per campus
3. Staff and teachers: profiles, class teacher on a section
4. Attendance: daily, per section
5. Fees: heads, class-wise structure, vouchers, collection
6. Exams and results: per class subject

## 2. Decisions (agreed with the product owner)

| Topic | Decision |
|---|---|
| Campus head | Each campus has a **Campus Admin**, appointed only by HQ. |
| What a Campus Admin manages | Everything about their own campus: profile, users, own roles, own sessions, own classes and subjects, sections, and every later campus module. |
| Sessions, classes, subjects, roles | **HQ default plus campus's own.** HQ items (no campus) show on every campus, read-only for campus users. Campus items belong to one campus and are managed by its admin or HQ. |
| Who appoints Campus Admins | HQ only. A Campus Admin cannot create another Campus Admin or grant HQ access. |
| Subjects ↔ classes | A class-wise subject list with an optional/compulsory flag, edited by whoever owns the class. |
| Section lifetime | Sections belong to one session. "Copy from last session" re-creates them each year. |
| Current session | One HQ current session. A campus may set one of **its own** sessions as current, which overrides the HQ one for that campus only. |
| First sign-in | Users created with a temporary password must change it on first sign-in. |
| UI | Reuse the existing Clean SaaS components and app shell. |

## 3. Who can do what

| | HQ (SuperAdmin or HQ user) | Campus Admin | Campus staff |
|---|---|---|---|
| Sees | All campuses, or one picked | Own campus only | Own campus, menus their role allows |
| Create or close campuses, change code or status | ✅ | ❌ | ❌ |
| Own campus profile (address, contact, in-charge) | ✅ | ✅ | ❌ |
| Appoint Campus Admin, grant HQ | ✅ | ❌ | ❌ |
| Users of the campus | ✅ | ✅ (campus-level roles only) | by role |
| HQ roles, sessions, classes, subjects | ✅ edit | 👁 use | 👁 |
| Campus's own roles, sessions, classes, subjects | ✅ | ✅ | by role |
| Sections and later campus modules | ✅ any campus | ✅ own campus | by role |

## 4. Part A: Campus Admin and role ownership

### 4.1 Role changes (`Roles` table)
| New column | Type | Notes |
|---|---|---|
| CampusId | int?, FK → Campuses | `null` = HQ role, visible to every campus. A value = role owned by that campus. |
| HqAssignOnly | bit | Only HQ may assign this role. True for `SuperAdmin` and `CampusAdmin`. |

- The role name stays unique **per owner**. A campus role cannot reuse the name of an HQ role, so the same name never means two different things.
- Role reads use a new **ownable filter** (§6): campus users see HQ roles plus their own campus's roles.
- **Writes:**
  - HQ roles: HQ only.
  - Campus roles: HQ, or a user of that campus with `roles` Edit.
  - Nobody edits a role they hold (existing rule).
- **No escalation:** a non-HQ user can only grant a campus role permissions they hold themselves. Granting anything else returns 403 "You can only grant permissions you have."
- **Assigning roles to users:**
  - `HqAssignOnly` roles: HQ only.
  - A campus role can only go to users whose campuses all equal that role's campus.
  - The user's role list in the Users dialog shows only the roles the actor may assign.

### 4.2 New system role `CampusAdmin`
- Seeded with `IsSystem = true`, `HqAssignOnly = true`, `CampusId = null`, description "Runs one campus".
- Seeded permissions:
  - Full (View/Add/Edit/Delete) on `dashboard`, `users`, `roles`, `academic-sessions`, `classes`, `subjects`, `sections`, `my-campus`
  - View and Edit on `campuses`
- **Future modules:** a module seeded with `CampusLevel = true` (new `Modules` column) is granted in full to `CampusAdmin` automatically by the seeder. Students, fees and other later modules set it.
- Default permissions for the other seeded roles, so no new user sees an empty sidebar:
  - `Principal`: same as CampusAdmin, minus `roles` writes
  - `Teacher`: View on `dashboard`, `sections`, `classes`, `subjects`
  - `Clerk`: View on `dashboard`, `sections`; View and Add on `users`
  - `Accountant`: View on `dashboard`
  - `Parent`, `Student`: nothing (portals come later)

  Defaults are only seeded when the role has **no** permissions, so HQ's later edits are never overwritten.

### 4.3 Appointing Campus Admins
- **Campuses page (HQ):** each campus card shows its Campus Admin(s) (name and email) or "No admin yet". "Add campus" gets an optional **Campus admin** step (first name, last name, email, temporary password). It creates the admin user with the `CampusAdmin` role and `MustChangePassword = true`, in the same request.
- **API:** `SaveCampusRequest` gets an optional `Admin: NewCampusAdminRequest?`. `CampusDto` gets `Admins: IReadOnlyList<CampusAdminRefDto(int Id, string FullName, string Email)>`.

### 4.4 My campus (Campus Admin page)
- A new module `my-campus` ("My campus", route `/admin/my-campus`, icon `school`, top-level, order 2) for campus users, showing the **working campus**:
  - **Profile** card: editable name, address, contact and in-charge. Code and status are read-only.
  - **Team** summary: user count per role, with a link to Users.
  - **Academic** summary: current session, class count and section count, with a link to Sections.
- Under the hood it uses `GET/PUT /api/campuses/{activeCampusId}`, which already enforces campus scope and the code/status lock.
- HQ users on "All campuses" see a prompt to pick a campus.

### 4.5 First sign-in password change
- `CurrentUserDto.MustChangePassword` is already returned. When it is true, the UI shows a full-screen **Set a new password** form before anything else.
- New endpoint `POST /api/auth/change-password` takes `{ currentPassword, newPassword }`. The new password must be at least 8 characters and different from the current one. On success it clears the flag. It is available to any signed-in user, and a "Change password" item is added to the user menu.

## 5. Part B: Academic setup

### 5.1 Tables
All of them inherit `AuditableEntity`, except `ClassSubjects`.

**`AcademicSessions`:** Name (nvarchar 50), StartDate, EndDate, Status (`Planned / Active / Closed`), IsCurrent, **CampusId int?** (null = HQ).

**`Classes`** (`SchoolClass`): Code (nvarchar 20, upper-case), Name (100), Stage (`PrePrimary / Primary / Middle / Secondary / HigherSecondary`), DisplayOrder, IsActive, **CampusId int?**.

**`Subjects`:** Code (20, upper-case), Name (100), Type (`Theory / Practical / Activity`), IsActive, **CampusId int?**.

**`ClassSubjects`:** ClassId, SubjectId (composite PK), IsOptional, DisplayOrder.

**`Sections`** (`ICampusScoped`): CampusId, AcademicSessionId, ClassId, Name (20), Shift (`Morning / Afternoon / Evening`), Capacity int?, Room (50)?, IsActive.

### 5.2 Ownership rules (shared by roles, sessions, classes and subjects)
- **Read:** an HQ user on "All campuses" sees everything. Anyone working in a campus sees HQ items plus that campus's items.
- **Write:**
  - HQ items: HQ only, otherwise 403.
  - Campus items: HQ, or a user of that campus with the module's permission.
  - New items made by a campus user are stamped with the working campus.
  - HQ creates HQ items when on "All campuses", or campus items by passing `campusId`.
- **Unique:** codes and names are unique among HQ items plus that campus's items. Two campuses may use the same code for their own items.
- **References:**
  - A class's subjects must be HQ subjects or subjects of the **same** campus.
  - A section's class and session must be HQ-owned or owned by the section's campus.
  - A campus user cannot change the subject list of an HQ class (403). They can create their own class instead.

### 5.3 Session rules
- Dates: end must be after start, and must not overlap another session **of the same owner**.
- At most one current session per owner: one HQ current, and at most one current per campus.
- **Effective current session** for a campus = its own current session if it has one, otherwise the HQ current session.
- A **Closed** session is read-only for sections: no add, edit, delete or copy into it.
- `set-current` on a Closed session is refused (400 "Reopen the session first.").
- A session that is current or has sections cannot be deleted.

### 5.4 Other integrity rules
- A class or subject that is in use (by sections or class-subject rows) cannot be deleted, only deactivated.
- Sections can only be created on active classes.
- Section name is unique per (campus, session, class).

## 6. Shared mechanism: `ICampusOwnable`

- New interface `ICampusOwnable { int? CampusId { get; set; } }`, implemented by `Role`, `AcademicSession`, `SchoolClass` and `Subject`.
- **DbContext query filter:** `CampusId == null || filter disabled || DataCampusIds contains CampusId`.
- **Save guard:**
  - An added row with `CampusId == null` created by a non-HQ user is stamped with `ActiveCampusId`.
  - Writing a row whose `CampusId` is null requires HQ.
  - Writing a row of an inaccessible campus throws `CampusAccessException` (403).
- Reuses the existing `CampusFilterIds` / `CampusFilterDisabled` members, and sits beside the `ICampusScoped` logic.

## 7. Permissions and menu

- **New modules:**
  - `my-campus` (top level)
  - `academics` group: `academic-sessions`, `classes`, `subjects`, `sections`
  - Menu order: Dashboard 1, My campus 2, Campuses 3, Academics 4, Settings 5
- **New column** `Modules.CampusLevel` (bit). True for `dashboard`, `my-campus`, `users`, `roles`, `academic-sessions`, `classes`, `subjects` and `sections`.
- **Menu hiding:**
  - HQ users always see every menu their role allows.
  - `My campus` is hidden for an HQ user on "All campuses".
  - `Campuses` is hidden for non-HQ users. Campus admins use My campus instead.

## 8. API

All responses use `ResponseDto<T>` and kebab-case routes.

**Auth:** `POST /api/auth/change-password`.

**Campuses:** create accepts the optional `admin` block, and responses include `admins`.

**Roles:**
- List items gain `campusId`, `campusCode` and `hqAssignOnly`.
- Create accepts optional `campusId`. It is HQ only, and when omitted the role is HQ-owned, or owned by the working campus for a campus user.

**Academic sessions:**
- `GET /api/academic-sessions`, `POST`, `PUT /{id}`, `DELETE /{id}`, `POST /{id}/set-current`
- Rows include `campusId` and `campusCode` (null means HQ)
- `GET` also returns `effectiveCurrentId` for the working campus

**Classes:** `GET`, `GET /{id}` (with subjects), `POST`, `PUT /{id}`, `DELETE /{id}`, `PUT /{id}/subjects` (`[{ subjectId, isOptional }]`, replaces the list).

**Subjects:** `GET`, `POST`, `PUT /{id}`, `DELETE /{id}`.

**Sections:**
- `GET ?sessionId=&classId=`, where `sessionId` defaults to the effective current session
- `POST`: on "All campuses", HQ must pass `campusId`
- `PUT /{id}` and `DELETE /{id}`
- `POST /copy` with `{ fromSessionId, toSessionId, dryRun }` copies the working campus's active sections and skips existing ones

**Dashboard:** a "Current session" KPI (effective current session name, plus "N sections") linking to Sections.

## 9. UI

Everything reuses `sms-ui.tsx` and `modal.tsx`.

- **Owner badge:** every list of roles, sessions, classes and subjects shows an owner pill: `HQ` (info) or the campus code (primary). HQ rows show a lock icon and are read-only for campus users. Add buttons create campus items for campus users. HQ users on "All campuses" create HQ items, or pick a campus in the dialog.
- **Set new password screen:** shown before the app shell when `mustChangePassword` is true.
- **My campus page** (§4.4).
- **Campuses page (HQ):** admin line on each card, and the optional admin step in the add dialog.
- **Users page:** the role picker lists only assignable roles. `HqAssignOnly` roles carry an "HQ" pill and only appear for HQ.
- **Roles page:**
  - Campus users can add or edit their campus's roles.
  - In the matrix, cells the actor does not hold are disabled with the title "You don't have this permission".
- **Sessions, Classes, Subjects and Sections pages** are as in revision 1, plus owner pills and "Current" per owner. Sections uses the effective current session.
- **Dashboard:** the Current session KPI.

## 10. Seed data (idempotent)

- `CampusAdmin` role and the default role permissions (§4.2), plus `CampusLevel` flags
- HQ session `2026-27` (current, Active, 1 Apr 2026 – 31 Mar 2027)
- HQ classes PG, NUR, PREP (PrePrimary), G1–G5 (Primary), G6–G8 (Middle), G9–G10 (Secondary)
- HQ subjects ENG, URDU, MATH, SCI, SST, ISL, CS, ART (Activity), PHY, CHEM, BIO (Theory), PE (Activity), with stage-wise class-subject lists
- Sections A and B for G1–G5 on each demo campus, in the current session
- No Campus Admin is seeded, so no extra passwords end up in code or logs. HQ creates them from the Campuses page, and the end-to-end tests create temporary ones.

## 11. Errors

| Case | Response |
|---|---|
| Duplicate name or code in scope | 409 |
| Changing an HQ item as a campus user | 403 |
| Assigning an `HqAssignOnly` role, or a foreign campus role | 403 |
| Granting a permission you do not hold | 403 |
| Change in a Closed session | 400 "This session is closed." |
| Section on an inactive class, or a class or session of another campus | 400 |
| Invalid or overlapping dates | 400 |
| Deleting something still in use | 400, saying what uses it |
| Wrong current password on change | 400 |

## 12. Testing

- One EF migration (`CampusAdminAndAcademics`). Running the seed twice must not duplicate anything.
- End-to-end Python script (`tests/e2e/`), with groups:
  - `campus-admin`: HQ creates a campus with an admin. The admin signs in, must change password, sees My campus and only their campus, can create a Teacher user but not a CampusAdmin or HQ user, and can edit the profile but not the code.
  - `roles-ownership`: the campus admin creates a campus role, cannot grant a permission they lack, cannot see another campus's roles, and cannot edit HQ roles.
  - `sessions`, `subjects`, `classes`: HQ items, campus items, uniqueness in scope, read-only HQ items for campus users, deleting items in use, effective current session.
  - `sections`: isolation, cross-campus id guessing, duplicate on update, copy (dry run and repeat), closed lock, foreign class refused.
  - `dashboard`: the KPI.
- UI: `npm run build`, plus a browser walk-through as HQ and as a Campus Admin created from the Campuses page.

## 13. Out of scope (later specs)

Students and promotion, staff and class teachers, timetable, attendance, fees, and exams. Campus overrides of an HQ class's subject list are also out of scope; the campus creates its own class instead.
