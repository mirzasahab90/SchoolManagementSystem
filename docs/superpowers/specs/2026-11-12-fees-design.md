# Fees — Design

Student fees per campus: fee heads, class-wise fee structure, concessions, monthly challans (bank and counter), payment collection, defaulters and reports. Builds on Academic Setup (sessions, classes, sections), Students (students, guardians, enrollments) and Staff (staff CNIC for staff-child concessions).

## 1. Goals

- Main Campus sets fee heads and class-wise amounts per session; a campus may override any amount.
- Every month a campus generates one challan per active student, printed with bank, school and parent copies.
- Fees are paid at the bank or the school counter; the school records each payment and prints a receipt.
- Partial payments are allowed; whatever is left (plus the late fine once the due date has passed) moves to the next month's challan as arrears, so a student always has at most one open challan.
- Concessions: per-student (manual), sibling discount (automatic) and staff child.
- Defaulters list and collection reports; a Fees tab on the student profile.

## 2. Decisions (agreed)

| Topic | Decision |
|---|---|
| Collection | Both: bank challan (3 copies) and school counter. Bank payments are entered by the school. |
| Fee heads | `Monthly` heads on every challan; `OneTime` heads in a chosen month or on the first challan after admission. |
| Ownership | Main Campus default, campus override (like grading). |
| Model | One challan per student per month; unpaid balance carries forward as an `Arrears` line and the old challan becomes `CarriedForward`. |
| Concessions | Manual per student, sibling discount, staff child. They do not stack: per head line, the largest discount applies. |
| Sibling count | Active students of the same campus sharing a guardian, ordered by admission no.; the first pays full. |
| Staff child | A `StaffChild` concession linked to a staff member; the system suggests matches (guardian CNIC = staff CNIC), the admin confirms. |
| Late fee | Fixed fine after the due date, at most once per challan; challan shows "payable by due date" and "payable after due date". |
| Payments | Partial allowed; no advance (cannot pay more than the payable amount). Wrong payments are voided with a reason, never deleted. |

## 3. Data model

All tables inherit `AuditableEntity` (audit + soft delete). Money is `decimal(10,2)`, ≥ 0 unless stated. Enums stored as strings.

### `FeeHead` (`ICampusOwnable`: null = Main Campus)
| Column | Type / rule |
|---|---|
| CampusId | int? |
| Name | nvarchar(60), unique per owner |
| Frequency | `Monthly / OneTime` |
| OneTimeMonth | int? 1–12 (OneTime only; null = on the first challan after admission) |
| SortOrder | int |
| IsActive | bool |

A campus sees Main Campus heads plus its own. Seeded Main Campus heads: Tuition (Monthly), Admission (OneTime, on admission), Annual Fund (OneTime, month 4), Exam (OneTime, inactive by default).

### `FeeStructure` (`ICampusOwnable`)
| Column | Type / rule |
|---|---|
| CampusId | int? |
| AcademicSessionId | int, FK |
| ClassId | int, FK |
| FeeHeadId | int, FK |
| Amount | decimal(10,2) |

Unique (owner, session, class, head). **Resolution** for (campus, session, class, head): campus row → Main Campus row → not charged. A class with no Monthly amount at all is "fee structure not set".

### `FeeSettings` (`ICampusOwnable`, one per owner; campus row wins field by field where set)
| Column | Type / rule |
|---|---|
| CampusId | int? |
| DueDay | int 1–28 (default 10) |
| LateFine | decimal(10,2) (default 300) |
| SiblingSecondPercent | decimal(5,2) (default 0) |
| SiblingThirdPlusPercent | decimal(5,2) (default 0) |
| StaffChildPercent | decimal(5,2) (default 0) |
| ConcessionHeadIds | nvarchar(200), comma list; heads that sibling/staff discounts apply to (default: Tuition) |
| BankName, AccountTitle, AccountNo | nvarchar(100)? |
| ChallanNote | nvarchar(500)? (printed instructions) |

Main Campus row is seeded; campus row fields are nullable and fall back to Main Campus.

