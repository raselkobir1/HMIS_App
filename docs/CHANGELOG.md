# Changelog

## 2026-11-06 — Business-rule review and fixes (docs/REVIEW-2026-11-06.md)
- Billing: a bill with only zero-priced lines can be finalized (discharge no longer blocked); charges arriving after the final bill open a supplementary running bill (`SourcePart`) that the cashier finalizes; cancelling a paid or part-paid appointment notifies refund-capable users; cancelled invoices show Cancelled / Refunded instead of Paid; nobody approves their own refund and the approver cannot pay it out; invoice lines protected by a database trigger like the other financial tables.
- Appointments and OPD: in-consultation appointments left from a previous day close automatically when the doctor next opens the queue; "Close without consultation" for charting doctors; a database guard keeps one patient in consultation per doctor; booking lock per doctor-day; rescheduling within a full session allowed; doctor session overlap serialized; queue display shows the completed count; vitals can be voided only by their author while the visit is open.
- IPD and Nursing: bed days stop at the doctor's discharge order (noon rule), the ward-leaving time is kept separately; administrators can reopen a stay after the final bill (`Ipd.Discharge.Reopen`); after discharge, nurses lose chart and nursing-record access and doctors need a treating relationship; roster rejects the same shift in two wards and a third shift in a day; only Available beds in active wards can be admitted to or transferred into; handover items must be active patients of that ward; bed-charge job isolates each stay; charge sync needs the discharge permission; handover items audited.
- Identity, Administration, Patient, HR: mobile numbers stored as digits (search and duplicate detection work regardless of formatting; data migrated); resigning or terminating an employee deactivates the linked login and revokes its sessions; patient documents need an explicit grant for the Super Administrator and downloads are audited; registration form validates the ID type/number pair; Super Administrator accounts protected on unlock and direct-permission edits; role creation with permissions audited; status toggles carry the row version; clearer next-number message.
- Platform: sign-in cookie Secure flag follows the request scheme (TLS at a reverse proxy documented for production); refresh and logout rate-limited; API port bound to loopback with forwarded headers trusted only when configured; missing number sequence returns 422; API health check and capped container logs in compose; startup error when no Super Administrator can sign in; `/system/info` no longer reveals the environment.
- UI "today" uses Asia/Dhaka everywhere.

## 2026-11-06 — Demo data seed and OPD guide
- `DemoData:Enabled` (off by default; on in Development and via `HMIS_SEED_DEMO_DATA=true` for Docker) seeds one demo employee per role, a doctor profile for the demo doctor, and the logins `admin1`, `reception1`, `cashier1`, `nurse1`, `doctor1` with the configured demo password. Idempotent; never for production.
- Department and designation lookups expose the active lists to other modules.
- `docs/OPD-END-TO-END-GUIDE.md`: hands-on walkthrough of setup, registration, booking, check-in, payment, consultation and reports.

## 2026-11-01 — Gap closure after reviewing Phases 1–4
- Patient merge is blocked while the duplicate has upcoming appointments, a stay in progress, dues, a draft bill, a deposit or an unpaid refund; the surviving record shows the merged records' history.
- A death discharge records the patient as deceased (no new appointments or admissions; correctable).
- Registration fee (price-list service REG), admission charge (ADM) and doctors' inpatient visit fees are billed automatically.
- Billing settings: discount approval limit and payment methods in use.
- Dashboard with today's figures; admissions tab on the patient profile.
- In-app notifications with a bell, and settings to switch each type off.

## 2026-11-01 — IPD step 2: inpatient care and nursing (Phase 4)
- Doctors: signed progress notes (SOAP) and inpatient medication orders with standard ward times; stop and re-order to change.
- Ward vitals on the inpatient chart.
- New Nursing module (`nursing` schema): ward view with doses due/overdue, Medication Administration Record (given, omitted, refused, held), intake/output with shift and 24-hour balance, nursing notes, shift handover with receipt, and a nurse roster by ward and shift.
- Allergy banner on the chart, the order dialog and every dose.
- Corrections only by the author, marked "entered in error"; chart views are audited.

