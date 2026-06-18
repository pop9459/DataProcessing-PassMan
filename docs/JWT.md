# JWT Authentication

This API authenticates requests with **JWT bearer tokens**. This document explains where the
token comes from, what's inside it, how to send it, and how it's configured — so it's easy to find.

## Where the token is issued

JWTs are created by **`PassManAPI/Services/JwtTokenService.cs`** (`IJwtTokenService.CreateAccessTokenAsync`)
and returned by the auth endpoints in **`PassManAPI/Controllers/AuthController.cs`**:

| Endpoint | Returns |
|---|---|
| `POST /api/auth/register` | `201` + `{ "accessToken": "<JWT>", "user": { … } }` |
| `POST /api/auth/login` | `200` + `{ "accessToken": "<JWT>", "user": { … } }` |
| `POST /api/auth/google` | `200` + `{ "accessToken": "<JWT>", "user": { … } }` (Google OAuth id-token exchange — **out of scope**: requires an active Google Cloud project) |

> There is **one** token service — `JwtTokenService`. (A second, unused `TokenService`/`AuthManager`
> stack used to exist and was never wired to any endpoint; it was removed to avoid confusion.)

## What's inside the token

Signed with **HMAC-SHA256**. Claims:

- `sub` / `nameidentifier` — the user id
- `email`, `name`
- `jti` — unique token id
- `role` — the user's role(s)
- `permission` — one claim per permission granted by the user's role (e.g. `vault.read`, `credential.create`)

The `permission` claims are what the authorization policies check (`RequireClaim("permission", …)`).
Without them every permission-protected endpoint returns **403** — see #159.

## How to send it

Add an `Authorization` header to every protected request:

```
Authorization: Bearer <accessToken>
```

### In Swagger UI
1. Run the API (`docker compose up -d db passman-api`) and open `http://localhost:5246/swagger`.
2. `POST /api/auth/login` with your email/password and copy the `accessToken` from the response.
3. Click the **Authorize 🔓** button (top-right), paste the token (no `Bearer ` prefix — Swagger adds it), and **Authorize**.
4. All "🔒" endpoints now send your token automatically.

The Authorize button is configured in `Program.cs` via `AddSecurityDefinition("Bearer", …)`.

### With curl
```bash
TOKEN=$(curl -s -X POST http://localhost:5246/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"you@example.com","password":"YourPassw0rd!"}' | jq -r .accessToken)

curl http://localhost:5246/api/vaults -H "Authorization: Bearer $TOKEN"
```

## How it's validated

`Program.cs` registers JWT bearer authentication (`AddJwtBearer`) and validates issuer, audience,
lifetime and signing key against the `Jwt` configuration section.

## Configuration (`appsettings.json` → `"Jwt"`)

| Key | Meaning |
|---|---|
| `Issuer` | Token issuer (`iss`), validated on every request |
| `Audience` | Token audience (`aud`), validated on every request |
| `SigningKey` | Symmetric HMAC-SHA256 secret. **Override in production** (env var `Jwt__SigningKey`). |
| `AccessTokenLifetimeMinutes` | Access-token lifetime (default 60) |

In Docker the signing key is supplied via the `JWT_SIGNING_KEY` env var (see `docker-compose.yml`).
