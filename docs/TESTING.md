# Testing Guide

Two test layers: **xUnit** (integration, SQLite in-memory, no Docker needed) and **Newman** (end-to-end, full stack against MySQL in Docker).

---

## Running the tests

**Integration tests:**
```bash
docker compose run --rm test
```
Local `dotnet test` also works but requires .NET 10 SDK to be installed — Docker is the supported path.

**E2E tests (Newman):**
```bash
docker compose --profile e2e up --abort-on-container-exit --scale passman-gui=0
```
Starts MySQL and the API, waits for the API healthcheck, runs the full collection, then exits. `--scale passman-gui=0` excludes the GUI — it isn't needed for API tests and crashes on low-memory VMs, which would abort Newman before it runs.

---

## Integration test classes

123 tests across 15 classes. Auth uses a dev-header scheme (`X-UserId`) instead of JWT; DB is SQLite in-memory seeded with roles and demo users on startup.

| Class | What it covers |
|---|---|
| AuthEndpointsTests | Register, login, profile GET/PUT, delete |
| AuthorizationSeedingTests | Roles exist and carry the correct `permission` claims |
| AuthorizationPolicyTests | Permission enforcement per role |
| AuthAssignRoleTests | Admin assigns role; permissions endpoint reflects it |
| JwtAuthFlowTests | JWT from register/login carries role + permission claims (regression #159) |
| UserEndpointsTests | User profile/vaults/tags CRUD; cross-user access blocked (403) |
| VaultEndpointsTests | Vault CRUD; shared user can read but not mutate (403) |
| VaultSharesEndpointsTests | Share/revoke access control; `GetMyVaultAccess` ownership types |
| CredentialsEndpointsTests | Credential CRUD; View-share blocked from mutations (403); soft-deleted vault hides credentials |
| TagsEndpointsTests | Tag CRUD; duplicate/cross-user name handling (regression #163) |
| AuditEndpointsTests | Log listing with pagination/filters; vault logs; admin all-logs |
| XmlSerializationTests | All endpoints accept XML bodies and return XML via `Accept` header |
| InvitationTests | Full invite lifecycle (create → accept → vault visible) and revoke |
| InvitationModelTests | Model invariants, constructor validation, EF persistence and cascade |
| AttachmentModelTests | Model invariants, method validation, EF persistence and cascade |

---

## Newman output

Each request prints a block during the run:
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

The summary table prints at the very end — **check the `assertions` row**:
```
│              assertions │  753 │    0 │
                                    ↑       ↑
                                 total   failed
```
`failed = 0` → full pass (exit code 0). `failed > 0` → scroll up and find the `✗` lines.

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

## Postman desktop (unreliable — use Newman instead)

Importing the collection into Postman desktop works on most machines, but we cannot guarantee it on a fresh install. Postman's secrets detection may prompt to remove detected tokens on import, stripping all `Authorization: Bearer {{accessToken}}` headers — every request then returns 401 and all tests fail. Reproducing it requires a fresh Postman account each time, so there are no reliable documented steps to avoid it.

If you do use Postman desktop: import `scripts/PASSMAN-tests.postman_collection.json`, and if prompted about secrets choose **No, keep** (not "Remove"). No environment needed — `baseUrl` (`http://localhost:5246`) and `userPassword` (`Password1!`) are collection variables; all other variables are set automatically as the collection runs.
