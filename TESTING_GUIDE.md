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
From repo root:
```
dotnet test PassManAPI.Tests/PassManAPI.Tests.csproj
```

### What automated tests do NOT do
- No MySQL access.
- No direct DbContext calls; behaviors observed via HTTP (except seeding checks that use RoleManager via DI).
- No real JWT/claim-based auth; only the dev header scheme for test runs.

## Postman smoke tests (end-to-end)

The collection `scripts/PASSMAN-tests.postman_collection.json` covers the full API surface against a live MySQL instance. It exercises every endpoint in order — auth, vaults, credentials, tags, invitations, vault shares, and audit — and is the primary way to verify the complete request/response cycle including content negotiation (JSON and XML).

### Prerequisites
- API running in **Test** environment on `http://localhost:5248`:
  ```
  ASPNETCORE_ENVIRONMENT=Test dotnet run --project PassManAPI
  ```
  The Test environment activates the `X-UserId` dev-header auth scheme. Without it, all requests will return 401.
- A running **MySQL** instance (the collection hits a real DB, not SQLite in-memory).
- Postman desktop app or Newman CLI.
- A Postman environment with at least these variables set before the first run:
  | Variable | Example value | Set by |
  |---|---|---|
  | `baseUrl` | `http://localhost:5248` | you |
  | `userPassword` | `Password1!` | you |
  | All others (`userId`, `vaultId`, `credentialId`, …) | _(any)_ | tests set them automatically |

### How to run (Postman desktop)
1. **Import**: File → Import → select `scripts/PASSMAN-tests.postman_collection.json`.
2. **Select environment**: choose the environment containing `baseUrl` and `userPassword`.
3. **Run**: open the Collection Runner, select the collection, and click **Run**.

### How to run (Newman CLI)
```bash
npm install -g newman
newman run scripts/PASSMAN-tests.postman_collection.json \
  --env-var baseUrl=http://localhost:5248 \
  --env-var userPassword=Password1!
```

### What the collection covers
Each folder runs sequentially; later folders depend on IDs set by earlier ones.

| Folder | Key assertions |
|---|---|
| **Auth** | Register, login, profile CRUD, password change, 2FA flow, role assignment |
| **Vaults** | CRUD, 403 on foreign vaults, soft-delete cascade |
| **Credentials** | CRUD, password retrieval, tag assignment (set/add/remove) |
| **Tags** | CRUD, rename conflict, forbidden operations |
| **Invitations** | Create, list, accept, revoke |
| **VaultShares** | Share, update permission, revoke, forbidden |
| **Audit** | Paginated log listing, single-log lookup, filters |

### Notes
- The collection creates its own smoke user (`smoke+{timestamp}@test.local`) on every run — no manual DB setup needed.
- If a run stops early and leaves partial state (stale `vaultId`, etc.) clear the environment variables and start a fresh run.
- JSON and XML content negotiation tests are included; responses are validated for both formats on key endpoints.

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

