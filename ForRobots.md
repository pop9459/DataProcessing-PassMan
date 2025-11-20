## For Robots: AI Contributor Guidelines

**Version:** 1.0
**Last Updated:** 2025-11-20

### Project Directive

Your primary directive is to assist in the development and maintenance of a secure, RESTful password manager API. The core of this project is built with C\# and ASP.NET Core. Your contributions should align with the established technology stack and coding standards. When generating code or providing assistance, prioritize security, clarity, and maintainability.

### Core Technologies

You are to exclusively use the following technologies. Do not introduce new libraries or frameworks without explicit instruction.

* **Language:** C\# (latest stable version)
* **Framework:** ASP.NET Core Web API
* **Database:** PostgreSQL
* **ORM:** Entity Framework Core (EF Core) with the Npgsql provider
* **Authentication:** JWT-based, following OAuth 2.0 principles
* **API Documentation:** Swagger (OpenAPI) via Swashbuckle


### Code Generation Standards

When generating code, adhere to the following standards:

* **Style:** Follow the standard Microsoft C\# coding conventions.
* **Architecture:** The project follows a standard API architecture. Adhere to the existing patterns for controllers, services, and repositories.
* **Asynchronous Programming:** Use `async` and `await` for all I/O-bound operations, especially database and network calls.
* **Dependency Injection:** Use the built-in dependency injection container in ASP.NET Core.
* **LINQ:** Use LINQ for database queries through EF Core. Do not write raw SQL unless explicitly instructed.
* **Error Handling:** Implement robust error handling using try-catch blocks and appropriate HTTP status codes.


### API Interaction

* **Endpoints:** All API endpoints should follow RESTful principles.
* **Authentication:** All endpoints, except for registration and login, must be protected. A valid JWT must be provided in the `Authorization` header as a Bearer token.
* **Data Format:** The API consumes and produces JSON.


### Security Protocol

This is a password manager. Security is the highest priority.

* **Password Storage:** User passwords for their accounts must be hashed using a strong, salted hashing algorithm (e.g., bcrypt, Argon2).
* **Credential Encryption:** Passwords stored within a user's vault **must be encrypted at all times** in the database.
* **Sensitive Data:** Never log, transmit, or expose plaintext passwords or other sensitive user data.
* **Input Validation:** Sanitize and validate all user input to prevent injection attacks.


### Feature Implementation

Refer to the project's `ProjectSummary.md` for the defined "Must Haves," "Nice to Haves," and "Will Not Haves."

* **"Must Haves"**: These are the core features. Prioritize their implementation and stability.
* **"Nice to Haves"**: These are features to be considered only after the core functionality is complete and stable. When implementing these, ensure they do not compromise the security or stability of the core application.
* **"Will Not Haves"**: Do not implement any features listed in this section.


### Contribution Workflow

1. **Analyze the Request:** Understand the developer's request in the context of the existing codebase and the guidelines in this document.
2. **Generate Code:** Provide code snippets, full classes, or endpoint implementations that adhere to all the standards above.
3. **Explain Your Work:** Briefly explain the purpose of the generated code and how it fits into the project's architecture.
4. **Verify against Guidelines:** Before presenting your solution, double-check that it follows the security protocols and coding standards.
