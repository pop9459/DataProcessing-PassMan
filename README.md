# Password Manager API

A secure password management system built as a RESTful API with ASP.NET Core.

This repository contains the source code for the Password Manager API, a project focused on providing a secure and reliable way to manage credentials with role-based access control and vault sharing.

## 🚀 Quick Start

### Prerequisites
- Docker & Docker Compose

### Docker Compose

```bash
git clone https://github.com/pop9459/DataProcessing-PassMan
cd DataProcessing-PassMan
docker compose up -d
```

No `.env` file needed — all defaults are built in.

- **API**: http://localhost:5246/
- **Swagger**: http://localhost:5246/swagger
- **GUI**: http://localhost:5247/ *(Blazor frontend — incomplete, not part of the graded rubric)*

### Demo accounts (seeded on first run)

| Email | Password | Role |
|-------|----------|------|
| admin@passman.test | Admin123! | Admin |
| auditor@passman.test | Audit123! | SecurityAuditor |
| owner@passman.test | Owner123! | VaultOwner |
| reader@passman.test | Reader123! | VaultReader |

Log in via `POST /api/auth/login` or the Swagger UI to get a JWT, then use it to explore any endpoint.

### Google OAuth (not in scope)

The `POST /api/auth/google` endpoint exists in the codebase but is **out of scope** for this submission. Enabling it requires registering and maintaining an active Google Cloud OAuth project, which we do not provide. All graded functionality uses email/password login — no Google credentials are needed at any point.

### Running locally (not recommended)

**Docker is the only supported and guaranteed path.** `docker compose up -d` gives you a fully working API against a real MySQL instance with no other dependencies.

Running the API with the dotnet CLI is possible but not recommended:
- Requires exactly .NET 10.0 SDK installed
- Requires a running MySQL instance configured separately
- The Blazor GUI (`PassManGUI`) is incomplete and does not work reliably
- Google OAuth does not work locally without a configured Google Cloud project

If you need to use the dotnet CLI for the API only:

```bash
dotnet run --project PassManAPI
```

Do not bother running `PassManGUI` — it is out of scope and non-functional.

## 🗄️ Database Access

### Using MySQL Workbench

