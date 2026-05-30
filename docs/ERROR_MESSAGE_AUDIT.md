# API Error-Message Audit (#162)

> Analysis of error responses across every API controller — which endpoints return
> the wrong status code, an inconsistent body shape, no message, or leak internals.
> This document is the input for the fixes in **#163**.
>
> Method: code review of every controller in `PassManAPI/Controllers/` plus live probing
> of a running stack (`docker compose up db passman-api`) with the #159 fix applied so the
> permission-protected paths are reachable. Probed scenarios: no-token (401), wrong
> owner (403), missing resource (404), invalid body (400), duplicate/conflict.

## TL;DR

The API has **no single error contract**. Five different error body shapes are returned depending on which code path you hit:

| Shape | Produced by | Example | `Content-Type` |
|---|---|---|---|
| **A. RFC7807 `ProblemDetails`** (framework) | `NotFound()`, `ValidationProblem(ModelState)`, `BadRequest(ModelState)`, auto model validation | `{"type":"…rfc9110…","title":"Not Found","status":404,"traceId":"…"}` | `application/problem+json` |
| **B. Plain string** | `NotFound("msg")`, `BadRequest("msg")`, `Unauthorized("msg")` (Vaults, Tags, Credentials, Invitations, VaultShares, User, Auth) | `Vault not found.` | `text/plain` |
| **C. Custom `ErrorResponse`** | `ExceptionHandlerMiddleware` (unhandled exceptions) | `{"type":"…rfc7231…","title":"Internal Server Error","status":500,"detail":"…","traceId":"…"}` | `application/json` |
| **D. Empty body** | `Unauthorized()`, `Forbid()`, 401 challenge | *(none)* | *(none)* |
| **E. Anonymous `{error}` object** | `AuditController` (`BadRequest(new { error })`, `NotFound(new { error })`) | `{"error":"…"}` | `application/json` |

Note shapes **A** and **C** even use **different `type` URIs** (rfc9110 vs rfc7231), A has no `detail` while C does, and E invents its own `{error}` envelope. A client cannot parse errors uniformly.

## Live evidence (probed against running API)

| Scenario | Endpoint | Status | Body (observed) | Issue |
|---|---|---|---|---|
| No token | `GET /api/vaults` | 401 | *(empty)* | D — no body/message |
| Wrong owner | `POST /api/vaults` (userId≠me) | 403 | *(empty)* | D — no body/message |
| Missing vault | `GET /api/vaults/999999` | 404 | `{… "title":"Not Found" …}` **no `detail`** | A — no human message |
| Missing tag | `GET /api/tags/999999` | 404 | `{… "title":"Not Found" …}` **no `detail`** | A — no human message |
| Invalid body | `POST /api/vaults` (no name) | 400 | ValidationProblem w/ `errors` ✓ | OK (but dup messages) |
| Missing vault | `POST /api/invitations` | 404 | `Vault not found.` | **B — text/plain** |
| Missing vault | `POST /api/vaults/999999/share` | 404 | `Vault not found.` | **B — text/plain** |
| Missing user | `POST /api/vaults/{id}/share` | 404 | `Target user not found.` | **B — text/plain** |
| Duplicate/cross-user name | `POST /api/tags` | **500** | `DbUpdateException …` | **BUG — should be 409** |

## Findings by severity

### 🔴 High — wrong status code / leaks internals

1. **Duplicate tag → 500 `DbUpdateException`** (`TagsController.CreateTag`, `Models/...`/`Data/ApplicationDbContext.cs:154`).
   - Root cause: the unique index is **global on `Name`** (`HasIndex(t => t.Name).IsUnique()`), but the controller's duplicate pre-check is **per-user** (`t.UserId == me && t.Name == …`). A name another user already has slips past the pre-check and violates the DB constraint → raw 500 with EF internals leaked in `detail`.
   - Expected: **409 Conflict** with a clear message. (Also a latent data-model bug: tags are documented as user-scoped, so the index should be `(UserId, Name)`.)

