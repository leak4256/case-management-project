# Case Management System

A case-tracking system for organisational requests, supporting server-side paging, filtering, search, sorting, filter-aware aggregations, and status updates protected by optimistic concurrency.

* **Backend:** .NET 9 Web API, EF Core 9, SQL Server
* **Frontend:** Angular 20, standalone components, signals, Angular Material
* **Data:** 10,000 cases seeded automatically when the database is empty

---

## Live demo

* Application — https://proud-smoke-041bba700.7.azurestaticapps.net
* Swagger UI — https://app-case-mgmt-mwatjxzd.azurewebsites.net/swagger

**The demo is hosted on Azure free tier; the first request may take up to a minute while the database and app wake from idle.**

---

## Quick Start

From the repository root:

```bash
cp .env.example .env
docker compose up --build
```

Make sure Docker Desktop is installed and running before starting the application.

Once the containers are ready:

* **Application:** http://localhost:4200
* **Swagger UI:** http://localhost:4200/swagger

For detailed setup instructions and alternative development options, see the sections below.


## Running the application

There are two ways to run the application:

* **Route A — Docker Compose:** runs the database, API, and frontend in containers.
* **Route B — Local development:** runs the API and frontend locally, using either an existing SQL Server installation or SQL Server in Docker.

---

## Getting Started

Clone the repository and navigate to the project directory:

```bash
git clone https://github.com/leak4256/case-management-project.git
cd <repository-directory>
```

## Route A — Docker Compose

### Prerequisite: Docker Desktop

Docker Desktop is required to run the full application with Docker Compose.

On Windows, Docker Desktop can be installed with:

```powershell
winget install -e --id Docker.DockerDesktop
```

After installation, start Docker Desktop and verify that Docker is available:

```bash
docker --version
docker compose version
```

### Start the application

From the repository root:

```bash
cp .env.example .env          # PowerShell: Copy-Item .env.example .env
docker compose up --build
```

The `.env.example` file contains a development-only SQL Server password. Copying it is enough to start the application; no additional configuration is required for local Docker usage.

Docker Compose starts three containers:

* SQL Server
* .NET API
* Angular frontend served by nginx

The API waits for SQL Server to become available before starting, and the frontend waits for the API health check.

On the first run, Docker may take a few minutes to build the images.

### URLs

* Application — http://localhost:4200
* Swagger UI — http://localhost:4200/swagger (via nginx) or http://localhost:5180/swagger (direct)

The nginx container serves the Angular application and reverse-proxies `/api` requests to the API.

The API applies the EF Core migrations on startup and seeds 10,000 cases if the database is empty.

### Reset the database

To remove the SQL Server container and its data volume:

```bash
docker compose down -v
```

---

## Route B — Local development

This route runs the API and frontend using the local .NET and Node.js toolchains.

### Prerequisites

| Requirement    | Version / Notes                                                             |
| -------------- | --------------------------------------------------------------------------- |
| .NET SDK       | 9.0                                                                         |
| Node.js        | 20 or later                                                                 |
| SQL Server     | LocalDB, Express, Developer, or SQL Server running in Docker                |
| Docker Desktop | Required only when using Docker for SQL Server or running integration tests |

### 1. Set up SQL Server

The API requires a SQL Server instance. You can either use a SQL Server installation already available on your machine or run SQL Server in Docker.

#### Option A — use an existing SQL Server

For example, for a local SQL Server Express instance:

```bash
dotnet user-secrets set "ConnectionStrings:CaseManagementDb" "Server=localhost\SQLEXPRESS;Database=CaseManagement;Trusted_Connection=True;TrustServerCertificate=True" --project backend/src/CaseManagement.Api
```

Use the connection string appropriate for your local SQL Server installation.

#### Option B — run SQL Server in Docker

If Docker Desktop is not installed, on Windows:

```powershell
winget install -e --id Docker.DockerDesktop
```

Start Docker Desktop, then from the repository root:

```bash
cp .env.example .env          # PowerShell: Copy-Item .env.example .env
docker compose up -d sqlserver
```