### `StudentConcession` (`ICampusScoped`)
| Column | Type / rule |
|---|---|
| CampusId | int |
| StudentId | int, FK |
| AcademicSessionId | int, FK |
| FeeHeadId | int? (null = all Monthly heads) |
| Kind | `Manual / StaffChild` |
| StaffId | int? (required for StaffChild) |
| ValueType | `Percent / Fixed` |
| Value | decimal(10,2); Percent 0–100 |
| Reason | nvarchar(200) |
| IsActive | bool |

At most one active concession per (student, session, head, kind). A StaffChild concession is created with `ValueType = Percent` and `Value = StaffChildPercent` (editable).

### `FeeVoucher` (`ICampusScoped`) — the challan
| Column | Type / rule |
|---|---|
| CampusId | int |
| VoucherNo | nvarchar(20), campus sequence `{CampusCode}-F-{000001}`, unique |
| StudentId, EnrollmentId, AcademicSessionId | int, FK |
| Month | date (first day of the month) |
| IssueDate, DueDate | date |
| Total | decimal(10,2) = Σ line Net |
| LateFineAmount | decimal(10,2) (from settings when issued) |
| LateFineApplied | bool |
| Paid | decimal(10,2) = Σ non-void payments |
| Status | `Unpaid / PartiallyPaid / Paid / CarriedForward / Cancelled` |
| CarriedToVoucherId | int? |
| CancelReason | nvarchar(200)? |

Unique (student, month) among non-cancelled vouchers. **Balance** = Total − Paid.

### `FeeVoucherLine`
| Column | Type / rule |
|---|---|
| FeeVoucherId | int, FK |
| Kind | `Head / Arrears / LateFine / Adjustment` |
| FeeHeadId | int? (Head only) |
| Description | nvarchar(120) (snapshot of head name, "Arrears (Nov 2026)", reason for adjustments) |
| Amount | decimal(10,2) (Adjustment may be negative) |
| Concession | decimal(10,2) (Head only) |
| ConcessionNote | nvarchar(80)? ("Sibling 10%", "Manual: merit") |
| Net | Amount − Concession |
| FromVoucherId | int? (Arrears only) |

Lines are a snapshot: later changes to structure or concessions do not alter issued challans.

### `FeePayment` (`ICampusScoped`)
| Column | Type / rule |
|---|---|
| CampusId | int |
| FeeVoucherId | int, FK |
| ReceiptNo | nvarchar(20), campus sequence `{CampusCode}-R-{000001}` |
| Amount | decimal(10,2) > 0 |
| PaidOn | date (not in the future) |
| Method | `Cash / Bank / Online` |
| Reference | nvarchar(60)? (bank branch / transaction id) |
| ReceivedByUserId | int |
| IsVoid | bool |
| VoidReason, VoidedOn, VoidedByUserId | nvarchar(200)?, datetime2?, int? |

## 4. Rules

