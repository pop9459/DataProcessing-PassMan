# Database Account & Access

The API connects to MySQL with a **dedicated, least-privilege account** (`passman_app`) rather than
`root`. The account is scoped to the application's own database and has no server-wide or
administrative rights.

> This is the **database login** the API authenticates with — distinct from the application's
> in-app RBAC (Admin / VaultOwner / etc.), which is documented separately (see #166).

## The account

| | |
|---|---|
| User | `passman_app` (host `%`) |
| Password | `DB_APP_PASSWORD` env var, default `passman_app_dev_pw` (dev only) |
| Grants | `GRANT ALL PRIVILEGES ON passManDB.*` — and nothing else |
| Cannot | access other databases, read the `mysql` system schema, or `GRANT` to others |

`root` is kept only for DB administration and the container healthcheck; the application never uses it.

Verify on a running stack:

```bash
docker compose exec db mysql -upassman_app -ppassman_app_dev_pw -e "SELECT CURRENT_USER(); SHOW GRANTS;"
# CURRENT_USER() -> passman_app@%
# GRANT USAGE ON *.* ...
# GRANT ALL PRIVILEGES ON `passManDB`.* ...
```

## How it's provisioned (Docker)

The MySQL image creates the user automatically on **first initialization** from the `db` service env
in `docker-compose.yml`:

```yaml
environment:
  - MYSQL_DATABASE=passManDB
  - MYSQL_USER=passman_app
  - MYSQL_PASSWORD=${DB_APP_PASSWORD:-passman_app_dev_pw}
```

The image grants `MYSQL_USER` full privileges on `MYSQL_DATABASE` only. Because this runs only when the
data volume is empty, recreate it for a clean account:

```bash
docker compose down -v && docker compose up -d db passman-api
```

### Why `--log-bin-trust-function-creators=1`

MySQL 8.0 has binary logging on by default, which normally requires `SUPER` to create triggers or
stored routines. The app creates a trigger and two stored procedures at startup
(`Data/DatabaseArtifacts.cs`), so the `db` service sets `--log-bin-trust-function-creators=1`,
letting the non-`SUPER` `passman_app` account create them. (Without this, the API would fail to start
as a non-root user.)

## Provisioning on an external / existing MySQL

For a database not created by this compose file (or an existing volume), create the account manually:

```sql
CREATE USER 'passman_app'@'%' IDENTIFIED BY 'a-strong-password';
GRANT ALL PRIVILEGES ON passManDB.* TO 'passman_app'@'%';
FLUSH PRIVILEGES;
-- if binary logging is enabled and the app account creates the triggers/procedures:
SET GLOBAL log_bin_trust_function_creators = 1;
```

Then point the connection string at it via `ConnectionStrings__DefaultConnection`
(or the `DB_APP_PASSWORD` env var for the compose default).

## Notes & future hardening

- The dev password is committed for zero-config local runs (mirroring the existing root/`hihi`
  convention). For any real deployment, set `DB_APP_PASSWORD` from a secret store and rotate it.
- The app self-migrates and creates DB artifacts at startup, so it needs DDL on its own database;
  `ALL PRIVILEGES ON passManDB.*` is the appropriate scope. If startup migrations/artifacts are ever
  moved to a separate provisioning step, the runtime account could be narrowed to DML + `EXECUTE`.
