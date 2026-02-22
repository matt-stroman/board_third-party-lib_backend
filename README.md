# board_third-party-lib_backend

A backend service for third party developers for the Board ecosystem to use to register and share their games with the public.

## Table of Contents

- [Local development (Phase 2)](#local-development-phase-2)

## Local development (Phase 2)

Prereq: local PostgreSQL running (see [`backend/docker-compose.yml`](docker-compose.yml)).

Recommended (repo root, automated):

```bash
pwsh ./scripts/dev.ps1 bootstrap
pwsh ./scripts/dev.ps1 up
```

Backend-only test commands (repo root, automated):

```bash
pwsh ./scripts/dev.ps1 test
pwsh ./scripts/dev.ps1 test -SkipIntegration
```

Run the API:

```bash
dotnet restore
dotnet run --project src/Board.ThirdPartyLibrary.Api
```

Verify endpoints:

```bash
curl http://localhost:5085/health/live
curl http://localhost:5085/health/ready
```

Notes:

- `appsettings.Development.json` is preconfigured for the local Postgres container from Phase 1.
- Override with env var `ConnectionStrings__BoardLibrary` if needed.
