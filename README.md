# HMIS — Hospital Management Information System

A modular monolith for private hospitals in Bangladesh. Stack: ASP.NET Core (.NET 10), Angular 22 + Angular Material, and PostgreSQL 17.

Documentation index: [`docs/README.md`](docs/README.md). Start with [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md).

## Prerequisites
- .NET SDK 10
- Node.js 22+ and npm
- Docker Desktop

## Local ports
| Service | URL |
|---|---|
| PostgreSQL (Docker) | `localhost:5434` (db `hmis`, user `hmis`) |
| API | `https://localhost:7001` (Swagger at `/swagger`, health at `/health`) |
| Angular UI | `http://localhost:4300` |

These ports avoid 5432, 5433 and 4200, which other local projects already use. To change the database port, set `HMIS_DB_PORT` and update the connection string.

## Run everything in Docker
No .NET SDK or Node.js is needed for this.
```powershell
copy .env.example .env          # then set HMIS_JWT_SIGNING_KEY and HMIS_SEED_ADMIN_PASSWORD
docker compose up -d --build
```
This starts PostgreSQL, applies all migrations (`migrator` container, exits when done), then starts the API and the UI.

| Service | URL |
|---|---|
| UI (nginx; proxies `/api` to the API) | http://localhost:4380 |
| API (direct, bound to `127.0.0.1` only) | http://localhost:5380/health |

- Sign in at http://localhost:4380 as `superadmin` with the seed password.
- Optional demo logins for development: set `HMIS_SEED_DEMO_DATA=true` in `.env` before starting. This seeds one employee per role and the logins `admin1`, `reception1`, `cashier1`, `nurse1` and `doctor1` (doctor profile included), all with password `Demo@2026`. Never enable it on a production install.
- After pulling new code, run `docker compose up -d --build` again. New migrations are applied automatically.
- Logs: `docker compose logs -f api`.
- Stop: `docker compose down`. Data is kept in the `hmis-pgdata` volume.
- The Docker stack and the Visual Studio setup below share the same database.
- `docker compose ps` shows the API as `healthy` once `/health` answers; the UI container waits for that.
- The stack serves plain HTTP, which is fine on a workstation or an isolated LAN for evaluation. **Production must terminate TLS at a reverse proxy** (nginx, Caddy, Traefik, a cloud load balancer) in front of the UI port, forwarding `X-Forwarded-Proto`. The sign-in cookie is marked `Secure` only when the request arrives over HTTPS, so without TLS the credentials of every user cross the network in clear text.
- Only the UI port needs to be reachable from other machines. The API port is published on the host's loopback interface only (`127.0.0.1:5380`); the UI container reaches it over the compose network. Keep it that way: the API trusts forwarded headers from nginx (`ForwardedHeaders__TrustAll=true` in `docker-compose.yml`), which is only safe while nothing else can reach it.

## First-time setup (Visual Studio / ng serve)
```powershell
# 1. Database
docker compose up -d postgres

# 2. API secrets (never committed)
cd src/Api/Hmis.Api
dotnet user-secrets set "Jwt:SigningKey" "<random string of at least 32 characters>"
dotnet user-secrets set "Identity:SeedAdmin:Password" "<initial Super Admin password>"
cd ../../..

# 3. Apply database migrations
./scripts/migrate.ps1

# 4. Run the API (seeds roles, permissions and the Super Admin on first start)
cd src/Api/Hmis.Api
dotnet run --launch-profile https

# 5. Run the UI (in a second terminal). It proxies /api to the API.
cd src/Frontend/hospital-ui
npm install
npm start
```

Sign in at http://localhost:4300 as `superadmin` with the seed password. You must change it at first sign-in.
In the Development environment the demo logins (`admin1`, `reception1`, `cashier1`, `nurse1`, `doctor1` / `Demo@2026`) are seeded as well, via `DemoData:Enabled` in `appsettings.Development.json`.
To see the configured seed password: `dotnet user-secrets list --project src/Api/Hmis.Api`.

New migration: `./scripts/add-migration.ps1 -Module Identity -Name <MigrationName>`.

The database credentials in `docker-compose.yml` and `appsettings.Development.json` are for the **local development container only**. Other environments use environment variables, for example `ConnectionStrings__HmisDatabase` and `Jwt__SigningKey`.

## Solution layout
```text
src/
├── Api/Hmis.Api                 Host: middleware, filters, auth, Swagger
├── BuildingBlocks/              Shared Domain/Application/Infrastructure/Presentation/Contracts
├── Modules/<Module>/            4 projects per module (Domain, Application, Infrastructure, Presentation)
└── Frontend/hospital-ui         Angular app
docs/                            BRD, architecture, ERD, permissions, status
docker/                          Dockerfiles (api, ui), nginx config, database init scripts
```

## Status
See [`docs/IMPLEMENTATION-STATUS.md`](docs/IMPLEMENTATION-STATUS.md).
