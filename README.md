# Password Manager API

A secure password management system built as a RESTful API in C#.

This repository contains the source code for the Password Manager API, a project focused on providing a secure and reliable way to manage credentials.

## Running the app

### Setting up Environment Variables (Required for Development)

The application uses Google OAuth for authentication. Before running the app, you need to configure your Google credentials:

1. Create a `.env` file in the repository root:
    ```bash
    cp .env.example .env
    ```

2. Edit the `.env` file and add your Google OAuth credentials:
    ```
    GOOGLE_CLIENT_ID=your-google-client-id
    GOOGLE_CLIENT_SECRET=your-google-client-secret
    ```

**Note:** The `.env` file is gitignored and never committed to Git. Each team member needs to set up their own `.env` file.

### Docker compose (Recommended)
1. Clone the repository
    ```
    git clone https://github.com/pop9459/DataProcessing-PassMan
    ```
2. cd into the repository
    ```
    cd DataProcessing-PassMan
    ```
3. Set up environment variables (see above)
4. Run 
    ```
    docker compose up -d
    ```
5. Open http://localhost:5247/ (GUI) or http://localhost:5246/ (API)

### Local (dotnet CLI)
1. Install .NET SDK (9.0 or later).
2. Clone the repository
    ```
    git clone https://github.com/pop9459/DataProcessing-PassMan
    ```
3. cd into the repository
    ```
    cd DataProcessing-PassMan
    ```
4. Set up environment variables (see "Setting up Environment Variables" section above)
5. Install dotnet user secrets globally if not already installed:
    ```
    dotnet tool install -g dotnet-user-secrets
    ```
6. Set your local user secrets:
    ```
    cd PassManGUI
    dotnet user-secrets set "Authentication:Google:ClientId" "your-google-client-id"
    dotnet user-secrets set "Authentication:Google:ClientSecret" "your-google-client-secret"
    cd ..
    ```
7. Run the projects
    ```
    dotnet run --project PassManAPI
    ``` 
8. In another terminal, run:
    ```
    dotnet run --project PassManGUI
    ```
9. Open http://localhost:5247/ (GUI)

### Troubleshooting

If you encounter build errors, try running the following commands from the root of the repository.

First, restore dependencies and clean the project:
```
dotnet restore
```
```
dotnet clean
```

If issues persist, manually delete the `bin` and `obj` directories.

**On Linux / macOS:**
```bash
rm -rf bin obj
```

**On Windows (Command Prompt):**
```batch
rmdir /s /q bin obj
```

**On Windows (PowerShell):**
```powershell
Remove-Item -Recurse -Force bin, obj
```

## Accessing the database manually

