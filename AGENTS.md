# Board Enthusiasts Backend

## Coding Standard

- Implement maintained external API behavior contract-first and test-first: OpenAPI/Postman assets first, backend verification second, implementation last.
- Keep the maintained backend aligned only to the current Workers + Supabase surface. Do not leave removed runtime behavior represented as if it is still active.
- Supabase Auth owns the maintained authentication lifecycle for this backend. Application tables should own only application data and local projections keyed to Supabase auth user identifiers.
- Prefer narrow service boundaries so Cloudflare request handlers stay thin and backend logic remains testable in isolation.
- New backend-only scripts, config, docs, and deployment templates belong in this submodule. Shared or cross-repository concerns belong in the root repository.
- Work from a branch, commit the completed change set, push it, and open or update a PR.
