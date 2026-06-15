# Input Validation

This document inventories every validation rule in the API, where it runs, and how violations are
reported. It also reconciles the two request-validation mechanisms in use (DataAnnotations and
FluentValidation).

## Where validation happens

| Layer | Mechanism | Applies to | On failure |
|-------|-----------|------------|------------|
| 1. Request models | **DataAnnotations** (`[Required]`, `[EmailAddress]`, …) | Every request DTO | `400` with field errors |
| 2. Request models | **FluentValidation** auto-validation | Auth, Vault, Credential requests | `400` with field errors |
| 3. Identity | `IdentityOptions` (`Program.cs`) | Password policy, lockout, email uniqueness/confirmation | `400`/`401`/`423` |
| 4. Domain | Invariants in entity methods (throw) | `Tag`, `Invitation`, `Attachment`, … | `400` (never `500` — see [Domain invariants](#domain-invariants-model-layer)) |

Layers 1–2 run during model binding (`[ApiController]`). Field errors are collected into `ModelState`
and returned as the standardized `ErrorResponse` via the `InvalidModelStateResponseFactory` in
`Program.cs`, content-negotiated as JSON **or** XML (see [docs/XML.md](XML.md)).

`ErrorResponse` follows the [RFC 7807](https://datatracker.ietf.org/doc/html/rfc7807) Problem Details
schema — `type` (a URI), `title`, `status`, `detail` — plus two extension members that RFC 7807
explicitly permits: `traceId`, and `errors` (a field → messages map, present only on validation
failures). A validation `400` therefore looks like:

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "detail": null,
  "traceId": "00-…-00",
  "errors": { "Email": ["A valid email address is required."] }
}
```

> **Media types:** pipeline-level errors (global exception handler, 401/403) are served as
> `application/problem+json`; controller/validation errors are content-negotiated as
> `application/json` or `application/xml`. `detail` carries the human-readable message for
> non-validation errors; validation errors put the per-field detail in `errors`.

## Request validation rules

Legend — **DA** = DataAnnotations, **FV** = FluentValidation.

### Auth (`/api/auth/*`)

| Request | Field | Rules | Source |
|---------|-------|-------|--------|
| `RegisterRequest` | Email | required, email format, ≤256 | DA + FV |
| | Password | required, 8–100, upper+lower+digit+non-alnum | DA (`[PasswordComplexity]`) + FV |
| | ConfirmPassword | required, equals Password | DA (`[Compare]`) + FV |
| | UserName | ≤256, `[A-Za-z0-9-_.]` only (when present) | DA + FV |
| | PhoneNumber | phone format (when present) | DA (`[Phone]`) + FV |
| `LoginRequest` | Email | required, email, ≤256 | DA + FV |
| | Password | required | DA + FV |
| `UpdateProfileRequest` | Email/UserName/Phone | same as register, all optional | DA + FV |
| `GoogleLoginRequest` | IdToken | required | DA |
| `AssignRoleRequest` | RoleName | must exist (checked in controller → 400/404) | controller |

### Vaults (`/api/vaults`)

| Request | Field | Rules | Source |
|---------|-------|-------|--------|
| `CreateVaultRequest` | Name | required, ≤100, `[A-Za-z0-9 \-_]` only | DA + FV |
| | Description | ≤500 (when present) | DA + FV |
| | Icon | ≤50 | DA |
| | UserId | required, > 0 (and must equal caller) | DA + FV |
| `UpdateVaultRequest` | Name / Description / Icon | as above | DA + FV |

### Credentials (`/api/vaults/{id}/credentials`, `/api/credentials/*`)

| Request | Field | Rules | Source |
|---------|-------|-------|--------|
| `CreateCredentialRequest` | Title | required, ≤255 | DA + FV |
| | Username | ≤255 (when present) | DA + FV |
| | EncryptedPassword | required | DA + FV |
| | Url | ≤500, must be absolute http/https (when present) | DA (`[Url]`) + FV (`Must`) |
| | CategoryId | > 0 (when present) | FV |
| `UpdateCredentialRequest` | Title / Username / Url / CategoryId | as above | DA + FV |
| `UpdateCredentialPasswordRequest` | EncryptedPassword | required | DA |
| `AssignTagsRequest` | TagIds | required; each id must belong to the caller (checked in controller → 400) | DA + controller |

### Tags, Invitations, Sharing

| Request | Field | Rules | Source |
|---------|-------|-------|--------|
| `CreateTagRequest` / `UpdateTagRequest` | Name | required, ≤100 | DA |
| `CreateInvitationRequest` | VaultId | required | DA |
| | Email | required, email format | DA |
| | Role | required, one of `View`/`Edit`/`Admin` | DA (`[RegularExpression]`) |
| `ShareRequest` (VaultShares) | UserEmail | required, email format | DA |

## Password policy (enforced in three places — kept consistent)

Minimum 8 chars, with at least one uppercase, one lowercase, one digit and one non-alphanumeric.

| Where | How |
|-------|-----|
| `RegisterRequest.Password` | `[PasswordComplexity]` attribute (`Validation/PasswordComplexityAttribute.cs`) |
| `RegisterRequestValidator` | FluentValidation `Matches(...)` rules with per-rule messages |
| ASP.NET Identity | `IdentityOptions.Password` in `Program.cs` |

## Identity-level rules (`Program.cs`)

| Rule | Value |
|------|-------|
| Lockout | 5 failed logins → locked 15 min (returns `423`) |
| Unique email | enforced (`RequireUniqueEmail`) |
| Confirmed email | login requires a confirmed email |

## Domain invariants (model layer)

Beyond request validation, entities guard their own invariants and throw on violation. **These always
resolve to `400`, never `500`** — by three mechanisms in order:

1. **Pre-empted by DTO validation (layers 1–2).** Bad input is rejected before the entity is
   constructed — e.g. a blank/whitespace tag name fails `[Required]` (which trims internally) and
   returns a `400` validation error first. (Verified live: `PUT /api/tags/{id}` with `"   "` → `400`.)
2. **Caught in the controller.** Reachable domain checks are caught and returned as `400` — e.g.
   `InvitationsController.AcceptInvitation` catches `Invitation.MarkAccepted()`'s
   `InvalidOperationException` and returns `BadRequestProblem`.
3. **Mapped by the global handler.** Anything that still reaches `ExceptionHandlerMiddleware` is
   mapped to `400`: `ArgumentException` and `InvalidOperationException` (the only types these
   invariants throw) → `BadRequest`. Only genuinely unexpected exceptions fall through to `500`.

The guarded invariants:

- **`Tag`** — `Rename()` rejects an empty/whitespace name.
- **`Invitation`** — invited email required + valid format; token required, ≤100; expiry must be in
  the future; `MarkAccepted()` / role changes rejected once accepted or expired.
- **`Attachment`** — file name/path required and length-bounded; encryption key required.

## Success response codes

This page documents the **validation (failure)** path. Success codes for every endpoint —
`200 OK`, `201 Created`, `204 No Content` — are declared per action with
`[ProducesResponseType(...)]` and rendered in **Swagger** (`/swagger`), and are asserted end-to-end by
the Postman/Newman collection (see [TESTING_GUIDE.md](../TESTING_GUIDE.md)).

## Reconciling FluentValidation vs DataAnnotations

**Current state:** request types are validated by DataAnnotations, and the Auth/Vault/Credential
requests *also* have FluentValidation validators (registered via `AddFluentValidationAutoValidation`
+ `AddValidatorsFromAssemblyContaining<RegisterRequestValidator>`). Both run during model binding and
both feed `ModelState`, so a field can produce overlapping messages (e.g. DataAnnotation
*"The Email field is required."* and FluentValidation *"Email is required."*).

**Guidance / cleanup opportunities (not blocking):**
- Treat **FluentValidation as the source of truth** for the types that have a validator (it carries the
  richer rules and clearer messages); keep DataAnnotations only for the request types without a
  validator (Tags, Invitations, Sharing, Google login).
- The credential request types are currently declared **twice** — as nested classes in
  `CredentialsController` *and* in `DTOs/CredentialDto.cs`. The controller binds its nested versions;
  the DTO copies are unused. Consolidate to one definition.
- Password complexity lives in three places (above). They are consistent today; if the policy changes,
  update all three (the attribute, the FV rules, and `IdentityOptions`).

All validation failures, regardless of layer, return the same `ErrorResponse` envelope in JSON or XML.
