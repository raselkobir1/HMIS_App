# Implementation Status

Statuses: Not Started · Analysis Completed · Design Completed · Backend Completed · Frontend Completed · Testing Completed (manual, see TESTING.md) · Approved

| Module / Step | Status | Notes |
|---|---|---|
| Phase 0 — Analysis & Architecture | Approved | Approved 2026-10-31 |
| Foundation (Stage 1) | Approved | 401/403/validation envelope verified during the Identity step |
| Identity | Approved | |
| Audit | Approved | |
| Administration | Approved | |
| HR (basic) | Approved | |
| Patient | Approved | Backend + frontend; 56 API + 18 browser checks |
| Doctor profiles + Appointment & Queue (Phase 2, step 1) | Approved | |
| Clinical + OPD consultation (Phase 2, step 2) | Approved | |
| Billing & Payments (Phase 3) | Approved | |
| IPD step 1: beds, admission, transfer, bed charges, discharge (Phase 4) | Approved | Backend + frontend; 55 API + 23 browser checks; Billing 85/85 regression |
| IPD step 2: progress notes, medication orders, MAR, vitals, intake/output, nursing notes, handover, roster (Phase 4) | Approved | Clinical additions + new Nursing module, backend + frontend; 106 API + 28 browser checks; IPD 55/55 and Clinical 45/45 regression |
| Laboratory, Radiology | Not Started | |
| Pharmacy, Inventory, Procurement | Not Started | |
| HR (full), Asset | Not Started | |
| Emergency, OT, Blood Bank, Ambulance | Not Started | |
| Finance, Reporting | Not Started | |

## Manual verification log

### Foundation — 2026-10-31
| Check | Result |
|---|---|
| `dotnet build Hmis.sln` | Pass, 0 warnings, 0 errors |
| `ng build` (production) | Pass, no warnings |
| PostgreSQL container (`localhost:5434`) | Running |
| `GET /health` | 200 `Healthy` (DB reachable) |
| `GET /api/v1/system/info` | 200, standard `ApiResponse` envelope with `traceId` |
| Unknown route | 404 |
| Swagger `/swagger/v1/swagger.json` | Generated, Bearer security scheme present |
| Serilog | Console + rolling JSON file, request logging with TraceId |
| CORS | `http://localhost:4300` allowed; other origins rejected |
| Angular `/login` on `localhost:4300` | 200; login page, admin layout and dashboard compile and lazy-load |
| Not yet verifiable | 401/403 envelope, validation 400, 409/422 mapping — no protected endpoints exist until the Identity module |

### Identity — 2026-10-31
| Check | Result |
|---|---|
| Build (`dotnet build`, `ng build`) | Pass, 0 warnings |
| Migration `InitialIdentity` on an empty DB | Pass (`auth` schema, 8 tables + history) |
| Startup seeding | 12 system roles, 5 permissions with default-role grants, Super Admin from user-secrets |
| API script (40 checks) | **40/40 pass**: 400 validation with field errors; 401 missing token/bad credentials; 403 missing permission; 404; 409 duplicates and stale rowVersion; 422 business rules; refresh rotation + reuse detection; forced password change; lockout after 5 failures + unlock; direct grant and deactivation take effect immediately; logout revokes session; login history |
| Browser walkthrough, Edge headless (20 checks) | **20/20 pass**: deep-link redirect, login errors, forced password change, session survives reload, create user (client + server validation), search, roles, role permission editor, login history, no page-level horizontal scroll at 390 px, logout, nurse sees only permitted menu, guard blocks direct URL, no console errors |
| Issue found and fixed | `GET /users/{id}` returned 500: EF could not translate ordering a projected DTO; now orders before projecting |

### Audit — 2026-10-31
| Check | Result |
|---|---|
| Build (`dotnet build`, `ng build`) | Pass, 0 warnings |
| Migration `InitialAudit` | Pass; Identity model unchanged (no Identity migration needed) |
| Append-only trigger | UPDATE, DELETE and TRUNCATE rejected by the database |
| API script (22 checks) | **22/22 pass**: seeding audited with System as actor; Create/Update/Delete capture; only changed fields stored; secrets and login counters excluded; actor, IP and the request trace id recorded; RolesChanged/PermissionsChanged/PasswordReset/PasswordChanged/Unlocked events; failed 409 writes no audit row; filters, filter options, 404, 400, 403 for Hospital Administrator, 401 anonymous |
| Rollback test | A simulated audit-insert failure returned 500 and the user change was **not** persisted |
| Browser walkthrough (8 checks) | **8/8 pass**: list, action filter, detail dialog, changed-field highlight, mobile layout, menu hidden and direct URL blocked for non-auditors, no console errors |

