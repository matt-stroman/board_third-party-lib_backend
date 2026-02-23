# New Developer Setup (API-First Backend MVP)

This guide is for getting a new developer to a working local backend environment quickly.

## Table of Contents

- [Prerequisites](#prerequisites)
- [Quick Start (Closest to Single Operation)](#quick-start-closest-to-single-operation)
- [VS Code Setup (Recommended)](#vs-code-setup-recommended)
- [Common Commands](#common-commands)
- [Manual Equivalents (If You Prefer Not to Use Scripts)](#manual-equivalents-if-you-prefer-not-to-use-scripts)
- [Project Layout (Current)](#project-layout-current)
- [Troubleshooting](#troubleshooting)
- [Next Setup Docs to Add (Recommended)](#next-setup-docs-to-add-recommended)

Current repo state:

- Backend is scaffolded and runnable (`ASP.NET Core + PostgreSQL`)
- Frontend MAUI client direction is documented, but the MAUI app is not scaffolded yet
- Developer/player experiences are intended to be **API-first**

## Prerequisites

Install these on your machine:

1. **Git**
2. **Docker Desktop** (or Docker Engine with Compose support)
3. **.NET SDK 10.x** (the backend is currently pinned and targeted to `.NET 10` / `net10.0`)
4. **PowerShell 7+** (recommended for the provided automation script)

Optional but useful:

- VS Code / Visual Studio / Rider
- `curl` for quick endpoint checks

## VS Code Setup (Recommended)

This repository includes shared VS Code workspace configuration in the root `/.vscode/` folder for backend development.

Included config:

- Launch profiles for backend **Debug** and **Release** runs
- Tasks that start/reuse PostgreSQL dependencies and build the backend
- Recommended extensions in [`/.vscode/extensions.json`](../../.vscode/extensions.json)

Recommended workflow in VS Code:

1. Open the repo root workspace (`board-third-party-lib`).
2. Open **Run and Debug** (`Ctrl+Shift+D`).
3. Select `Backend API (Debug Build)` or `Backend API (Release Build)`.
4. Press `F5`.

The configured pre-launch tasks will:

- start/reuse the `board_tpl_postgres` container
- build the backend in the selected configuration
- launch the API in the integrated terminal

### .NET HTTPS development certificate prompt (manual step)

When running/debugging .NET apps in VS Code, you may see a security prompt asking to create/trust a development certificate.

- This is a normal **manual OS trust** step for .NET local development.
- It may require OS/admin confirmation and cannot be fully automated by repo config.
- The current backend launch path is HTTP-based, but tooling may still prompt for the cert.

If prompted, approve it. You can also run this once manually:

```powershell
dotnet dev-certs https --trust
```

Or run the VS Code task:

- `tpl: trust dotnet dev certificate`

## Quick Start (Closest to Single Operation)

From the repository root:

```powershell
pwsh ./scripts/dev.ps1 bootstrap
pwsh ./scripts/dev.ps1 up
```

What this does:

- `bootstrap`: initializes git submodules (if needed) and restores the backend solution
- `up`: starts local PostgreSQL (or reuses an existing `board_tpl_postgres` container) and runs the backend API
- together, these commands provide a reliable first-time setup + startup flow
- for convenience, `pwsh ./scripts/dev.ps1 up -Bootstrap` is still supported

When the API is running, verify:

```powershell
curl http://localhost:5085/health/live
curl http://localhost:5085/health/ready
```

## Common Commands

Run environment checks:

```powershell
pwsh ./scripts/dev.ps1 doctor
```

Run one-time project bootstrap (submodules + restore):

```powershell
pwsh ./scripts/dev.ps1 bootstrap
```

Start only dependencies (Postgres) without launching the API:

```powershell
pwsh ./scripts/dev.ps1 up -DependenciesOnly
```

Stop dependencies:

```powershell
pwsh ./scripts/dev.ps1 down
```

Show dependency status:

```powershell
pwsh ./scripts/dev.ps1 status
```

Run backend tests (unit + integration):

```powershell
pwsh ./scripts/dev.ps1 test
```

Run only unit tests:

```powershell
pwsh ./scripts/dev.ps1 test -SkipIntegration
```

Run versioned Postman API tests via Newman (optional, requires Node.js / `npx`):

```powershell
pwsh ./backend/scripts/run-postman.ps1
```

## Manual Equivalents (If You Prefer Not to Use Scripts)

Initialize submodules:

```powershell
git submodule update --init --recursive
```

Restore backend solution:

```powershell
dotnet restore ./backend/Board.ThirdPartyLibrary.Backend.sln
```

Start PostgreSQL:

```powershell
docker compose -f ./backend/docker-compose.yml up -d postgres
```

Run backend API:

```powershell
dotnet run --project ./backend/src/Board.ThirdPartyLibrary.Api/Board.ThirdPartyLibrary.Api.csproj
```

## Project Layout (Current)

- `backend/`: ASP.NET Core API, tests, backend CI workflow
- `frontend/`: frontend submodule (MAUI client work planned)
- [`docs/`](../../docs): project-wide technical direction docs (root repo)
- [`backend/docs/`](../docs): backend-specific setup docs (this submodule)
- [`backend/postman/`](../postman): versioned Postman collections/environments for API endpoint testing
- [`scripts/`](../../scripts): root-level developer orchestration scripts

## Troubleshooting

### Port 5432 already in use

Update the host port mapping in `backend/docker-compose.yml` (for example `5433:5432`) and update the backend connection string accordingly:

- `backend/src/Board.ThirdPartyLibrary.Api/appsettings.Development.json`
- or environment variable `ConnectionStrings__BoardLibrary`

### Docker container starts but readiness fails

Check container logs:

```powershell
docker logs board_tpl_postgres
```

### Script says a command is missing

Run:

```powershell
pwsh ./scripts/dev.ps1 doctor
```

Then install the missing prerequisite and rerun the quick-start command.

### Container name conflict for `board_tpl_postgres`

The automation script now attempts to reuse an existing `board_tpl_postgres` container before creating a new one with Docker Compose.

If you still want to reset it completely:

```powershell
docker stop board_tpl_postgres
docker rm board_tpl_postgres
pwsh ./scripts/dev.ps1 up
```

### VS Code warns about HTTPS development certificate

This is expected for .NET development tooling on some machines.

- Approve the prompt, or run `dotnet dev-certs https --trust` manually once.
- You can also run the VS Code task: `tpl: trust dotnet dev certificate`.

## Next Setup Docs to Add (Recommended)

As the project evolves, add dedicated docs for:

- MAUI client local setup (Android emulator/device, workloads, SDK tools)
- API auth/local identity provider setup
- Database migrations workflow
- Provider sandbox credentials (payments/content hosts) for integration testing

See also:

- [Technology direction (`docs/technology-fit-recommendation.md`)](../../docs/technology-fit-recommendation.md)
- [Phase 1 Postgres setup (`backend/docs/backend-phase-1-postgres-setup.md`)](backend-phase-1-postgres-setup.md)
- [Postman API testing (`backend/docs/postman-api-testing.md`)](postman-api-testing.md)
