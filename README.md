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

## First-time setup
```powershell
# 1. Database
docker compose up -d postgres

# 2. API secrets (never committed)
cd src/Api/Hmis.Api
dotnet user-secrets set "Jwt:SigningKey" "<random string of at least 32 characters>"

# 3. Run the API
dotnet run --launch-profile https

# 4. Run the UI (in a second terminal)
cd src/Frontend/hospital-ui
npm install
npm start
```

The database credentials in `docker-compose.yml` and `appsettings.Development.json` are for the **local development container only**. Other environments use environment variables, for example `ConnectionStrings__HmisDatabase` and `Jwt__SigningKey`.

## Solution layout
```text
src/
├── Api/Hmis.Api                 Host: middleware, filters, auth, Swagger
├── BuildingBlocks/              Shared Domain/Application/Infrastructure/Presentation/Contracts
├── Modules/<Module>/            4 projects per module (Domain, Application, Infrastructure, Presentation)
└── Frontend/hospital-ui         Angular app
docs/                            BRD, architecture, ERD, permissions, status
docker/                          Container init scripts
```

## Status
See [`docs/IMPLEMENTATION-STATUS.md`](docs/IMPLEMENTATION-STATUS.md).