This starts only the SQL Server container. The API and frontend still run locally.

SQL Server is exposed on host port `14330` to avoid conflicts with a local SQL Server instance.

### 2. Configure the connection string

The API uses the `CaseManagementDb` connection string.

The connection string is intentionally empty in `appsettings.json` so credentials are not committed to the repository.

For the Docker SQL Server instance:

```bash
dotnet user-secrets set "ConnectionStrings:CaseManagementDb" "Server=localhost,14330;Database=CaseManagement;User Id=sa;Password=LocalDev_2026!;TrustServerCertificate=True" --project backend/src/CaseManagement.Api
```

The password must match the value configured in `.env`.

Alternatively, the connection string can be supplied through:

```text
ConnectionStrings__CaseManagementDb
```

The environment variable takes precedence over the application configuration.

### 3. Run the API

```bash
dotnet run --project backend/src/CaseManagement.Api
```

On startup, the API:

1. Applies the EF Core migrations.
2. Seeds 10,000 cases if the database is empty.

No manual migration or seed script is required.

The application uses EF Core with the SQL Server provider, which relies on the underlying SQL Server client driver to communicate with SQL Server.

### API URLs

* API — http://localhost:5180
* Swagger UI — http://localhost:5180/swagger
* Health — http://localhost:5180/health

### 4. Run the frontend

```bash
cd frontend/case-management-web
npm install
npm start
```

The application runs on:

http://localhost:4200

The development environment points to:

```text
http://localhost:5180/api
```

The API allows the frontend origin through its CORS configuration.

### 5. Run the tests

```bash
dotnet test backend/CaseManagement.sln
```

The integration tests use Testcontainers and a real SQL Server instance. Docker Desktop must therefore be running.

A single SQL Server container is shared across the test run.

---

## Technologies

| Technology       | Version / Usage |
| ---------------- | --------------- |
| .NET             | 9.0             |
| EF Core          | 9.0.19          |
| SQL Server       | 2022            |
| Swashbuckle      | 10.2.3          |
| Bogus            | 35.6.5          |
| xUnit            | 2.9.2           |
| Testcontainers   | 4.14.0          |
| Angular          | 20.2            |
| Angular Material | 20.2            |
| TypeScript       | 5.9             |

The frontend uses Angular 20 standalone components, signals, `input()` / `output()`, `inject()`, built-in `@if` / `@for` control flow, and `OnPush` change detection.

RxJS is used where it fits the problem, including debouncing search input and switching between API queries.

---

## Solution structure

```text
backend/
  src/
    CaseManagement.Domain
      Entities and enums

    CaseManagement.Application
      DTOs, query models, validation,
      services and repository interfaces

    CaseManagement.Infrastructure
      EF Core, DbContext, indexes,
      migrations, repository and seeder

    CaseManagement.Api
      Controllers, error handling,
      CORS, Swagger and application setup

  tests/
    CaseManagement.Api.Tests
      Integration tests through the real HTTP pipeline

frontend/
  case-management-web/
    src/app/
      core
        Typed API client, models and HTTP error interceptor

      features/cases
        Case list, filters, summary panel and status editor

      shared
        Loading, empty and error states
```

The repository also contains:

```text
docker-compose.yml
backend/src/CaseManagement.Api/Dockerfile
frontend/case-management-web/Dockerfile
frontend/case-management-web/nginx.conf
```

The dependency direction is:

```text
Api ────────────────┐
                    ↓
Infrastructure → Application → Domain
```

`Domain` has no dependency on EF Core or ASP.NET concerns.

---

## API surface

| Endpoint                       | Description                                                            |
| ------------------------------ | ---------------------------------------------------------------------- |
| `GET /api/cases`               | Returns a page of cases with search, filtering, sorting and pagination |
| `GET /api/cases/summary`       | Returns aggregate figures using the same filters                       |
| `GET /api/cases/{id}`          | Returns a single case and its concurrency token                        |
| `PATCH /api/cases/{id}/status` | Updates the case status using optimistic concurrency                   |

