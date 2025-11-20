<img src="https://r2cdn.perplexity.ai/pplx-full-logo-primary-dark%402x.png" style="height:64px;margin-right:32px"/>

# We decided to build the app in C\#. It will be a password manager app. I attached a use case document that we quickly put together and also an image containing some nice to haves if we need more complexity. I need you to make a brief document summarizing the project so I can reference it and know all the features that will or might be there and also those that will not be there. I do not want to over complicate the project.

The document should contain:

- a brief description/idea of the project
- technologies used (C\#, database, swagger...)
- Must haves, nice to haves, will not haves

Based on your use case document and the attached image, here's a concise project summary document:

***

# Password Manager API - Project Summary

## Project Description

A secure password management system built as a RESTful API in C\#. Users can register, create encrypted vaults to store credentials, organize passwords with categories and tags, and securely share vaults with other users. The system includes role-based access control, audit logging, and subscription tiers to manage feature access.

## Technologies

- **Backend Framework**: ASP.NET Core Web API
- **Programming Language**: C\# (.NET 8 or later)
- **Database**: PostgreSQL
- **ORM**: Entity Framework Core with Npgsql provider
- **Authentication**: OAuth 2.0 / JWT tokens
- **API Documentation**: Swagger/OpenAPI (Swashbuckle)
- **Security**: Encryption for stored credentials, password hashing (bcrypt/Argon2)

***

## Feature Breakdown

### Must Haves (Core Functionality)

**User Management:**

- User registration with email and strong password
- User login with JWT authentication
- Basic user profile management

**Vault Management:**

- Create, read, update, delete vaults
- Multiple vaults per user (e.g., Personal, Work)
- Basic folder/label organization within vaults

**Credential Storage:**

- Store login credentials (username, password, URL, notes)
- Encrypt stored passwords
- Categorize credentials (bank, social media, email, work, etc.)
- Search and filter credentials by tags/categories

**Basic Sharing:**

- Share vaults with other users via email invitation
- Simple role-based access (view-only vs. edit permissions)

**Security Features:**

- Password encryption in database
- Secure password hashing for user accounts
- Basic audit logging (who accessed/modified what)

***

### Nice to Haves (If Time Permits)

From the use case document:

- **Advanced Vault Sharing**: Granular role-based permissions (view, add, modify, admin)
- **Security Preferences**: Password generation policies, auto-logout timers, password change reminders
- **Attachments**: Store secure files (backup codes, scanned documents) with credentials
- **Version History**: Track and retrieve previous versions of credentials for auditing
- **Enhanced Audit Logs**: Track failed login attempts, access history, share activity
- **Device Management**: Device approval workflows

From the image (additional complexity):

- **Password Health Audit**: Analyze vault for weak, reused, or compromised passwords (client-side analysis)
- **Data Breach Monitoring**: Integration with "Have I Been Pwned" API to check if saved services have been breached
- **Vault Statistics**: Display password count, strongest/weakest password info, security insights
- **Secure Password Sharing**: Public-key cryptography for sharing individual passwords with non-users

***

### Will Not Have (Out of Scope)

- **Subscription/Payment System**: No Free/Premium/Enterprise tiers or billing (all users get full access)
- **Employee/Admin Portal**: No internal staff roles for database access or customer support
- **External Identity Provider Integration**: No LDAP, Auth0, or SSO integrations
- **Browser Extensions**: API-only, no client applications
- **Mobile Apps**: API-only, no native mobile development
- **2FA/MFA**: Basic password authentication only (no two-factor authentication)
- **Cross-service Password Updates**: No automatic password changes across external services
- **Technical API User with Views/Procedures**: Direct EF Core queries instead of stored procedures

***

## Database Structure (High-Level)

**Core Tables:**

- `Users` - User accounts and authentication
- `Vaults` - Vault metadata and ownership
- `Credentials` - Stored passwords and login info
- `Categories` - Credential categorization
- `VaultShares` - Sharing permissions between users
- `AuditLogs` - Security and access tracking

**Optional Tables (Nice to Have):**

- `Attachments` - Secure file storage
- `CredentialVersions` - Version history
- `Devices` - Approved device tracking

***

## API Endpoints (Core)

**Authentication:**

- `POST /api/auth/register`
- `POST /api/auth/login`

**Vaults:**

- `GET /api/vaults` - List user's vaults
- `POST /api/vaults` - Create vault
- `PUT /api/vaults/{id}` - Update vault
- `DELETE /api/vaults/{id}` - Delete vault

**Credentials:**

- `GET /api/vaults/{vaultId}/credentials` - List credentials in vault
- `POST /api/vaults/{vaultId}/credentials` - Add credential
- `PUT /api/credentials/{id}` - Update credential
- `DELETE /api/credentials/{id}` - Delete credential

**Sharing:**

- `POST /api/vaults/{vaultId}/share` - Share vault with user
- `DELETE /api/vaults/{vaultId}/share/{userId}` - Revoke access

**Audit:**

- `GET /api/audit/logs` - View audit logs for user's vaults

***

## Success Criteria

A successful implementation includes:

1. Secure user registration and authentication
2. Encrypted credential storage and retrieval
3. Multi-vault support with organization features
4. Basic vault sharing with permission control
5. Working API with Swagger documentation
6. PostgreSQL database with proper relationships
7. Basic audit logging for security

The project should prioritize security, clean API design, and a solid foundation that could be extended with "nice to have" features if time permits.

***

Let me know if you'd like me to adjust the scope or add/remove anything from this summary!
<span style="display:none">[^1][^2]</span>

<div align="center">⁂</div>

[^1]: image.jpg

[^2]: PasswordManagerUseCaseDocument.docx

