# API Design Standards

Implements TAD §6.3, §6.4 and §12. Endpoints for each module are added to this file when the module is built.

## Conventions
- Base path: `/api/v1/{resource}`. Resources are plural and kebab-case, for example `/api/v1/number-sequences`.
- Versioning: URL segment `v1` (`Asp.Versioning.Mvc`).
- JSON: camelCase. Enums are strings. Dates use ISO-8601. `DateOnly` values are sent as `yyyy-MM-dd`.
- Every endpoint accepts a `CancellationToken` and declares `[HasPermission(...)]`, unless it is `[AllowAnonymous]` (login and refresh only).
- Lists: `GET /resource?pageNumber=1&pageSize=20&search=...&sortBy=name&sortDir=asc` returns `ApiResponse<PagedResponse<T>>`. `pageSize` must be 100 or less.
- Status changes use sub-resources rather than generic PUT: `PATCH /patients/{id}/status`, `POST /patients/{id}/merge-requests`.
- Concurrency: mutable resources return `rowVersion` (xmin). Updates must send it back, and a mismatch returns **409**.
- Idempotency (from Phase 3): payment and dispensing POSTs require an `Idempotency-Key` header.

## Response envelope

```json
{
  "success": false,
  "message": "Validation failed.",
  "data": null,
  "errors": [{ "code": "VALIDATION_ERROR", "message": "Mobile number is required.", "field": "mobileNumber" }],
  "traceId": "00-4bf92f...-01"
}
```

## Status codes
See ARCHITECTURE.md §6.2. Responses are never 200 with `success: false`.

## Endpoint catalogue
*(Filled in per module.)*

### Identity (implemented)
| Method | Route | Permission |
|---|---|---|
| POST | `/api/v1/auth/login` | anonymous |
| POST | `/api/v1/auth/refresh` | anonymous (refresh token from HttpOnly cookie) |
| POST | `/api/v1/auth/logout` | anonymous (revokes the session in the refresh cookie) |
| POST | `/api/v1/auth/change-password` | authenticated |
| GET | `/api/v1/auth/me` | authenticated (profile + effective permissions) |
| GET/POST | `/api/v1/users` | Identity.Users.View / Manage |
| GET/PUT | `/api/v1/users/{id}` | Identity.Users.View / Manage |
| PATCH | `/api/v1/users/{id}/status` | Identity.Users.Manage |
| POST | `/api/v1/users/{id}/reset-password` | Identity.Users.Manage |
| POST | `/api/v1/users/{id}/unlock` | Identity.Users.Manage |
| PUT | `/api/v1/users/{id}/roles` | Identity.Roles.Manage |
| PUT | `/api/v1/users/{id}/permissions` | Identity.Roles.Manage (direct grants) |
| GET/POST | `/api/v1/roles` | Identity.Roles.View / Manage |
| GET/PUT/DELETE | `/api/v1/roles/{id}` | Identity.Roles.View / Manage |
| PUT | `/api/v1/roles/{id}/permissions` | Identity.Roles.Manage |
| GET | `/api/v1/permissions` | Identity.Roles.View |
| GET | `/api/v1/login-history` | Identity.LoginHistory.View |

**Identity notes**
- Login and change-password responses include `accessToken`, `accessTokenExpiresAt` and `user` (with roles and effective permissions). The refresh token is never in a response body.
- `POST /auth/login` and `POST /auth/change-password` are rate limited per IP (`RateLimiting:AuthPermitPerMinute`, 10 by default).
- Users with `mustChangePassword = true` have **no** effective permissions until they change their password.
- Error codes: `INVALID_CREDENTIALS`, `ACCOUNT_LOCKED`, `ACCOUNT_INACTIVE`, `INVALID_REFRESH_TOKEN` (401); `USERNAME_TAKEN`, `EMAIL_TAKEN`, `ROLE_NAME_TAKEN`, `ROLE_IN_USE`, `CONCURRENCY_CONFLICT` (409); `INVALID_CURRENT_PASSWORD`, `PASSWORD_SAME_AS_CURRENT`, `CANNOT_CHANGE_OWN_ACCOUNT`, `LAST_SUPER_ADMINISTRATOR`, `SUPER_ADMINISTRATOR_REQUIRED`, `SUPER_ADMINISTRATOR_PERMISSIONS_REQUIRED`, `ROLE_SYSTEM_DELETE`, `ROLE_NOT_FOUND`, `PERMISSION_NOT_FOUND` (422).