The cases endpoint supports:

* `search`
* `status[]`
* `priority[]`
* `organization`
* `createdFrom`
* `createdTo`
* `sortBy`
* `sortDirection`
* `page`
* `pageSize`

Paging, filtering, searching and sorting are performed on the server; the browser does not load all 10,000 records and filter them locally.

The API uses `ValidationProblemDetails` for validation failures and `ProblemDetails` for error responses, including a `traceId`.

`PATCH` is used for status changes because the operation updates only part of the resource.

---

## Database and indexing

The case data has a fixed relational structure: seven scalar fields and two enums, with no variable schema or nested documents.

The main access patterns are filtering, sorting and aggregation over these columns, so SQL Server is used with indexes designed around those queries.

The list is supported by four indexes:

| Index                         | Key                        | Purpose                               |
| ----------------------------- | -------------------------- | ------------------------------------- |
| `IX_Cases_CreatedAt`          | `CreatedAt DESC`           | Default ordering and date filtering   |
| `IX_Cases_Status_CreatedAt`   | `Status, CreatedAt DESC`   | Status filtering with date ordering   |
| `IX_Cases_Priority_CreatedAt` | `Priority, CreatedAt DESC` | Priority filtering with date ordering |
| `IX_Cases_OrganizationName`   | `OrganizationName`         | Organisation filtering                |

The indexes are covering indexes for the fields required by the list query, allowing SQL Server to satisfy the query from the index without an additional lookup to the base table.

`Status` and `Priority` are stored as `tinyint` values because they represent fixed sets of values, while the API exposes readable enum names.

The summary endpoint calculates the required aggregate figures using a single grouped query rather than issuing a separate query for each figure.

---

## Optimistic concurrency

Status updates use SQL Server `rowversion` for optimistic concurrency.

The version associated with a case is returned with the list and exposed through the API as an `ETag` when retrieving an individual case.

When updating a status, the client sends the version it received using the `If-Match` header.

EF Core uses the concurrency token as part of the `UPDATE` condition. If another client has modified the case since the version was read, the update affects zero rows and the API returns `409 Conflict`.

The response includes the current status, version and timestamp so the client can handle the conflict without making another request.

The API also handles invalid concurrency headers:

* Missing `If-Match` → `428 Precondition Required`
* Malformed value → `400 Bad Request`
* Concurrent modification → `409 Conflict`

Repeating the current status is idempotent, but the concurrency version is still checked.

Pessimistic locking was not used because the application uses stateless HTTP requests and should not hold a database lock while a user is deciding what to do.

---

## What I would improve with more time

1. **Cache the summary.**
   The summary query could use short-lived caching. The cache key would need to include the active filters, and status changes would need to invalidate the relevant cache entries.

2. **Refresh the table after a status change.**
   I would use lightweight polling because the system does not require real-time updates. It would reduce stale data while keeping the implementation simpler than maintaining a persistent SignalR connection. Polling would complement, not replace, the `If-Match` concurrency check.

---

## Deployment

The demo runs on three Azure free-tier services:

* **Azure SQL Database** (General Purpose serverless, free limit) — auto-pauses when idle.
* **App Service** (Linux, F1) — hosts the API.
* **Static Web Apps** — serves the Angular application.

Two GitHub Actions workflows in `.github/workflows/` deploy on a push that touches the relevant
tree. The API workflow publishes the .NET project and deploys it; the frontend workflow builds the
Angular application with the `azure` configuration and uploads the result.

The `azure` build configuration exists because the two hosting routes need different API
addresses: under Docker, nginx serves the application and the API on one origin, so
`environment.ts` uses the relative `/api`. On Azure the two are separate hosts, so
`environment.azure.ts` carries the absolute App Service URL and the API allows that origin through
`Cors__AllowedOrigins__0`.

The connection string and the allowed origin are App Service application settings, not repository
content.

---

## Notes

* No real credentials are committed to the repository.
* Docker Compose reads its SQL Server configuration from `.env`, which is git-ignored.
* Timestamps are stored and returned as UTC.