### Administration — 2026-10-31
| Check | Result |
|---|---|
| Build (`dotnet build`, `ng build`) | Pass, 0 warnings |
| Migration `InitialAdministration` | Pass (`admin` schema, 5 tables) |
| Startup seeding | Profile, settings (Asia/Dhaka, BDT, July FY), 14 departments, 12 designations, 3 number sequences (PATIENT_MRN `MR26000001`, INVOICE, APPOINTMENT) |
| API script (42 checks) | **42/42 pass**: anonymous branding/logo; profile update, 409 and 400 validation; logo upload, content-type sniffing (disguised file rejected), 1 MB limit, replace, remove; settings; department uniqueness (code and case-insensitive name), two-level hierarchy, activation rules, lookup, filters, 409; designations; sequence rules (yearly reset needs year, pad length, forward-only counter, 404); **50 concurrent allocations gave 50 unique consecutive numbers**; role permissions (Receptionist, HR Manager, Hospital Administrator); audit records for department, profile, logo and counter changes |
| Browser walkthrough (Edge headless) | **20 checks across two runs, all behaviours pass**: branding on login and sidebar; profile save; logo upload appears immediately; create/duplicate/deactivate department; create designation; settings save; live number-format preview; format save; mobile layout; Receptionist sees read-only profile, no create buttons, no Settings; Settings URL blocked. One check failed only on timing and was confirmed visually (table showed the new `PT…` number). |
| Not yet exercised end-to-end | `INumberSequenceService.NextAsync` from C#. Its SQL was verified under concurrency; the Patient module will be its first caller. Department deactivation does not yet check employees (HR module not built). |

### HR (basic) — 2026-10-31
Tested in an **isolated environment** (database `hmis_test`, API on 7101, UI on 4301), because the developer's own API session was running against the main `hmis` database.

