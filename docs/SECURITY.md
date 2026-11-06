# Security

Design summary: ARCHITECTURE.md §7–§9. Permissions: ROLE-PERMISSION-MATRIX.md.

## Controls (Phase 0–1)
| Control | Implementation |
|---|---|
| Transport | HTTPS everywhere; HSTS in production. The Docker stack itself speaks plain HTTP: production must terminate TLS at a reverse proxy in front of the UI container (forwarding `X-Forwarded-Proto`), and only the UI port may be reachable from other machines |
| Reverse proxy | `X-Forwarded-For` / `X-Forwarded-Proto` are honoured from loopback proxies only (ASP.NET Core default). `ForwardedHeaders:TrustAll=true` trusts every sender and is set only by `docker-compose.yml`, where the API port is bound to `127.0.0.1` so nothing but nginx (and the host) can reach it |
| Authentication | ASP.NET Core Identity password hashing (PBKDF2), JWT access 15 min, rotating SHA-256-hashed refresh tokens 7 days in an `HttpOnly; SameSite=Strict` cookie that is `Secure` whenever the request arrived over HTTPS (directly or via the proxy), reuse detection revokes the token family |
| Password policy | 8–128 characters with upper, lower and a digit; forced change on first login and after an admin reset; no permissions until changed |
| Session | UI signs out after 30 minutes idle; deactivation and password reset revoke all refresh tokens; permission changes apply on the next request |
| Privilege protection | Only Super Administrators can grant or revoke the Super Administrator role; the last active Super Administrator cannot be removed or deactivated; the Super Administrator role must keep `Identity.Users.Manage` and `Identity.Roles.Manage`; clinical permissions flagged `RequiresExplicitGrant` are never auto-granted |
| Lockout | 5 failures → 15 min lockout; all attempts in `auth.LoginHistory` |
| Authorization | Permission policy on every endpoint; effective permissions loaded per request (no cross-request cache) |
| Input validation | FluentValidation on every request DTO; EF Core parameterized queries only |
| CORS | Allow-list of UI origin(s) from configuration |
| Rate limiting | Fixed window per client IP on `/auth/login`, `/auth/refresh`, `/auth/logout` and `/auth/change-password` |
| Fresh install | `/api/v1/system/info` is anonymous but exposes no environment name; the API logs `STARTUP CHECK FAILED` at error level while no active Super Administrator exists |
| Secrets | Environment variables / user-secrets; nothing in `appsettings*.json` that is secret |
| Error output | No stack traces/internal messages in responses; TraceId for correlation |
| Logging hygiene | No passwords, tokens, payment data or clinical content in logs |
| Files | Stored outside web root; served only via authorized endpoint; MIME + extension + size validation; SHA-256 recorded |
| Audit | Append-only `audit.AuditLogs`; sensitive patient record views recorded |
| Concurrency | `xmin` optimistic concurrency; unique constraints on business identifiers |

## Before production (Phase 10)
Backup encryption, restore test, penetration test, dependency vulnerability scan, review against Bangladesh data-protection and medical-record requirements with qualified local professionals (BRD §11).
