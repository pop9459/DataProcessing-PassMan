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
  - Owner can create a vault, add a credential, list it, update it, and delete it.
  - VaultReader (role-assigned) on a shared vault cannot create credentials (403).
- VaultEndpointsTests (HTTP):
  - Owner can create/list/get/update/delete own vault; delete makes get return 404.
  - Shared user (via share) can list/get shared vault but cannot update/delete (403).
- VaultSharesEndpointsTests (HTTP):
  - Owner can share a vault; shared user sees it.
  - Owner can revoke; shared user no longer sees it.
  - Non-owner cannot share (403).
  - Shared user cannot revoke (403).
  - Sharing to a nonexistent user returns 404.

### How to run automated tests

Docker (recommended) — isolated, repeatable, no host SDK required

Prerequisites: Docker Desktop (or another Docker runtime) installed and running.

From the repo root run:

```bash
docker compose up --build --abort-on-container-exit --exit-code-from test test
```

Notes:
- The compose `test` service runs the `mcr.microsoft.com/dotnet/sdk:10.0` image so you do not need .NET 10 installed locally.
- The command returns the test container exit code (non-zero on failures) so CI/teacher VMs detect pass/fail.
- The test service is configured to use a normal test logger verbosity and reduced ASP.NET/EF log levels to limit noise.

Single-container alternative (no compose)

```powershell
docker run --rm -v "${PWD}:/workspace" -w /workspace mcr.microsoft.com/dotnet/sdk:10.0 dotnet test "PassManAPI.Tests/PassManAPI.Tests.csproj" --logger "console;verbosity=normal"
```

Local (optional) — requires .NET 10 SDK

If you prefer running tests on your host machine you must have the .NET 10 SDK installed. Check SDKs with:

```
dotnet --list-sdks
dotnet --info
```

If you don't have .NET 10 installed, download it from https://aka.ms/dotnet/download. The repo includes a `global.json` to pin an SDK version for reproducible local builds; update or remove it if your installed SDK differs from the pinned version.

Run locally from the repo root:

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


Additional Docker troubleshooting tips
- If the test container reports an SDK mismatch (`Requested SDK version`), update `global.json` to match the SDK available in the container (or remove `global.json`), or use an image tag that matches the pinned SDK.
- On Windows, path mounts can require quoting as shown in the PowerShell example; if volume mounts fail, ensure Docker Desktop has file sharing / permissions enabled for the repo path.
- If tests or the container are unexpectedly slow or fail due to resource limits, increase Docker Desktop resources (CPU/memory) temporarily.
1) Run tests (`dotnet test ...`).
2) If adding new features, add/extend integration tests in PassManAPI.Tests.
3) When ready, commit/push and open PR; include what was tested.

## Questions or issues
- Check browser Console + Network.
- Check API logs.
- If stuck, ask the team and reference `API_INTEGRATION_SUMMARY.md`.

