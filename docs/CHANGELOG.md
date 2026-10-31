# Changelog

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
