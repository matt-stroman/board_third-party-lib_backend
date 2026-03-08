# board-enthusiasts_backend

Maintained backend implementation for Board Enthusiasts.

The legacy ASP.NET, Keycloak, local PostgreSQL, and Mailpit runtime has been removed from this submodule. The maintained backend now consists of:

- Cloudflare Workers API in [`apps/workers-api`](./apps/workers-api)
- Supabase schema and local project config in [`supabase`](./supabase)
- backend-owned seed tooling in [`scripts/migration-seed.ts`](./scripts/migration-seed.ts)
- backend-owned deployment template in [`cloudflare/workers/wrangler.template.jsonc`](./cloudflare/workers/wrangler.template.jsonc)

## Local Workflow

Run the backend from the root repository so the shared workspace package and root CLI stay in control:

```bash
python ./scripts/dev.py supabase start
python ./scripts/dev.py supabase db-reset
python ./scripts/dev.py workers run
python ./scripts/dev.py api-test --start-workers
python ./scripts/dev.py workers-smoke --start-stack
```

The root CLI is the supported developer entrypoint:

- [`scripts/dev.py`](../scripts/dev.py)
- [`docs/developer-cli.md`](../docs/developer-cli.md)
- [`docs/cloudflare-supabase-workers-wave-2.md`](../docs/cloudflare-supabase-workers-wave-2.md)

## Repository Boundary

This submodule owns backend-only runtime concerns for the maintained stack:

- Workers request handling and backend service logic
- Supabase migrations, seeds, and local provider config
- backend deployment templates and backend-only docs

Historical planning artifacts remain under [`planning`](./planning). They are retained for traceability and are not the source of truth for the maintained backend runtime.