1. Install [MySQL Workbench](https://dev.mysql.com/downloads/workbench/)
2. Open database menu: <kbd>Ctrl</kbd>+<kbd>J</kbd>
3. Enter connection details:
   - Host: `localhost`
   - Port: `3306`
   - User: `root`
   - Password: `hihi`

## 🔐 Authentication & Authorization

### JWT Authentication
- **Token-based**: JWT Bearer tokens for API authentication
- **Stateless tokens**: no server-side session state; each request is self-contained
- **Google OAuth**: endpoint exists (`POST /api/auth/google`) but is out of scope — requires a Google Cloud project we don't maintain

📖 See **[docs/JWT.md](docs/JWT.md)** — where tokens are issued, the claims they carry, how to use the Swagger **Authorize** button, and configuration.

### Role-Based Permissions
The API seeds role-based permissions into MySQL on startup (see [`PassManAPI/Data/DbSeeder.cs`](PassManAPI/Data/DbSeeder.cs)):

- **Admin**: Full system access (all permissions)
- **SecurityAuditor**: `audit.read`, `vault.read`, `credential.read`, `system.health`
- **VaultOwner**: Manage own vaults/credentials (`vault.*`, `credential.*`, `tag.*`)
- **VaultReader**: Read-only access (`vault.read`, `credential.read`, `tag.read`)

Permissions are stored as Identity role claims with claim type `permission`. Update [`PassManAPI/Models/Permissions.cs`](PassManAPI/Models/Permissions.cs) to add new permissions.


## 🏗️ Architecture

### Technology Stack
- **Backend**: ASP.NET Core 10 Web API
- **Database**: MySQL with Entity Framework Core
- **Authentication**: JWT Bearer
- **Testing**: xUnit with FluentAssertions
- **API Docs**: Swagger/OpenAPI
- **Frontend**: Blazor Server *(incomplete, out of scope for grading)*

### Database Features
- **Constraints**: PK/FK, unique indexes, cascading deletes
- **View**: `vwUserVaultAccess` for vault access queries
- **Stored Procedures**:
  - `sp_AddVaultShare`: Validated vault sharing
  - `sp_LogAudit`: Centralized audit logging
- **Triggers**: `trg_Credentials_SetUpdatedAt` for automatic timestamp updates
- **Isolation**: `READ COMMITTED` for high-concurrency operations

### Project Structure

```
PassManAPI/
├── Controllers/       # API endpoints
├── Models/            # Domain entities
├── Managers/          # Business logic layer
├── Data/              # EF Core context, migrations, seeder, DB artifacts
├── Services/          # JWT, encryption, TOTP, breach-check (registered, not yet controller-wired)
├── Validators/        # FluentValidation validators
├── DTOs/              # Request/response models
└── Middleware/        # Exception handler, auth error body

PassManAPI.Tests/      # xUnit integration tests (123 passing, SQLite in-memory)

PassManGUI/            # Blazor frontend — incomplete, not part of the graded rubric
├── Components/        # Blazor pages and layout
└── Services/          # API client services

docs/                  # All project documentation
scripts/               # Postman collection + DB verification SQL
diagrams/              # Architecture and ER diagrams
```

## 📝 API Endpoints

### Authentication
- `POST /api/auth/register` - Register new user
- `POST /api/auth/login` - Login, returns JWT
- `POST /api/auth/google` - Google OAuth login *(out of scope — requires active Google Cloud project)*
- `GET /api/auth/me` - Get current user profile
- `PUT /api/auth/me` - Update current user profile
- `DELETE /api/auth/me` - Delete account
- `GET /api/auth/permissions` - List permissions for current user
- `POST /api/auth/assign-role` - Assign role to user (Admin only)

### Vaults
- `GET /api/vaults` - List user's vaults
- `POST /api/vaults` - Create vault
- `GET /api/vaults/{id}` - Get vault details
- `PUT /api/vaults/{id}` - Update vault
- `DELETE /api/vaults/{id}` - Soft-delete vault
- `GET /api/vaults/my-access` - List all vaults the user can access (owned + shared)

### Credentials
- `GET /api/vaults/{vaultId}/credentials` - List credentials in a vault
- `POST /api/vaults/{vaultId}/credentials` - Create credential
- `GET /api/credentials/{id}` - Get credential metadata
- `GET /api/credentials/{id}/password` - Get decrypted password
- `PUT /api/credentials/{id}` - Update credential metadata
- `PUT /api/credentials/{id}/password` - Update credential password
- `DELETE /api/credentials/{id}` - Delete credential
- `GET /api/credentials/{id}/tags` - List tags on a credential
- `PUT /api/credentials/{id}/tags` - Replace all tags on a credential
- `POST /api/credentials/{id}/tags/{tagId}` - Add a tag to a credential
- `DELETE /api/credentials/{id}/tags/{tagId}` - Remove a tag from a credential

### Vault Sharing & Invitations
- `POST /api/vaults/{vaultId}/share` - Share vault with a user
- `DELETE /api/vaults/{vaultId}/share/{userId}` - Revoke a user's share
- `POST /api/invitations` - Create invitation
- `GET /api/invitations` - List pending invitations
- `POST /api/invitations/{token}/accept` - Accept invitation by token
- `DELETE /api/invitations/{id}` - Revoke invitation

### Users (Admin)
- `GET /api/user` - List all users (Admin only)
- `GET /api/user/{id}` - Get user by ID
- `PUT /api/user/{id}` - Update user profile
- `DELETE /api/user/{id}` - Delete user (Admin only)
- `GET /api/user/{id}/vaults` - List a user's vaults (Admin only)
- `GET /api/user/{id}/tags` - List a user's tags (Admin only)

### Tags
- `GET /api/tags` - List all tags
- `GET /api/tags/{id}` - Get tag by ID
- `POST /api/tags` - Create tag
- `PUT /api/tags/{id}` - Update tag
- `DELETE /api/tags/{id}` - Delete tag

### Audit
- `GET /api/audit/logs` - Paginated audit log (own entries)
- `GET /api/audit/logs/{id}` - Get single audit entry
- `GET /api/audit/logs/vault/{vaultId}` - Audit log for a vault
- `GET /api/audit/logs/all` - All entries across all users (Admin only)

### System
- `GET /health` - Liveness probe (no auth required)

## 🧪 Testing

123 xUnit integration tests (SQLite in-memory) and a Newman collection (full stack, MySQL). **Both run in Docker — this is the only supported path.** See **[docs/TESTING.md](docs/TESTING.md)** for details.

```bash
# Integration tests
docker compose run --rm test

# E2E tests (Newman)
docker compose --profile e2e up --abort-on-container-exit --scale passman-gui=0
```

> **Postman desktop is unreliable** — see [docs/TESTING.md](docs/TESTING.md) for why. Use Newman (above) instead.

## 📚 Documentation

| File | Covers |
|------|--------|
| [docs/TESTING.md](docs/TESTING.md) | Running xUnit + Newman tests, test class breakdown |
| [docs/BACKUP_RECOVERY.md](docs/BACKUP_RECOVERY.md) | Backup strategy, PITR, recovery playbook, downtime prevention |
| [docs/VALIDATION.md](docs/VALIDATION.md) | All input validation rules (DataAnnotations + FluentValidation) |
| [docs/XML.md](docs/XML.md) | JSON + XML content negotiation on every endpoint |
| [docs/JWT.md](docs/JWT.md) | Token issuance, claims, Swagger auth, configuration |
| [docs/ROLES.md](docs/ROLES.md) | Roles, permission claims, vault share permission levels |
| [docs/DATABASE.md](docs/DATABASE.md) | DB account provisioning, least-privilege setup |
| [docs/TRANSACTIONS.md](docs/TRANSACTIONS.md) | Isolation level choice and justification |
| [docs/SystemArchitecture.md](docs/SystemArchitecture.md) | Component overview, DB artifacts (view, SPs, triggers) |
| [diagrams/](diagrams/) | ER diagram, class diagram, architecture diagram |
| **Swagger UI** | http://localhost:5246/swagger |

## 🎯 Features

- JWT authentication with role-based access control
- Vault and credential CRUD with AES-256-GCM encryption (per-credential key wrapped under server master key)
- Vault sharing via invitations with View / Edit / Admin permission levels
- Tag-based credential organization
- Attachment model with cascade delete (upload endpoints not yet wired)
- Full audit log with pagination and filtering
- JSON and XML on every endpoint
- 123 xUnit integration tests + Newman E2E suite

## 🛠️ Development

### Adding New Permissions
1. Update [`PassManAPI/Models/Permissions.cs`](PassManAPI/Models/Permissions.cs)
2. Restart API - seeder will attach to roles automatically

### Database Migrations
```bash
# Add migration
dotnet ef migrations add MigrationName --project PassManAPI

# Apply migration
dotnet ef database update --project PassManAPI
```

### Code Quality
- All code follows C# naming conventions
- Integration tests required for new endpoints
- Swagger documentation auto-generated

## 📄 License

This project is for educational purposes as part of a university course.

## 👥 Contributors

Data Processing course team @ University
