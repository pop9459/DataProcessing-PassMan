**PassManAPI Postman collection — quick guide**

Files added:

Quick steps (local, Docker recommended):

1. Start the API (Docker recommended):

```bash
docker compose up --build -d
```

2. Open Postman and import the collection:
 File → Import → Choose `docs/Postman-Collection-PassManAPI-full.postman_collection.json`.

3. Set environment (optional):
   - Create an environment or use Globals. Ensure `baseUrl` matches your API (default `http://localhost:5246`).
      - Or import `docs/Postman-PassManAPI-Env.postman_environment.json` and select it. Default `baseUrl` is `http://localhost:5246` (Docker-local).

4. Run the requests in order (the collection is arranged to be usable sequentially):
   - `Auth - Register` — creates a user and (in Postman test script) saves `userId` and `accessToken` to the environment.
   - `Auth - Login` — alternate path if you want to log in an existing user (sets `accessToken` and `userId`).
   - `Auth - Me (JWT)` — verifies JWT-authenticated profile.
   - `Vaults - List (no auth)` — sanity check (should return 401).
   - `Vaults - Create (X-UserId)` — creates a vault using `X-UserId` header (Postman script attempts to save `vaultId`).
   - `Vaults - List (auth via X-UserId)` — lists vaults for the seeded user.
   - `Credentials - Create` — creates a credential inside `vaultId` (stores `credentialId`).
   - `Credential - Get Password` — attempts to retrieve the credential's decrypted password.
 - `docs/Postman-Collection-PassManAPI-full.postman_collection.json` — full coverage collection (positive + negative tests).

Notes and tips:
  - Dev header auth: set `X-UserId` to the numeric user id (this collection uses that for vault/credential flows).
  - JWT auth: the register/login endpoints return `accessToken`; requests that use JWT set the `Authorization: Bearer {{accessToken}}` header.

Automated run (Newman)

```bash
newman run docs/Postman-Collection-PassManAPI-full.postman_collection.json --env-var "baseUrl=http://localhost:5246"
```

If you want, I can:

Which would you like next?