**Charges for a student and month** (session = the enrollment's session):
- Every active Monthly head with a resolved amount > 0.
- Every active OneTime head whose `OneTimeMonth` = the month, or with no month when this is the student's first non-cancelled voucher in the session and the student was admitted in that session.

**Concession per head line:** candidates are the active manual concession for the head (or all-monthly), the active StaffChild concession, and the sibling discount (if the head is in `ConcessionHeadIds`). Percent = Amount × v / 100; Fixed = min(v, Amount). The largest wins; ties prefer Manual, then StaffChild, then Sibling. Rounded to whole rupees.

**Sibling position:** active students of the same campus with an active enrollment in the session, sharing any guardian with the student, ordered by admission no. Position 1 pays full; 2 gets `SiblingSecondPercent`; 3+ gets `SiblingThirdPlusPercent`.

**Generate a month** (`POST /api/fees/vouchers/generate` with `{ month, classId?, sectionId?, issueDate, dueDate?, preview }`):
- Month must be inside the campus's current session; due date defaults to `DueDay` of the month.
- Roster: active enrollments in the session (filtered by class/section).
- Skipped with a reason: voucher for the month already exists; fee structure not set for the class.
- For each student: build head lines; if the student has an open voucher (Unpaid / PartiallyPaid) from an earlier month, add an `Arrears` line = its balance + its late fine (when its due date has passed and not yet applied), set that voucher `CarriedForward` and `CarriedToVoucherId`.
- `preview: true` returns rows `{ student, class, lines, total, arrears, skippedReason }` and writes nothing. Otherwise all vouchers are written in one transaction; the response gives counts.
- Running again creates only the missing vouchers.

**Single voucher:** `POST /api/fees/vouchers` `{ studentId, month }` with the same rules (used after a late admission).

**Cancel** (`POST /api/fees/vouchers/{id}/cancel` `{ reason }`): only when it has no non-void payments and is the student's latest voucher. Vouchers it carried forward return to their previous status (Unpaid / PartiallyPaid) and lose `CarriedToVoucherId`.

**Adjustment** (`POST /api/fees/vouchers/{id}/adjustments` `{ amount, reason }`): only on an open voucher; amount may be negative but the voucher total cannot go below the amount already paid.

**Payment** (`POST /api/fees/payments` `{ voucherId, amount, paidOn, method, reference }`):
- Voucher must be Unpaid or PartiallyPaid.
- If `paidOn` > DueDate and the fine is not applied: add the `LateFine` line (LateFineAmount) and set `LateFineApplied`.
- Amount ≤ balance (after the fine) — 400 "Amount is more than the balance of Rs {balance}."
- Status becomes Paid (balance 0) or PartiallyPaid.

**Void** (`POST /api/fees/payments/{id}/void` `{ reason }`): only when the voucher is not CarriedForward or Cancelled; recomputes Paid and Status. A late fine added by that payment stays (admins can adjust it off).

**Defaulter:** a student with an open voucher whose due date has passed. Months outstanding = number of months covered by the open voucher and its carried-forward chain with an unpaid balance. Left students with a balance are included and flagged.

## 5. Permissions and menu

New menu group `fees-menu` ("Fees", icon `wallet`, after Exams) with CampusLevel children:

| Key | Name | Route | Icon |
|---|---|---|---|
| `fee-setup` | Fee setup | `/admin/fees/setup` | `settings-2` |
| `fee-vouchers` | Challans | `/admin/fees/challans` | `receipt` |
| `fee-collection` | Collect fee | `/admin/fees/collect` | `hand-coins` |
| `fee-reports` | Fee reports | `/admin/fees/reports` | `chart-column` |

- fee-setup: View; Edit for heads, structure, settings and concessions.
- fee-vouchers: View, print; Add = generate / single; Edit = cancel / adjustment.
- fee-collection: View; Add = receive payment; Delete = void.
- fee-reports: View defaulters and reports; Export for CSV.

Default grants: CampusAdmin full (CampusLevel). Principal full. Accountant full on all four. Clerk: fee-vouchers View, fee-collection View + Add. Teacher: none. Main Campus heads, structure and settings need Main Campus scope (no active campus) with fee-setup Edit.

## 6. API

- Setup: `GET/POST/PUT /api/fees/heads` · `GET /api/fees/structure?sessionId=` (grid: classes × heads with Main Campus and campus amounts) · `PUT /api/fees/structure` `{ sessionId, rows: [{ classId, feeHeadId, amount | null }] }` (null removes the campus override) · `GET/PUT /api/fees/settings`
- Concessions: `GET /api/fees/concessions?sessionId=&q=` · `POST/PUT/DELETE /api/fees/concessions/{id}` · `GET /api/fees/concessions/staff-child-suggestions`
- Vouchers: `GET /api/fees/vouchers?month=&classId=&sectionId=&status=&q=&page=&pageSize=` · `GET /api/fees/vouchers/{id}` · `POST /api/fees/vouchers/generate` · `POST /api/fees/vouchers` · `POST …/{id}/cancel` · `POST …/{id}/adjustments` · `GET /api/fees/vouchers/print?ids=|month=&sectionId=` (JSON for the print page)
- Payments: `GET /api/fees/collect/lookup?q=` (student / admission no. / voucher no. → open voucher) · `POST /api/fees/payments` · `POST /api/fees/payments/{id}/void` · `GET /api/fees/payments/{id}/receipt`
- Reports: `GET /api/fees/defaulters?classId=&sectionId=&minMonths=&minAmount=&page=&pageSize=` (+ `/csv`) · `GET /api/fees/reports/collection?from=&to=` · `GET /api/fees/reports/monthly?month=` · `GET /api/fees/reports/heads?month=` (each + `/csv`)
- Student: `GET /api/students/{id}/fees` (vouchers and payments, newest first, with current balance)
- Dashboard KPI `fees`: "Collected this month" value, hint `"Rs {outstanding} outstanding"`.

All campus-scoped endpoints act on the active campus; Main Campus (all campuses) gets read-only report totals per campus.

## 7. UI

- **Fee setup:** tabs Heads (list, add/edit, frequency, month), Structure (session picker; grid with classes as rows and heads as columns; campus override cells marked and resettable), Settings (due day, fine, sibling and staff percentages, concession heads, bank details, note).
- **Concessions:** list with search and session filter; add/edit dialog (student search, head, % or fixed, reason); "Staff child suggestions" panel with Confirm.
- **Challans:** "Generate month" (month, class/section, dates → preview table with skipped rows → Generate); list with URL filters (month, class, section, status, search), pagination; detail drawer (lines, payments, Cancel, Add adjustment, Print).
- **Print challan:** `/admin/fees/challans/print?…` A4 landscape page with three copies (Bank / School / Parent): campus header, bank details, voucher no., student, class, roll, month, lines, total, payable by due date and after due date, note. One page per student.
- **Collect fee:** search box (focus on load); shows student, open voucher lines, balance, due date and the fine if late; amount (pre-filled), date, method, reference, Receive → receipt (print, A5). Recent receipts today with Void (needs Delete).
- **Defaulters:** filters, table (student, class, guardian phone, months, balance, Left flag), print and CSV.
- **Reports:** Daily collection, Monthly summary, Head-wise; each with filters, totals row and CSV.
- **Student profile:** Fees tab (balance, vouchers with status, payments).
- **Dashboard:** Fees KPI.

## 8. Errors

| Case | Response |
|---|---|
| Negative amount / percent > 100 / bad due day | 400 |
| Duplicate head name or structure row | 400 |
| Generate for a month outside the current session | 400 |
| Payment above balance | 400 "Amount is more than the balance of Rs {balance}." |
| Payment on a Paid / CarriedForward / Cancelled voucher | 400 |
| Cancel a voucher with payments, or not the latest | 400 |
| Void on a carried-forward / cancelled voucher | 400 |
| Adjustment taking total below paid | 400 |
| Campus editing Main Campus heads or amounts | 403 |
| Another campus's voucher / payment / concession / student | 404 |

## 9. Testing

- One migration `Fees`; seeding twice changes nothing (heads, settings, menu, grants).
- E2E `tests/e2e/fees_e2e.py` groups: `schema`, `setup` (heads, structure resolution Main Campus vs campus override, settings fallback), `concessions` (manual %, fixed, largest wins, sibling positions, staff-child suggestion and confirm), `generate` (preview writes nothing, one-time heads by month and on admission, skipped reasons, rerun idempotent, arrears and carry-forward with fine), `vouchers` (single, cancel restores previous, adjustment limits), `payments` (full, partial, late fine once, over-balance rejected, void restores status), `defaulters`, `reports` (totals match vouchers and payments), `fees-isolation`, `fees-permissions`, `dashboard-fees`, `student-fees`.
- Regression: all existing suites.
- UI: build; browser: set structure, generate a month, print challans, collect a partial payment, see the arrears next month, defaulters list; mobile and dark mode.

## 10. Out of scope

Refunds, advance payments and credit balances, online payment gateways, SMS/email reminders, transport routes and hostel fees, installment plans, bank file import, accounting/general ledger integration, fee changes mid-month (pro-rating).
