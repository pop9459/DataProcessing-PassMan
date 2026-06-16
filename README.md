# Password Manager API

A secure password management system built as a RESTful API with ASP.NET Core and Blazor frontend.

This repository contains the source code for the Password Manager API, a project focused on providing a secure and reliable way to manage credentials with role-based access control, vault sharing, and subscription tiers.

## 🚀 Quick Start

### Prerequisites
- Docker & Docker Compose (recommended)
- OR .NET SDK 10.0+ for local development

###Setting up Environment Variables (Required)

The application uses Google OAuth for authentication. Before running the app, configure your Google credentials:

1. Create a `.env` file in the repository root:
   ```bash
   cp .env.example .env
   ```

2. Edit the `.env` file and add your Google OAuth credentials:
   ```
   GOOGLE_CLIENT_ID=your-google-client-id
   GOOGLE_CLIENT_SECRET=your-google-client-secret
   ```

**Note:** The `.env` file is gitignored and never committed. Each team member needs their own `.env` file.

### Docker Compose (Recommended)

```bash
# Clone and navigate
git clone https://github.com/pop9459/DataProcessing-PassMan
cd DataProcessing-PassMan

# Set up environment variables (see above)

# Start all services
docker compose up -d
```

- **GUI**: http://localhost:5247/
- **API**: http://localhost:5246/
- **Swagger**: http://localhost:5246/swagger

### Local Development (dotnet CLI)

```bash
# Install .NET SDK (10.0 or later)

# Clone and navigate
git clone https://github.com/pop9459/DataProcessing-PassMan
cd DataProcessing-PassMan

# Set up environment variables (see "Setting up Environment Variables" above)

# Set user secrets for GUI
cd PassManGUI
dotnet user-secrets set "Authentication:Google:ClientId" "your-google-client-id"
dotnet user-secrets set "Authentication:Google:ClientSecret" "your-google-client-secret"
cd ..

# Run API
dotnet run --project PassManAPI

# In another terminal, run GUI
dotnet run --project PassManGUI
```

### Troubleshooting

If you encounter build errors:

```bash
dotnet restore
dotnet clean
```

Or manually delete `bin` and `obj` directories:

**Linux/macOS:**
```bash
rm -rf bin obj
```

**Windows (PowerShell):**
```powershell
Remove-Item -Recurse -Force bin, obj
```

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
- **Google OAuth**: Social login integration
- **Session Management**: Secure token storage and validation

📖 See **[docs/JWT.md](docs/JWT.md)** — where tokens are issued, the claims they carry, how to use the Swagger **Authorize** button, and configuration.

### Role-Based Permissions
The API seeds role-based permissions into MySQL on startup (see [`PassManAPI/Data/DbSeeder.cs`](PassManAPI/Data/DbSeeder.cs)):

- **Admin**: Full system access (all permissions)
- **SecurityAuditor**: `audit.read`, `vault.read`, `credential.read`, `system.health`
- **VaultOwner**: Manage own vaults/credentials (`vault.*`, `credential.*`)
- **VaultReader**: Read-only access (`vault.read`, `credential.read`)

Permissions are stored as Identity role claims with claim type `permission`. Update [`PassManAPI/Models/Permissions.cs`](PassManAPI/Models/Permissions.cs) to add new permissions.

### Subscription Tiers
- **Free Tier**: 3 vaults, 50 credentials per vault, 5MB attachments, no sharing
- **Premium Tier**: 50 vaults, 1000 credentials per vault, 100MB attachments, vault sharing enabled
- Default tier assigned on user registration

## 🏗️ Architecture

### Technology Stack
- **Backend**: ASP.NET Core 10 Web API
- **Frontend**: Blazor Server
- **Database**: MySQL with Entity Framework Core
- **Authentication**: JWT Bearer + Google OAuth
- **Testing**: xUnit with FluentAssertions
- **API Docs**: Swagger/OpenAPI

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
├── Services/          # JWT, encryption, TOTP, breach-check
├── Validators/        # FluentValidation validators
├── DTOs/              # Request/response models
└── Middleware/        # Exception handler, auth error body

PassManAPI.Tests/      # xUnit integration tests (123 passing, SQLite in-memory)

PassManGUI/
├── Components/        # Blazor pages and layout
└── Services/          # API client services

docs/                  # All project documentation
scripts/               # Postman collection + DB verification SQL
diagrams/              # Architecture and ER diagrams
```

## 📝 API Endpoints

### Authentication
- `POST /api/auth/register` - User registration
- `POST /api/auth/login` - Login with JWT
- `POST /api/auth/google-login` - Google OAuth login
- `GET /api/auth/me` - Get current user profile

### Vaults
- `GET /api/vaults` - List user's vaults
- `POST /api/vaults` - Create vault
- `GET /api/vaults/{id}` - Get vault details
- `PUT /api/vaults/{id}` - Update vault
- `DELETE /api/vaults/{id}` - Delete vault

### Credentials
- `GET /api/credentials?vaultId={id}` - List credentials
- `POST /api/credentials` - Create credential
- `GET /api/credentials/{id}` - Get credential
- `PUT /api/credentials/{id}` - Update credential
- `DELETE /api/credentials/{id}` - Delete credential

### Vault Sharing & Invitations
- `POST /api/vaults/{id}/share` - Share vault
- `GET /api/invitations` - List invitations
- `POST /api/invitations/{id}/accept` - Accept invitation
- `POST /api/invitations/{id}/revoke` - Revoke invitation

### Tags & Categories
- `GET /api/tags` - List tags
- `POST /api/tags` - Create tag
- `PUT /api/tags/{id}` - Update tag
- `DELETE /api/tags/{id}` - Delete tag

### Audit
- `GET /api/audit/logs` - View audit logs (admin only)

## 🧪 Testing

123 xUnit integration tests (SQLite in-memory) and a Newman collection (full stack, MySQL). Both run in Docker. See **[docs/TESTING.md](docs/TESTING.md)** for details.

```bash
# Integration tests
docker compose run --rm test

# E2E tests (Newman)
docker compose --profile e2e up --abort-on-container-exit --scale passman-gui=0
```

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

- JWT + Google OAuth authentication with role-based access control
- Vault and credential CRUD with AES-256-GCM encryption (per-credential key wrapped under server master key)
- Vault sharing via invitations with View / Edit / Admin permission levels
- Tag-based credential organization
- File attachments per credential
- Full audit log with pagination and filtering
- Breach-check integration (HaveIBeenPwned)
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
