# Password Manager API

A secure password management system built as a RESTful API in C#.

This repository contains the source code for the Password Manager API, a project focused on providing a secure and reliable way to manage credentials.

## Running the app

### Setting up User Secrets (Required for Development)

The application uses Google OAuth for authentication. Before running the app, you need to configure your Google credentials using .NET User Secrets:

1. Navigate to the PassManGUI project directory:
    ```bash
    cd PassManGUI
    ```

2. Set your Google OAuth credentials:
    ```bash
    dotnet user-secrets set "Authentication:Google:ClientId" "your-google-client-id"
    dotnet user-secrets set "Authentication:Google:ClientSecret" "your-google-client-secret"
    ```

3. Verify your secrets are set:
    ```bash
    dotnet user-secrets list
    ```

**Note:** User secrets are stored locally on your machine and are never committed to Git. Each team member needs to set up their own secrets.

### Docker compose
1. Clone the repository
    ```
    git clone https://github.com/pop9459/DataProcessing-PassMan
    ```
2. cd into the repository
    ```
    cd DataProcessing-PassMan
    ```
3. Set up user secrets (see above)
4. Run 
    ```
    docker compose up -d
    ```
5. Open http://localhost:5246/

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
4. Set up user secrets (see "Setting up User Secrets" section above)
5. Run the project
    ```
    dotnet run --project PassManAPI
    ``` 
6. Open http://localhost:5246/

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

- Integration tests live in `PassManAPI.Tests` and run the API with a test `WebApplicationFactory` using in-memory SQLite (no MySQL needed).
- Just run from the root directory:
    ```
    dotnet test PassManAPI.Tests/PassManAPI.Tests.csproj
    ``` 
