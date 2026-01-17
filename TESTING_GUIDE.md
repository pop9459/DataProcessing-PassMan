# Testing Guide

Plain, explicit, and structured so we can extend it easily. All sections follow the same concise style. Scope: backend API only (no frontend/UI).

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
- AuthEndpointsTests (HTTP):
  - Register → 201 with profile/token placeholder.
  - Login → 200.
  - Get profile (`/me` with X-UserId) → 200.
  - Update profile → 200 with changes.
  - Delete → 204; subsequent `/me` with same X-UserId → 401 (user gone).
- AuthorizationSeedingTests (DI):
  - Each seeded role exists (Admin, SecurityAuditor, VaultOwner, VaultReader).
  - Each role has exactly its mapped `permission` claims; all role claims use claim type `permission`.
- AuthorizationPolicyTests (HTTP):
  - VaultOwner can create vault (allowed).
  - VaultReader cannot create vault (admin assigns role; expect 403).
  - `/api/auth/permissions` returns effective permissions for the user.
- AuthAssignRoleTests (HTTP):
  - Admin can assign a role (e.g., VaultReader) to a user.
  - Permissions endpoint reflects the assigned role (e.g., has `vault.read`, lacks `vault.create`).
- CredentialsEndpointsTests (HTTP):
  - Owner can create a vault, add a credential, and list it.
  - VaultReader (role-assigned) on a shared vault cannot create credentials (403).
- VaultEndpointsTests (HTTP):
  - Owner can create/list/get/delete own vault; delete makes get return 404.
  - Shared user (via share) can list/get shared vault but cannot update/delete (403).
- VaultSharesEndpointsTests (HTTP):
  - Owner can share a vault; shared user sees it.
  - Owner can revoke; shared user no longer sees it.
  - Non-owner cannot share (403).

### How to run automated tests
From repo root:
```
dotnet test PassManAPI.Tests/PassManAPI.Tests.csproj
```

### What automated tests do NOT do
- No MySQL access.
- No direct DbContext calls; behaviors observed via HTTP (except seeding checks that use RoleManager via DI).
- No real JWT/claim-based auth; only the dev header scheme for test runs.

## Developer debugging tips
- Check HTTP responses and payloads for `/api/auth/*`, `/api/vaults`, `/api/vaults/{id}/share`, `/api/vaults/{vaultId}/credentials`.
- Headers: verify `Authorization` (dev token) and `X-UserId`.
- Logs: check backend console output for errors.

## Known limitations / TODOs
- UI create/edit/delete for vaults/credentials are still TODO.
- Item detail, copy/pw visibility, search/filter, sharing UI, OAuth/JWT, PIN/remember-me, forgot password not implemented.

## Success criteria (quick list)
- Auth flows work end-to-end (register, login, me, update, delete).
- Policies enforce access (owner create allowed; reader create blocked).
- Vault CRUD works for owner; shared users can read but not mutate.
- Credentials can be created/read by owner; shared readers cannot create.
- Sharing works: share grants visibility; revoke removes it.

## Troubleshooting
- “Failed to load vaults/credentials”: ensure API running; check 404/403 vs 500; inspect logs.
- “Network request failed”: API not running; check `docker-compose ps`; firewall/ports.
- “User not found”: apply migrations; inspect Users table.
- Empty vaults but data exists: verify `X-UserId`; curl `/api/vaults`.
- Build errors: `dotnet clean; dotnet build`; confirm Program.cs service registrations/usings.

## Next steps (process)
1) Run tests (`dotnet test ...`).
2) If adding new features, add/extend integration tests in PassManAPI.Tests.
3) When ready, commit/push and open PR; include what was tested.

## Questions or issues
- Check browser Console + Network.
- Check API logs.
- If stuck, ask the team and reference `API_INTEGRATION_SUMMARY.md`.

