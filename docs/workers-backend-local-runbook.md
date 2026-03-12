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
- Local auth email flows are backed by Inbucket. The maintained sender identity is configured in [`backend/supabase/config.toml`](../supabase/config.toml) as `Board Enthusiasts <noreply@boardenthusiasts.com>`, and the branded HTML email templates are checked in under [`backend/supabase/templates/`](../supabase/templates/).
- Supabase Auth redirect allowlists must include the maintained SPA callback paths, especially [`/auth/signin`](../../frontend/src/App.tsx) and [`/auth/signin?mode=recovery`](../../frontend/src/App.tsx), or email links will fall back to the site root instead of the recovery/sign-in flow.
- Supabase's confirmation and recovery templates may include the `{code}` placeholder. The maintained frontend supports those confirmation and recovery codes directly, and the checked-in branded templates render both the action link and the one-time code.
- Signup stores `firstName` in Supabase auth user metadata, and the maintained templates use that metadata to greet recipients by first name when it is available.
