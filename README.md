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
│   ├── AuthController.cs
│   ├── VaultsController.cs
│   ├── CredentialsController.cs
│   ├── InvitationsController.cs
│   ├── TagsController.cs
│   ├── AuditController.cs
│   └── ...
├── Models/           # Domain entities
│   ├── User.cs
│   ├── Vault.cs
│   ├── Credential.cs
│   ├── SubscriptionTier.cs
│   ├── Attachment.cs
│   ├── Invitation.cs
│   └── ...
├── Managers/         # Business logic
├── Data/            # EF Core context and migrations
├── Services/        # JWT, email, etc.
└── DTOs/            # Request/response models

PassManGUI/
├── Components/
│   ├── Pages/       # Blazor pages
│   └── Layout/      # Layout components
└── Services/        # API client services

PassManAPI.Tests/    # Integration tests
└── *.cs            # 106 passing tests
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

### Test Coverage
- **106 passing tests** with 0 failures
- Integration tests using in-memory SQLite
- No MySQL dependency for tests
- Dev header authentication (`X-UserId`)

### Running Tests

**Docker (Recommended):**
See `TESTING_GUIDE.md` for the recommended Docker-first test workflow.

```bash
docker compose up --build --abort-on-container-exit --exit-code-from test test
```

**Local:**
```bash
# Clean first
rm -rf PassManAPI/obj PassManAPI/bin PassManAPI.Tests/obj PassManAPI.Tests/bin

# Run tests
dotnet test PassManAPI.Tests/PassManAPI.Tests.csproj
```

**Watch Mode:**
```bash
docker compose run --rm test dotnet watch test PassManAPI.Tests/PassManAPI.Tests.csproj
```

**Specific Tests:**
```bash
dotnet test --filter "FullyQualifiedName~AuthEndpointsTests"
```

### Test Categories
- **AuthEndpointsTests**: Registration, login, JWT validation
- **SubscriptionTierTests**: Tier seeding and assignment
- **VaultEndpointsTests**: CRUD operations and permissions
- **CredentialsEndpointsTests**: Credential management
- **InvitationTests**: Vault sharing workflows
- **AttachmentModelTests**: File attachment handling
- **AuthorizationPolicyTests**: Role-based access control

## 📚 Documentation

- **[API_INTEGRATION_SUMMARY.md](/API_INTEGRATION_SUMMARY.md)** - Frontend-backend integration
- **[TESTING_GUIDE.md](/TESTING_GUIDE.md)** - Testing strategies and commands
- **[BACKUP_RECOVERY.md](/BACKUP_RECOVERY.md)** - Database backup procedures
- **[ProjectSummary.md](/ProjectSummary.md)** - Feature breakdown and roadmap
- **[GOOGLE_AUTH_DOCS.md](/GOOGLE_AUTH_DOCS.md)** - OAuth setup guide
- **Swagger UI**: http://localhost:5246/swagger

## 🎯 Current Features

### ✅ Implemented
- User authentication (JWT + Google OAuth)
- Vault management with CRUD operations
- Credential storage with encryption
- Tag-based organization
- **Subscription tiers** (Free/Premium)
- **Vault sharing via invitations**
- **File attachments** for credentials
- Role-based access control
- Audit logging
- Blazor frontend with real-time updates

### 🚧 In Progress
- Password strength analysis
- Enhanced search and filtering
- Password generator
- Breach monitoring integration

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
