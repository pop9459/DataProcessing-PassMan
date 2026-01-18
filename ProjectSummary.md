# Password Manager API - Project Summary

The document should contain:

- a brief description/idea of the project
- technologies used (C\#, database, swagger...)
- Must haves, nice to haves, will not haves

***

## Project Description

A secure password management system built as a RESTful API in C#. Users can register, create encrypted vaults to store credentials, organize passwords with categories and tags, and securely share vaults with other users. The system includes role-based access control, audit logging, and subscription tiers to manage feature access.

## Technologies

- **Backend Framework**: ASP.NET Core 10 Web API
- **Programming Language**: C# (.NET 10)
- **Database**: MySQL 8.0
- **ORM**: Entity Framework Core with Pomelo.EntityFrameworkCore.MySql provider
- **Authentication**: JWT Bearer + Google OAuth 2.0
- **API Documentation**: Swagger/OpenAPI (Swashbuckle)
- **Security**: AES encryption for stored credentials, password hashing (ASP.NET Core Identity)
- **Testing**: xUnit with FluentAssertions (106 passing tests)

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

- **External Identity Provider Integration**: No LDAP, Auth0, or SSO integrations beyond Google OAuth
- **Browser Extensions**: API-only, Blazor frontend only
- **Mobile Apps**: No native mobile development (API supports future mobile apps)
- **2FA/MFA**: Basic password authentication + Google OAuth only (no TOTP/SMS 2FA)
- **Cross-service Password Updates**: No automatic password changes across external services
- **Enterprise Admin Panel**: No internal staff roles for database access or customer support

***

## Database Structure (High-Level)

**Core Tables:**

- `AspNetUsers` - User accounts with ASP.NET Core Identity
- `Vaults` - Vault metadata and ownership
- `Credentials` - Stored passwords and login info (encrypted)
- `Tags` - Tag-based organization
- `CredentialTags` - Many-to-many relationship
- `VaultShares` - Sharing permissions between users
- `AuditLogs` - Security and access tracking
- `SubscriptionTiers` - Free/Premium tier definitions

**Implemented Tables:**

- `Attachments` - Secure file storage with credentials
- `Invitations` - Vault sharing via email invitations
- `AspNetRoles` - Role definitions (Admin, VaultOwner, etc.)
- `AspNetUserRoles` - User-role assignments
- `AspNetRoleClaims` - Permission claims for roles

**Database Features:**

- View: `vwUserVaultAccess` for optimized vault access queries
- Stored Procedures: `sp_AddVaultShare`, `sp_LogAudit`
- Triggers: `trg_Credentials_SetUpdatedAt` for automatic timestamps
- Constraints: PK/FK, unique indexes, cascading deletes
- Isolation: `READ COMMITTED` for high-concurrency operations

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