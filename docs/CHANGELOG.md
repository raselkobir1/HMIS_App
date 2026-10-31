# Changelog

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
