# Changelog

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