| Check | Result |
|---|---|
| Build (`dotnet build`, `ng build`) | Pass, 0 warnings |
| Migrations `InitialHr`, Identity `UniqueEmployeeLink` | Applied to `hmis` and `hmis_test` |
| API script (30 checks) | **30/30 pass**: generated EMP codes; **20 concurrent registrations gave unique codes** (first real call of `INumberSequenceService` from C#); names resolved via contracts; validation and business rules (DOB/joining/exit dates, inactive or unknown department); search, filters, paging, sorting; 409 concurrency; status rules; lookup returns current employees only; login linking (one login per employee, current employees only); role permissions; audit records |
| Department guard | Deactivating a department with current employees is blocked with the HR reason |
| Browser walkthrough (14 checks) | 12 passed. The 2 remaining failed only because the test read the table before it reloaded; the database confirmed the edit and the Resigned status. Dates entered in Asia/Dhaka are stored exactly (no day shift). |
| Regression: Identity / Audit / Administration scripts | 38/40, 22/22, 40/42. All 5 failures were outdated expectations (more modules now add permissions and sequences) or a test query against the wrong database; no product regressions |
| Issue found and fixed | 400 responses for unreadable request bodies exposed CLR type names and JSON positions, and added a redundant "request field is required" error. Now sanitized for all modules. |

### Patient — 2026-10-31
Tested in the isolated environment (`hmis_test`, API 7101, UI 4301).

| Check | Result |
|---|---|
| Build (`dotnet build`, `ng build`) | Pass, 0 warnings (`jsbarcode` allowed as CommonJS) |
| Migration `InitialPatient` | Applied (`patient` schema, 5 tables, `pg_trgm` extension, trigram index) |
| API script (56 checks) | **56/56 pass**: MR numbers; **25 concurrent registrations, unique MR numbers**; validation (incl. district list, post code, ID pairing); NID normalization and duplicate block; duplicate warnings (same mobile + similar name, similar name + same DOB/gender) and exclusion of the record being edited and of merged records; search by MR, mobile digits, name, ID; update + 409; photo (content-sniffed, no-store); clinical permissions need explicit grant for Super Admin, every medical-info view audited; allergies/history lifecycle (entered-in-error locks); documents (content-sniffed, 10 MB limit, safe filenames, soft delete keeps row and file); inactive and merged records read-only; merge request/approve/reject rules; permissions for Receptionist, Doctor, HR Manager; full address change in audit |
| Browser walkthrough (18 checks) | **18/18 pass**: Receptionist registers (age only → estimated DOB), sees live duplicate warning, NID block disables save, searches, uploads a document, prints the ID card (barcode), reports a duplicate; Doctor records an allergy and the banner appears; Super Admin approves the merge and the duplicate shows a read-only banner; mobile layout without page-level horizontal scroll |
| Regression (Identity / Audit / Administration) | Unchanged: 38/40, 22/22, 40/42; only the known outdated expectations fail |
| Issues found and fixed | Duplicate check returned 500 when date of birth was omitted (SQL NULL in boolean flags → COALESCE); approving an already-approved merge reported the wrong reason (check order); menu highlighted two items on `/patients/new` (exact-match option) |

### Doctor profiles + Appointment & Queue — 2026-10-31
Isolated environment (database `hmis_test`, API 7101, UI 4301); the developer's `hmis` database was not used for testing.

| Check | Result |
|---|---|
| `dotnet build` / `ng build` | Pass, 0 warnings |
| API script (doctor profiles, sessions, overlap, leave, availability, booking, walk-ins, capacity, duplicates, reschedule, check-in, no-show, cancel, doctor queue, call next, other doctor blocked, queue board without names) | 44/44 |
| Concurrency: 8 parallel bookings for a session with 2 places left | Exactly 2 succeed, serials unique |
| Browser: admin menu, doctors list, new session, invalid times, add/duplicate/remove leave | Pass |
| Browser: reception walk-in (auto check-in), booking, check-in from list, duplicate booking banner, cancel with required reason, no My Queue menu | Pass |
| Browser: doctor queue — call next, complete; queue display (serials only, no sidebar); patient profile Appointments tab; booking from profile pre-selects patient | Pass |
| Fixes found during testing | Clear message for duplicate leave (`LEAVE_ALREADY_RECORDED`, was a generic unique-value error); doctor dropdown on Doctor Sessions exceeded max page size; logout now waits for the server to revoke the refresh token before navigating (an immediate reload could previously keep the session) |

### Clinical + OPD consultation — 2026-10-31
Isolated environment (database `hmis_test`, API 7101, UI 4301); the developer's `hmis` database was only migrated.

| Check | Result |
|---|---|
| `dotnet build` / `ng build` | Pass, 0 warnings |
| API script: access (super admin / receptionist / other doctor 403), vitals ranges, BMI, entered-in-error, workspace content, open consultation (idempotent, one at a time), draft save with line sync, stale row version 409, two primaries, missing duration, follow-up date, unknown line, finalize rules, finalize completes visit and is repeatable, locked after finalize, addenda, print data, history, relationship gained by booking, audited views and line changes, branding contact fields | 45/45 |
| Regression: Appointment API script on a fresh database | 44/44 |
| Browser: nurse vitals (validation, BMI preview), doctor queue → consultation, editor (diagnosis, medicine, test, Bangla advice), unsaved indicator and leave guard, draft persists after reload, finalize → read-only, addendum, prescription print (BMDC, Rx, vitals, Bangla), queue shows completed, patient Visits tab; receptionist has no clinical menu or tab | Pass |
| Fixes found during testing | New prescription/diagnosis lines were treated as existing rows (false concurrency conflict) — child keys now `ValueGeneratedNever`; dropped a filtered unique "one primary" index that would also have broken swapping primary/secondary in one save (rule kept in the domain); nested `<main>` landmark on the consultation page; primary-diagnosis toggle shared its label with the text field and was not visibly selected |

### Billing & Payments — 2026-10-31
Isolated environment (database `hmis_test`, API 7101, UI 4301); the developer's `hmis` database was only migrated.

| Check | Result |
|---|---|
| `dotnet build` / `ng build` | Pass, 0 warnings |
| API script: access by role; price list (codes, validation, deactivation, stale row version); OPD fee posted at check-in (once), walk-in billed, unpaid bill cancelled with the appointment, paid bill kept; draft invoices (inactive service, fixed vs editable price, line sync, stale row version, finalize, locked after finalize); payments (partial, over-payment, reference rule, two decimals, idempotent retry, reused request id); discounts (within limit, pending, one at a time, approval rights, above due, decided twice); two cashiers paying the same bill at once (one wins); advance deposit, apply, insufficient advance, wrong patient; advance return and cancellation refund (approval, payout, rejection, pending blocks payments, invoice cancelled, advance released); cancel permissions; account, receipt, lists; collection report (own vs all, totals by method and cashier, 31-day limit); audit rows; database refuses deleting/editing financial rows | 85/85 |
| Regression on fresh databases: Appointment / Clinical API scripts | 44/44, 45/45 |
| Browser: cashier menu, new invoice (patient picker, services, quantity, editable price, save & finalize), advance and bKash payments, receipt print, discount needing approval, patient account, collections; administrator approves discount and refund, adds a service; cancel & refund, payout, invoice cancelled and advance released, invoice print; phone width; receptionist has no billing access | 26/26 |
| Fixes found during testing | Cashier role could not search patients (Patient.View added to its defaults); reason/note dialog did a native form submit that reloaded the page (approvals did nothing); cancelling an already-cancelled invoice reported a permission error instead of "already cancelled"; receipt wording for payments from advance; appointment list offered a page size above the API maximum |

### IPD step 1 — 2026-10-31
Isolated environment (database `hmis_test`, API 7101, UI 4301); the developer's `hmis` database was only migrated.

| Check | Result |
|---|---|
| `dotnet build` / `ng build` | Pass, 0 warnings |
| API script: setup permissions and validation (categories, wards, beds, duplicates, inactive category); bed board counts; admission (number, bed occupied, admission day on a running bill, suggested deposit); already admitted; occupied/maintenance bed refused; two admissions into one bed at once (one wins); housekeeping rules (occupied bed not changeable, not deactivatable, ward with patients not closable); transfer (stale row version, history, old bed needs cleaning); doctor change; noon-rule bed days over 3+ days and re-posting adds nothing; running bill (cashier cannot finalize, posted lines locked, services can be added, not counted as outstanding); deposit shown as advance; discharge order rights (receptionist, super admin, doctor), follow-up date, clinical summary hidden from non-clinical users, printable summary for doctors, cancel order; final bill (finalized, discharge time fixed, not twice); release blocked with due, cashier cannot override, settled release frees the bed; administrator discharge with due keeps the due on the account; lists; audit | 55/55 |
| Regression: Billing API script on a fresh database | 85/85 |
| Browser: administrator adds a bed; bed board (rates, occupant); admit from the board (bed preselected, deposit, phone validation); transfer; doctor orders discharge and prints the summary (Bangla, BMDC); receptionist cannot see the summary, prepares the final bill, cannot discharge with due; cashier settles; receptionist discharges; nurse marks the cabin cleaned; running bill read-only lines at the cash counter; Admit on the patient profile; phone width | 23/23 |
| Fixes found during testing | Bed-charge job disposed its service scope synchronously (error after every run; now async); discharge summary showed the order time as the discharge time before the final bill |

### IPD step 2 — 2026-11-01
Isolated environment (database `hmis_test`, API 7101, UI 4301); the developer's `hmis` database was only migrated.

| Check | Result |
|---|---|
| `dotnet build` / `ng build` | Pass, 0 warnings |
| API script: chart access (receptionist, administrator refused; attending and duty doctor, nurse allowed); progress notes (empty, nurse refused, signed by attending and duty doctor, only the author marks in error); medication orders (nurse refused, unknown frequency, PRN without indication, start too far back, duration, STAT ignores duration); ward vitals; roster (nurse refused, in charge, duplicate, second in charge, resigned employee, past date, range limit, remove); current shift and "my ward"; ward view (nurses on duty, doses due, active orders); MAR (doctor cannot record, STAT given, duplicate, not a scheduled time, too early, missing time, refused needs reason and records no dose, PRN with/without time, minute-precision PRN time, another patient's order, time two days back, other nurse cannot correct, author corrects, two nurses record one dose at once — one wins); stop order (stale version, nurse refused, stopped twice, cancelled STAT not givable); standard times (OD, TDS, QID, Q12H) and overdue doses via time travel; late documentation; intake/output (wrong category, zero, receptionist refused, correction by author only, balance and shift totals); nursing notes (Bangla kept, doctor reads, receptionist refused, author-only correction); handover (empty, old date, duplicate patient, unknown admission, revise, other nurse cannot rewrite, author cannot receive, receive, read-only after receipt); after discharge (no notes or nursing records; non-treating doctor refused; attending doctor still reads; IPD relationship opens OPD history; patient leaves the ward view); audit | 106/106 |
| Regression on a fresh database: IPD step 1 / Clinical (OPD) API scripts | 55/55, 45/45 |
| Browser: nurse ward view (my ward, nurses on duty, overdue/due), allergy banner, nurse lands on MAR; doctor lands on orders, allergy warning, STAT order, progress note; nurse gives the dose, PRN refusal needs a reason, ward vitals with fever highlight, fluid balance, Bangla nursing note; handover written, not receivable by its author, received by the incoming nurse, received handover read-only; administrator roster (grid, assign); receptionist has no chart link and is redirected; admission page links to the chart; phone width | 28/28 |
| Fixes found during testing | A PRN dose timed in the minute the order was signed was refused (minute-precision times; now 5 minutes' tolerance and the dialogs send "now" unless the time is changed); a refused/omitted dose stored the ordered dose as given; the IPD test expected a pre-noon day count that the real admission had already posted (test fix) |
