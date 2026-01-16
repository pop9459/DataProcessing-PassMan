# Testing Guide

Plain, explicit, and structured so we can extend it easily. All sections follow the same concise style.

## Automated backend tests (integration)
- Type: Integration (HTTP calls to in-memory API). No unit tests yet.
- DB: SQLite in-memory (temporary). MySQL is **not** used in tests.
- Auth: Dev header auth. Client sends `X-UserId`; server loads Identity user, roles, and `permission` claims.
- Roles/permissions: Seeded on test startup (Admin, SecurityAuditor, VaultOwner, VaultReader with permission claims).

### How the test server is built (TestWebApplicationFactory)
1) Environment = `Test`.
2) Use SQLite in-memory; create schema.
3) Run `DbSeeder.SeedAsync(..., seedDemoUsers: true)` for roles/claims/demo users.
4) Register DevHeader auth scheme + policies for every permission (e.g., `vault.create`, `audit.read`).
5) Expose in-memory HTTP server; tests use `HttpClient`.

### What each test class covers
- AuthEndpointsTests (HTTP): register, login, get profile, update profile, delete account (follow-up GET returns 401 because user is gone).
- AuthorizationSeedingTests (DI): roles exist; each has exactly its mapped `permission` claims; all role claims use claim type `permission`.
- AuthorizationPolicyTests (HTTP): VaultOwner allowed to create vault; VaultReader blocked (403) after admin assigns that role; permissions endpoint returns effective permission claims.

### How to run automated tests
From repo root:
```
dotnet test PassManAPI.Tests/PassManAPI.Tests.csproj
```

### What automated tests do NOT do
- No MySQL access.
- No direct DbContext calls; behaviors observed via HTTP (except seeding checks that use RoleManager via DI).
- No real JWT/claim-based auth; only the dev header scheme for test runs.

## Manual UI smoke tests (optional quick checks)
- Registration: Create account via GUI; expect success redirect to vaults.
- Login: Sign in; expect redirect to vaults; token/X-UserId stored in sessionStorage.
- Auth guard: Incognito → navigate to `/vaults` → expect redirect to `/login`.
- Vault list: After login, expect vaults grid or empty-state CTA.
- Vault detail: Click a vault; expect details and items/empty-state.
- Error handling: Stop API and attempt requests → expect friendly errors, no crash.

## Developer debugging tips
- Network tab (browser): check status codes/payloads for `/api/auth/*`, `/api/vaults`, etc.
- Headers: verify `Authorization` (dev token) and `X-UserId`.
- sessionStorage: `passman_auth_token`, `passman_user_id`; clear to simulate logout.
- Logs: check backend console output for errors.

## Known limitations / TODOs
- UI create/edit/delete for vaults/credentials are still TODO.
- Item detail, copy/pw visibility, search/filter, sharing UI, OAuth/JWT, PIN/remember-me, forgot password not implemented.

## Success criteria (quick list)
- Register/login works; guarded routes redirect when unauthenticated.
- Vaults and vault detail load via API; loading and error states show.
- Policies enforce access (owner can create; reader is blocked).
- Session restored on refresh; cleared when storage cleared.

## Troubleshooting
- “Failed to load vaults”: ensure API port is correct; check Network tab for 404/500; check API logs.
- “Network request failed”: API not running; check `docker-compose ps`; firewall/ports.
- “User not found” after registration: apply migrations; inspect Users table.
- Empty vaults but data exists: verify userId header/storage; curl `/api/vaults?userId=...`.
- Build errors: `dotnet clean; dotnet build`; confirm Program.cs service registrations and usings.

## Next steps (process)
1) Run tests (`dotnet test ...`).
2) If adding new features, add/extend integration tests in PassManAPI.Tests.
3) When ready, commit/push and open PR; include what was tested.

## Questions or issues
- Check browser Console + Network.
- Check API logs.
- If stuck, ask the team and reference `API_INTEGRATION_SUMMARY.md`.

