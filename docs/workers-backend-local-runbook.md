# Workers Backend Local Runbook

The maintained backend local stack is:

- local Supabase services
- the Workers API in [`apps/workers-api`](../apps/workers-api)

From the root repository:

```bash
python ./scripts/dev.py supabase start
python ./scripts/dev.py supabase db-reset
python ./scripts/dev.py workers run
```

Useful verification commands:

```bash
python ./scripts/dev.py api-test --start-workers
python ./scripts/dev.py contract-smoke --target migration --start-workers
python ./scripts/dev.py workers-smoke --start-stack
```

Notes:

- `supabase db-reset` reseeds deterministic auth, relational, and storage fixtures.
- `workers run` writes local Wrangler bindings into `apps/workers-api/.dev.vars` before launch.
- The shared TypeScript contract package lives in the root workspace, so the backend is expected to be run through the root CLI.
