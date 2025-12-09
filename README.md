# Password Manager API

A secure password management system built as a RESTful API in C#.

This repository contains the source code for the Password Manager API, a project focused on providing a secure and reliable way to manage credentials.

## Running the app

### Docker compose
1. Clone the repository
    ```
    git clone https://github.com/pop9459/DataProcessing-PassMan
    ```
2. cd into the repository
    ```
    cd DataProcessing-PassMan
    ```
3. Run 
    ```
    docker compose up -d
    ```
4. Open http://localhost:5246/

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
4. Restore 
    ```
    dotnet restore
    ```
5. Run the project
    ```
    dotnet run --project PassManAPI
    ``` 
6. Open http://localhost:5246/

## Accessing the database manually

1. Install [MySQL Workbench](https://dev.mysql.com/downloads/workbench/)

2. Open the database menu

<kbd>Ctrl</kbd>+<kbd>J</kbd>

3. Enter the details

-Host: localhost
-Port: 3306
-User: root
-Password: hihi         (intentional leak hihihiha)

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
├── Models/                 # Data acess layers (namespace: PassManAPI.Models)
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
