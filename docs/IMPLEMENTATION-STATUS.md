# Implementation Status

Statuses: Not Started · Analysis Completed · Design Completed · Backend Completed · Frontend Completed · Testing Completed (manual, see TESTING.md) · Approved

| Module / Step | Status | Notes |
|---|---|---|
| Phase 0 — Analysis & Architecture | Approved | Approved 2026-10-31 |
| Foundation (Stage 1) | Approved | 401/403/validation envelope verified during the Identity step |
| Identity | Approved | |
| Audit | Testing Completed | Backend + frontend; 22 API + 1 rollback + 8 browser checks passed; awaiting approval |
| Administration | Analysis Completed | SRS §3 |
| HR (basic) | Analysis Completed | SRS §4 |
| Patient | Analysis Completed | SRS §5; Q-PAT-1..12 blocking |
| Appointment, Clinical, OPD | Not Started | |
| Billing | Not Started | |
| IPD, Bed, Nursing | Not Started | |
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