1. Install [MySQL Workbench](https://dev.mysql.com/downloads/workbench/)

2. Open the database menu

<kbd>Ctrl</kbd>+<kbd>J</kbd>

3. Enter the details

-Host: localhost
-Port: 3306
-User: root
-Password: hihi         (intentional leak hihihiha)

## Authorization roles & permissions (API)

The API seeds role-based permissions into MySQL on startup (see `PassManAPI/Data/DbSeeder.cs`). Permissions are stored as Identity role claims with claim type `permission` and follow least-privilege defaults:

- Admin: full access (all permissions).
- SecurityAuditor: `audit.read`, `vault.read`, `credential.read`, `system.health`.
- VaultOwner: manage own vaults/credentials (`vault.read/create/update/delete/share`, `credential.read/create/update/delete`).
- VaultReader: read-only for vault metadata and credentials (`vault.read`, `credential.read`).

In development, demo users are created automatically with the roles above; in other environments only the roles/claims are ensured. Update `PassManAPI/Models/Permissions.cs` to add new permissions, and the seeder will attach them to roles on next startup.

## Database artifacts & isolation (value and justification)

- Constraints: PK/FK/unique and length/null constraints are defined in the EF model/migrations (e.g., unique Users.Email, composite key on VaultShares, cascading deletes where appropriate) to maintain referential integrity.
- View: `vwUserVaultAccess` lists vaults a user can access (owner or shared) to support least-privilege querying without exposing sensitive fields.
- Stored procedures:
  - `sp_AddVaultShare`: validated, idempotent share creation by email (guards missing vault/user and ignores duplicates).
  - `sp_LogAudit`: centralized insert into AuditLogs for privileged actions.
- Trigger: `trg_Credentials_SetUpdatedAt` maintains UpdatedAt on credential updates for auditability.
- Isolation: when running on MySQL, we set session isolation to `READ COMMITTED` during artifact setup to reduce phantom-read risk for high-churn operations while avoiding excessive locking; SQLite/test bypasses these artifacts.

## Project Structure

The project follows the standard ASP.NET Core Web API structure:

```
PassManAPI/
├── Components/             # Blazor components for UI
│   ├── Layout/             # Layout components (MainLayout, NavMenu)
│   └── Pages/              # Page components (Home, Login, Register, Vaults)
├── Controllers/            # Presentation layer (namespace: PassManAPI.Controllers)
│   ├── AuditController.cs
│   ├── AuthController.cs
│   ├── # API endpoint controllers
│   └── ...
├── Helpers/                # Helper/utility classes (namespace: PassManAPI.Helpers)
│   ├── SqlTest.cs
│   └── ...
├── Managers/               # Business layer (namespace: PassManAPI.Managers) 
│   ├── # Logic core classes
│   └── ...
├── Models/                 # Data access layers (namespace: PassManAPI.Models)
│   ├── # ORM definitions, DB connections...
│   └── ...
├── Properties/             # Project properties and launch settings
├── wwwroot/                # Static files (CSS, JS, images)
├── Program.cs              # Application entry point
├── appsettings.json        # Configuration settings
└── PassManAPI.csproj       # Project file
```

### Namespace Conventions
- **Controllers**: `PassManAPI.Controllers`
- **Models**: `PassManAPI.Models`
- **Helpers**: `PassManAPI.Helpers`
- **Components**: `PassManAPI.Components`

## Project Status

This board tracks the current development progress.

### In Progress

#### Documentation
- [x] Architecture Diagram
- [ ] Entity Relationship Diagram (ERD)
- [ ] Class Diagram

#### Infrastructure
- [x] Setup ASP.NET Core Web API project structure
- [x] Setup a Dockerfile for the API host
- [x] Setup Swagger/OpenAPI for API documentation

### Upcoming
- [ ] Configure PostgreSQL database with Entity Framework Core
- [ ] Implement basic user registration and JWT authentication

## API Documentation

To access the Swagger documentation for the API, run the application and navigate to `/swagger` in your browser.

- **URL**: `http://localhost:5246/swagger`

This will display the Swagger UI, which provides detailed information about the available endpoints, models, and allows you to interact with the API directly.

## Testing

Integration tests live in `PassManAPI.Tests` and run the API with a test `WebApplicationFactory` using in-memory SQLite (no MySQL needed).

### Running Tests in Docker (Recommended)

To avoid file permission conflicts between Docker and local builds, run tests inside a Docker container:

```bash
docker-compose run --rm test
```

**What this does:**
- Runs tests in an isolated container environment
- Automatically cleans up the container after tests complete (`--rm`)
- Prevents `obj/bin` permission issues when switching between Docker and local development
- Uses the same test configuration as CI/CD pipelines

**Additional options:**

```bash
# Run tests with watch mode (auto-rerun on file changes)
docker-compose run --rm test dotnet watch test PassManAPI.Tests/PassManAPI.Tests.csproj

# Run specific test class
docker-compose run --rm test dotnet test --filter "FullyQualifiedName~AuthEndpointsTests"

# Use the helper script
./scripts/test.fish docker        # Run tests in Docker
./scripts/test.fish watch         # Run with watch mode
./scripts/test.fish local         # Run locally (auto-cleans first)
./scripts/test.fish clean         # Clean all build artifacts
```

### Running Tests Locally

If you need to run tests locally (outside Docker):

```bash
# Clean build artifacts first to avoid permission issues
rm -rf PassManAPI/obj PassManAPI/bin PassManAPI.Tests/obj PassManAPI.Tests/bin

# Run tests
dotnet test PassManAPI.Tests/PassManAPI.Tests.csproj
```

### Why Docker for Tests?

When you run `docker compose up` to start the API, Docker creates `obj/` and `bin/` directories owned by the container user. Later running `dotnet test` locally fails with permission errors because your local user can't write to those directories.

**The solution:** Run tests in Docker using volume exclusions (configured in `docker-compose.yml`) to keep Docker and local build artifacts separate:

```yaml
test:
  volumes:
    - .:/workspace
    # Exclude build artifacts to prevent permission issues
    - /workspace/PassManAPI/obj
    - /workspace/PassManAPI/bin
    - /workspace/PassManAPI.Tests/obj
    - /workspace/PassManAPI.Tests/bin
```

This way:
- ✅ Docker tests use containerized build artifacts
- ✅ Local tests use local build artifacts  
- ✅ No permission conflicts between the two 
