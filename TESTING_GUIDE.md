# Testing Guide

Two test layers: xUnit integration tests (SQLite, no Docker needed) and a Postman/Newman collection (full stack against MySQL).

---

## xUnit integration tests

Run against an in-memory SQLite database via `TestWebApplicationFactory`. Auth uses a dev-header scheme (`X-UserId`) instead of real JWT. 121 tests, 0 failures.

```bash
dotnet test PassManAPI.Tests/PassManAPI.Tests.csproj
# or in Docker:
docker compose run --rm test
```

### Test classes

| Class | What it covers |
|---|---|
| AuthEndpointsTests | Register, login, profile GET/PUT, delete |
| AuthorizationSeedingTests | Roles exist and carry the correct `permission` claims |
| AuthorizationPolicyTests | Permission enforcement per role (VaultOwner allowed, VaultReader 403) |
| AuthAssignRoleTests | Admin assigns role; permissions endpoint reflects it |
| CredentialsEndpointsTests | Credential CRUD; VaultReader blocked from creating (403) |
| VaultEndpointsTests | Vault CRUD; shared user can read but not mutate (403) |
| VaultSharesEndpointsTests | Share/revoke access control; `GetMyVaultAccess` ownership types |
| UserEndpointsTests | User profile/vaults/tags CRUD; cross-user access blocked (403) |
| TagsEndpointsTests | Tag CRUD; duplicate/cross-user name handling (regression #163) |
| AuditEndpointsTests | Log listing with pagination/filters; vault logs; admin all-logs |
| JwtAuthFlowTests | JWT from register/login carries role+permission claims (regression #159) |
| XmlSerializationTests | All endpoints accept XML bodies and return XML via `Accept` header |
| InvitationTests | Full invite lifecycle (create → accept → vault visible) and revoke |
| InvitationModelTests | Model invariants, constructor validation, EF persistence and cascade |
| AttachmentModelTests | Model invariants, method validation, EF persistence and cascade |

---

## End-to-end tests (Newman — recommended)

Newman is the Postman CLI runner. It needs no account, no GUI, and has no secrets detection. It is the only reliable way to run the collection on a clean machine.

```bash
docker compose --profile e2e up --abort-on-container-exit --scale passman-gui=0
```

`--scale passman-gui=0` excludes the GUI — it isn't needed for API tests and crashes on low-memory VMs (OOM in the file watcher), which would kill Newman before it runs.

Newman starts automatically once the API passes its healthcheck, runs all 152 requests, then exits.

### Reading the output

Each request prints a block while running:
```
→ Auth - Register (201)
  POST http://passman-api:8080/api/auth/register [201 Created, 1.2kB, 45ms]
  ✓  hardening: json responses are parseable
  ✓  201 Created
```

A failure looks like:
```
→ VaultShares - Revoke (204)
  DELETE http://passman-api:8080/api/vaults/3/share/5 [404 Not Found, 300B, 12ms]
  ✗  204 NoContent
     expected response to have status code 204 but got 404
```

The summary table prints at the very end — **the `assertions` row is the one to check**:
```
│              assertions │  753 │    0 │
                                    ↑       ↑
                                 total   failed
```
`failed = 0` → full pass. `failed > 0` → scroll up and find the `✗` lines.

Newman exits 0 on pass, 1 on any failure:
```bash
docker compose --profile e2e up --abort-on-container-exit --scale passman-gui=0
echo "exit: $LASTEXITCODE"   # PowerShell
echo "exit: $?"              # bash
```

### What the collection covers

| Folder | Key assertions |
|---|---|
| Auth | Register, login, profile CRUD, role assignment |
| Vaults | CRUD, 403 on foreign vaults, soft-delete |
| Credentials | CRUD, password retrieval, tag assignment |
| Tags | CRUD, rename conflict, forbidden operations |
| Invitations | Create, list, accept, revoke |
| VaultShares | Share, revoke, forbidden |
| Audit | Paginated logs, single-log lookup, filters |

---

## Postman desktop (unreliable — avoid for verification)

Importing the collection into Postman desktop works on most machines, but **we cannot guarantee it on a fresh install**. Postman has a secrets detection feature that, depending on account state and sync settings, may prompt to remove detected tokens on import — stripping all `Authorization: Bearer {{accessToken}}` headers from the collection. Every request then returns 401 and all tests fail. Reproducing the issue requires a fresh Postman account each time, so we cannot document reliable steps to avoid it.

**Use Newman instead.** If you do use Postman desktop:
1. Import `scripts/PASSMAN-tests.postman_collection.json`.
2. If prompted about secrets, choose **No, keep** (not "Remove"). If you see "Remove" was already applied and auth headers are missing, re-import.
3. No environment needed — `baseUrl` (`http://localhost:5246`) and `userPassword` (`Password1!`) are collection variables. All other variables are set automatically as the collection runs.