2. **`GoogleLogin` leaks exception messages** (`AuthController.GoogleLogin`, ~lines 218-225): `return BadRequest($"Invalid Google Token: {ex.Message}")` / `BadRequest($"Google Login Failed: {ex.Message}")`. Returns raw exception text to the client (information disclosure) and uses 400 for what is an auth failure (401).

3. **`int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!)`** in `InvitationsController` (CreateInvitation/AcceptInvitation/RevokeInvitation) and `user!.Email!` null-forgiving derefs. If the claim is absent/malformed these throw → **500** instead of **401**. Other controllers use the safe `TryGetCurrentUserId(out …) → Unauthorized()` pattern; Invitations should too.

### 🟠 Medium — inconsistent shape / missing message

4. **Plain-text 404/400 bodies** (`text/plain`) instead of problem+json:
   - `VaultSharesController`: `NotFound("Vault not found.")`, `NotFound("Target user not found.")`, `NotFound("Share not found.")`.
   - `InvitationsController`: `NotFound("Vault not found.")`, `NotFound("Invitation not found.")`, `BadRequest("Invalid role.")`, `BadRequest("This invitation is for a different email address.")`.
   - `CredentialsController`: `NotFound("Credential not found.")`, `NotFound("Tag not found.")`, `BadRequest("Tag does not belong to the current user.")`, etc.
   - `UserController`: `NotFound(result.Error ?? "User not found.")`, `BadRequest(result.Error ?? "Update failed.")`.
   - `AuthController`: `BadRequest(result.Error)`, `Unauthorized("Invalid credentials.")`, `Unauthorized("Email not confirmed.")`, `StatusCode(423, "…")`.

   And a *fifth* shape — `AuditController` wraps messages in an anonymous `{ "error": "…" }` object (`BadRequest(new { error = result.Error })`, `NotFound(new { error = result.Error })`), unlike every other controller.

5. **Empty-body 404s with no `detail`** (shape A, no message): `VaultsController.GetVault/UpdateVault/DeleteVault` and `TagsController` use bare `NotFound()` / `Forbid()` — client gets a status code but no explanation of *what* was not found.

6. **`401`/`403` have no body at all** (`Unauthorized()`, `Forbid()`) across `VaultsController`, `TagsController`, `CredentialsController`, `VaultSharesController` — no problem+json, no message.

### 🟡 Low — quality / consistency

7. **Fragile string-matching on business errors**: `VaultsController` branches on `result.Error == "Vault not found."` / `"Only the vault owner can update the vault."` to map to 404/403. A typo in the manager message silently turns a 404 into a 400. Prefer typed results / error codes.

8. **Duplicated validation messages**: `POST /api/vaults` returns three messages for `Name` (DataAnnotations `[Required]` + two FluentValidation rules). Harmless but noisy.

9. **Dead/redundant `if (!ModelState.IsValid)` checks**: with `[ApiController]`, invalid models are auto-rejected before the action runs, so the manual checks (e.g. `InvitationsController.CreateInvitation` → `BadRequest(ModelState)`) never execute. Cosmetic.

## Recommended target contract (for #163)

- **One shape everywhere**: the existing `PassManAPI/DTOs/ErrorResponse.cs` (RFC7807, camelCase, `traceId`) already has factory methods `BadRequest/Unauthorized/Forbidden/NotFound/Conflict/InternalServerError`. Use it for *all* error returns; configure `[ApiController]` `ClientErrorMapping`/`InvalidModelStateResponseFactory` so framework-generated 400/401/403/404 also use it.
- **Status codes**: duplicate/constraint → **409**; missing auth claim → **401**; Google auth failure → **401** (no `ex.Message`).
- **Always include a `detail` message** so 404/403/401 explain what happened, without leaking exception internals.
- Add tests asserting status code **and** body shape for representative bad-input / unauthorized / not-found / conflict cases.

## Endpoint inventory covered

`AuthController`, `VaultsController`, `CredentialsController`, `TagsController`, `InvitationsController`, `VaultSharesController`, `AuditController`, `UserController`. (`OrmTest` is a dev-only probe controller, excluded.)