## 2026-10-31 — IPD step 1: beds, admission, discharge (Phase 4)
- New IPD module (`ipd` schema): bed categories with daily rent and suggested deposit, wards, beds, bed board with housekeeping statuses.
- Admission with bed allocation (no double booking), bed transfer with history, attending doctor change.
- Bed rent by the noon rule posted daily to a running IPD bill; deposits are taken as advance.
- Doctor's discharge order with discharge summary (A4 print, Bangla advice); final bill fixes the discharge time.
- Release requires a settled bill, or an administrator's "discharge with due" with a reason.
- Billing: running bills with read-only posted charges; the cashier can add services to them.

## 2026-10-31 — Billing & Payments (Phase 3)
- New Billing module (`billing` schema): price list, invoices, discounts, payments, advance deposits and refunds.
- OPD consultation fee billed automatically at check-in; cancelling a checked-in appointment cancels its unpaid bill.
- Cash counter: invoices from the price list, partial payments (cash, card, bKash, Nagad, Rocket, bank transfer), advance applied to bills, A5 receipts and invoices.
- Discounts above 10% and all refunds need an administrator's approval; cancellation refunds cancel the invoice.
- Patient account page, approvals queue and collection report by cashier and method.
- Financial safety: per-patient lock for money operations, idempotent payments, database triggers against deleting financial records.
- Cashier role can now search patients (Patient.View default).

## 2026-10-31 — Clinical + OPD consultation (Phase 2, step 2)
- New Clinical module (`clinical` schema): consultations with diagnoses, prescription lines, investigation requests and addenda; vital signs.
- Nurse station (OPD Vitals) with plausibility checks and BMI; mistakes marked as entered in error, never deleted.
- Consultation screen from My Queue: allergy banner, vitals, history and previous visits beside the editor; draft save, leave-page warning, finalize & complete.
- Finalized records are locked; corrections are addenda. Printable A4 prescription with letterhead, BMDC number and Bangla advice.
- Patient profile Visits tab. Clinical access is explicit-grant, relationship-checked for doctors, and every view is audited.
- Appointment exposes `IOpdEncounterService`; branding now includes hospital address, phone and email.

## 2026-10-31 — Doctor profiles, appointments and queues (Phase 2, step 1)
- HR: doctor profiles (specialization, qualifications, BMDC number, new / follow-up fees, accepting appointments) and a Doctors screen.
- `appointment` schema: weekly doctor sessions with capacity and minutes per patient, leave (whole day or one session), appointments with serial numbers and estimated times.
- Booking screen with patient search, department/doctor filter, live session availability and fee; walk-ins for today are checked in at once.
- Appointments list with confirm, check-in, reschedule, no-show and cancel (reason required); patient profile Appointments tab and "Book appointment".
- Doctor's My Queue (call next, see now, complete, auto-refresh) and a full-screen Queue Display showing serial numbers only.
- Serial numbers and capacity are safe under concurrent booking (per-session advisory lock).
- Fixes: clear duplicate-leave message; logout waits for the refresh token to be revoked before leaving the page.

## 2026-10-31 — Patient module (reference module)
- `patient` schema: Patients, Allergies, MedicalHistories, Documents, MergeRequests; trigram name index.
- Registration with generated MR numbers, date of birth or age, 64-district address list, emergency contacts, ID documents.
- Live duplicate check (warnings) and exact ID-number block; single-box search.
- Profile with allergy banner, photo, Overview / Medical info / Documents tabs; printable ID card with barcode.
- Allergies and medical history (explicit-grant clinical permissions, audited views, entered-in-error instead of delete).
- Documents (PDF/JPEG/PNG, content-checked, soft delete); duplicate merge requests with approval.
- Shared `FileSignatures` helper (also used for the hospital logo); menu items support exact-match highlighting.

## 2026-10-31 — HR module (basic employees)
- `hr` schema: Employees with generated codes (`EMP000001`), employment type and status (with exit date).
- Employees screen: search, filters, register/edit dialog with date pickers, status dialog.
- Login accounts can be linked to a current employee from the user form (employee picker); one login per employee.
- Administration blocks deactivating a department with current employees through the new `IDepartmentUsageGuard` extension point (no reverse module dependency).
- Batch department/designation lookups for name resolution without cross-schema joins.
- Fix: validation errors for unreadable request bodies no longer leak internal type names or JSON positions.
- `scripts/migrate.ps1` builds once and then applies each module migration without rebuilding.

