# Workers Backend Local Runbook

The maintained backend local stack is:

- local Supabase services
- the Workers API in [`apps/workers-api`](../apps/workers-api)

From the root repository:

```bash
python ./scripts/dev.py database up
python ./scripts/dev.py auth up
python ./scripts/dev.py api
```

Useful verification commands:

```bash
python ./scripts/dev.py api-test --start-workers
python ./scripts/dev.py contract-smoke --start-workers
python ./scripts/dev.py workers-smoke --start-stack
```

Notes:

- `database up` starts PostgreSQL only.
- `auth up` adds the local auth services needed for token and role testing.
- `api` starts the maintained backend on top of the local database and auth runtime.
- The shared TypeScript contract package lives in the root workspace, so the backend is expected to be run through the root CLI.
