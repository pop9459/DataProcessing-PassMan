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

## Project Status

This board tracks the current development progress.

### In Progress

#### Documentation
- [x] Architecture Diagram
- [ ] Entity Relationship Diagram (ERD)
- [ ] Class Diagram

#### Infrastructure
- [ ] Setup ASP.NET Core Web API project structure
- [x] Setup a Dockerfile for the API host

### Upcoming
- [ ] Configure PostgreSQL database with Entity Framework Core
- [ ] Implement basic user registration and JWT authentication
- [ ] Setup Swagger/OpenAPI for API documentation