## 2026-10-31 — Administration module
- `admin` schema: HospitalProfile (with logo), SystemSettings, Departments (two levels), Designations, NumberSequences.
- Public branding endpoint; hospital name and logo shown on the login page and in the app shell.
- Atomic, forward-only number sequences with yearly reset and live format preview; PATIENT_MRN, INVOICE and APPOINTMENT seeded.
- Contracts for other modules: department/designation lookups, hospital settings, number sequence service.
- Shared `IFileStorageService` (local storage) and shared list/dialog styles moved to `shared/styles`.
- Seeded 14 standard departments and 12 designations on first run; July–June financial year.

## 2026-10-31 — Audit module
- `audit` schema with append-only `AuditLogs` (jsonb old/new values) and a database trigger that blocks UPDATE/DELETE/TRUNCATE.
- `AuditSaveChangesInterceptor` on every module DbContext: captures `[Audited]` entity changes and queued business events in the same transaction as the change.
- `IAuditService` (`Record`, `RecordImmediateAsync`); Identity now records RolesChanged, PermissionsChanged, PasswordReset, PasswordChanged and Unlocked. Login counters are `[NotAudited]`.
- Read-only Audit Log API and screen (filters, before/after detail), `Audit.View` for Super Administrator only.

## 2026-10-31 — Identity module
- Backend: `auth` schema; login (username or email), refresh-token rotation with reuse detection, logout, change password, `me`; users, roles, direct permissions, permission catalogue, login history; lockout; forced password change; per-request permission evaluation; Super Administrator protections; startup seeding.
- Frontend: same-origin dev proxy, session restore on reload, silent refresh-and-retry on 401, 30-minute idle logout, change-password page, Security area (Users, Roles & Permissions, Role permission editor, Login History).
- Shared: `IStartupTask`, `IConcurrencyRepository`, `JwtSettings`, `SystemRoles`, permission default roles and `RequiresExplicitGrant`.
- Decision D9 amended (own entities + Identity PBKDF2 hasher instead of the full UserManager stack). Auth rate limit is configurable (`RateLimiting:AuthPermitPerMinute`).

## 2026-10-31 — Stage 1 Foundation
- Solution `Hmis.sln` (.NET 10) with central package management and shared build props.
- BuildingBlocks: Domain (base/auditable entities, markers), Application (ApiResponse, pagination, typed exceptions, IRepository, IUnitOfWork, ICurrentUser, IRequestContext, IDateTimeProvider, IPermissionChecker), Infrastructure (ModuleDbContext with per-schema migrations history and xmin concurrency, BaseRepository, UnitOfWork, auditable-stamping interceptor), Presentation (ApiControllerBase, HasPermission + dynamic permission policies), Contracts (empty, for cross-module contracts).
- API host: Serilog, global exception handler, FluentValidation action filter, JWT bearer auth with ApiResponse 401/403 bodies, permission authorization (deny-all until Identity), CORS, auth rate-limit policy, URL-segment API versioning, Swagger, `/health` with PostgreSQL check.
- Docker Compose PostgreSQL 17 on port 5434 with pg_trgm/citext; optional pgAdmin profile.
- Angular 22 + Material shell: login page, authentication and admin layouts, dashboard, auth/error interceptors, auth/guest/permission guards, `*hasPermission` directive, page header and confirm dialog.

## 2026-10-31 — Phase 0
- Added BRD and Technical Architecture source documents to `/docs`.
- Recorded decisions: .NET 10 LTS, 4 projects per module, single facility, Angular Material, Docker PostgreSQL, no test projects.
- Added the architecture proposal, module dependencies, initial ERD (auth/audit/admin/hr/patient), role-permission matrix, navigation, SRS for Phase 0–1 modules, workflows, API/coding/security/testing standards, roadmap, open questions.
