# PassMan System Architecture

## High-Level Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                        ASP.NET Core Application                             │
│                                                                             │
│  ┌──────────────────── PRESENTATION LAYER ────────────────────────────────┐ │
│  │                                                                        │ │
│  │  [INV_CTRL]  [CRED_CTRL]  [VAULT_CTRL]  [TAG_CTRL]  [SHARE_CTRL]       │ │
│  │                                                                        │ │
│  │  [AUDIT_CTRL]  [AUTH_CTRL]  [USER_CTRL]                                │ │
│  │                                                                        │ │
│  └────────────────────────────────────────────────────────────────────────┘ │
│                                      ↓                                      │
│  ┌──────────────────── SECURITY LAYER ────────────────────────────────────┐ │
│  │                                                                        │ │
│  │            [JWT (OAuth 2.0)] → [AUTHZ] → [IDENTITY]                    │ │
│  │                                                                        │ │
│  └────────────────────────────────────────────────────────────────────────┘ │
│                                      ↓                                      │
│  ┌────────────────── BUSINESS LOGIC LAYER ────────────────────────────────┐ │
│  │                                                                        │ │
│  │  [AUTH_MGR] → [BCRYPT], [TOTP], [GOOGLE]                               │ │
│  │                                                                        │ │
│  │  [VAULT_MGR] → [AUDIT_LOG]                                             │ │
│  │                                                                        │ │
│  │  [CRED_MGR] → [ENCRYPT], [ATTACH], [AUDIT_LOG]                         │ │
│  │                                                                        │ │
│  │  [SHARE_MGR] → [AUDIT_LOG], [EMAIL]                                    │ │
│  │                                                                        │ │
│  │  [AUDIT_MGR] → [AUDIT_LOG]                                             │ │
│  │                                                                        │ │
│  │  [USER_MGR] → [SUBSCRIPTION]                                           │ │
│  │                                                                        │ │
│  └────────────────────────────────────────────────────────────────────────┘ │
│                                      ↓                                      │
│  ┌─────────────────── DATA ACCESS LAYER ──────────────────────────────────┐ │
│  │                                                                        │ │
│  │  [ApplicationDbContext] → [Entity Framework Core] → [Repository]       │ │
│  │                                                                        │ │
│  └────────────────────────────────────────────────────────────────────────┘ │
│                                      ↓                                      │
└──────────────────────────────────────┼──────────────────────────────────────┘
                                       ↓
                            ┌──────────────────┐
                            │  PostgreSQL /    │
                            │  MySQL Database  │
                            └──────────────────┘
```

---

## Detailed Component Interaction

### Client Layer
```
┌─────────────────┐
│  WEB BROWSER    │
│  (Blazor GUI)   │
└────────┬────────┘
         │ HTTP/HTTPS Requests
         ↓
┌─────────────────┐
│  MOBILE CLIENT  │
└────────┬────────┘
         │
         ↓
```

---

## Presentation Layer → Managers Mapping

| Controller | Primary Manager | Secondary Managers |
|-----------|-----------------|------------------|
| **INV_CTRL** (Invitations) | VaultManager | - |
| **CRED_CTRL** (Credentials) | CredentialManager | VaultManager |
| **VAULT_CTRL** (Vaults) | VaultManager | - |
| **TAG_CTRL** (Tags) | VaultManager | - |
| **SHARE_CTRL** (VaultShares) | SharingManager | - |
| **AUDIT_CTRL** (Audit) | AuditManager | - |
| **AUTH_CTRL** (Auth) | AuthManager | - |
| **USER_CTRL** (Users) | UserManager | - |

---

## Manager Responsibilities & Dependencies

### **AuthManager**
- Handles user registration, login, password reset
- **Uses**: BCrypt (password hashing), TOTP (2FA), Google OAuth
- **Logs to**: Audit Log
- **Accesses**: ApplicationDbContext

### **VaultManager**
- Creates, updates, deletes vaults
- Manages vault metadata and soft deletes
- **Logs to**: Audit Log
- **Accesses**: ApplicationDbContext

### **CredentialManager**
- Creates, reads, updates, deletes credentials
- Manages password encryption/decryption
- Handles attachments
- **Uses**: Encryption Service, Attachment Handler
- **Logs to**: Audit Log
- **Accesses**: ApplicationDbContext

### **SharingManager**
- Manages vault sharing permissions
- Handles access control
- Sends sharing notifications
- **Uses**: Email Service
- **Logs to**: Audit Log
- **Accesses**: ApplicationDbContext

### **UserManager**
- Manages user profiles and preferences
- Handles subscription information
- **Uses**: Subscription API
- **Accesses**: ApplicationDbContext

### **AuditManager**
- Logs all security-relevant events
- Tracks user actions, access patterns
- **Logs to**: Audit Log
- **Accesses**: ApplicationDbContext

---

## Security Flow

```
┌──────────────┐
│   REQUEST    │
└──────┬───────┘
       │
       ↓
┌──────────────────────┐
│  HTTPS / TLS Layer   │
│  (Encryption)        │
└──────┬───────────────┘
       │
       ↓
┌──────────────────────┐
│  JWT Validation      │
│  (OAuth 2.0 Token)   │
└──────┬───────────────┘
       │
       ↓
┌──────────────────────┐
│  Authorization       │
│  (Policy Check)      │
└──────┬───────────────┘
       │
       ↓
┌──────────────────────┐
│  Identity Resolution │
│  (User/Roles)        │
└──────┬───────────────┘
       │
       ↓
┌──────────────────────┐
│  Route to Manager    │
│  (Business Logic)    │
└──────────────────────┘
```

---

## Data Flow Example: Create Credential

```
1. CLIENT
   └─→ HTTPS Request to /api/credentials
       │
       ├─→ PRESENTATION LAYER
       │   └─ CredentialsController.Post()
       │
       ├─→ SECURITY LAYER
       │   ├─ JWT Token Validation
       │   ├─ Authorization Policy Check
       │   └─ Identity Resolution
       │
       ├─→ BUSINESS LOGIC LAYER
       │   ├─ CredentialManager.CreateCredential()
       │   ├─ Encrypts password (ENCRYPT service)
       │   ├─ Handles attachments (ATTACH service)
       │   └─ Logs action (AUDIT_LOG)
       │
       ├─→ DATA ACCESS LAYER
       │   ├─ ApplicationDbContext
       │   ├─ Entity Framework Core
       │   └─ Repository Pattern
       │
       └─→ DATABASE
           └─ Persists to Credentials table
```

---

## Infrastructure Services

### **BCRYPT** - Password Hashing
- Used by: AuthManager
- Purpose: Secure password storage
- One-way hashing algorithm

### **ENCRYPT** - Credential Encryption
- Used by: CredentialManager
- Purpose: Encrypt/decrypt sensitive password data
- Symmetric encryption

### **TOTP** - Two-Factor Authentication
- Used by: AuthManager
- Purpose: Generate time-based one-time passwords
- For 2FA login

### **ATTACH** - Attachment Handler
- Used by: CredentialManager
- Purpose: Manage file uploads/downloads
- Handles encryption keys for attachments

### **EMAIL** - Email Service
- Used by: SharingManager, InvitationsController
- Purpose: Send invitations, notifications
- Integration point for email delivery

### **GOOGLE OAuth** - Social Authentication
- Used by: AuthManager
- Purpose: Enable Google sign-in
- Third-party identity provider

### **SUBSCRIPTION API** - Subscription Management
- Used by: UserManager
- Purpose: Track user subscription tier
- Integration with subscription service

### **AUDIT_LOG** - Audit Logging
- Used by: All Managers
- Purpose: Track security events
- Compliance and security auditing

---

## Database Layer Details

### **ApplicationDbContext**
- Entity Framework Core DbContext
- Manages all database operations
- Implements Unit of Work pattern

### **Entity Framework Core**
- ORM (Object-Relational Mapper)
- Converts LINQ queries to SQL
- Handles migrations

### **Repository Pattern**
- Abstracts data access logic
- Generic repository for CRUD operations
- Promotes testability

### **Database**
- PostgreSQL or MySQL
- 9 core tables (Users, Vaults, Credentials, Tags, Categories, etc.)
- Cascade deletes, soft deletes, indexing

---

## Key Design Patterns

### **Layered Architecture**
- Clear separation of concerns
- Each layer has specific responsibilities
- Easier to test and maintain

### **Manager Pattern (Service Layer)**
- Business logic encapsulation
- Handles domain rules
- Orchestrates data access

### **Repository Pattern**
- Abstract data access
- Easier to swap data sources
- Improved testability

### **Dependency Injection**
- ASP.NET Core built-in DI container
- Loose coupling between components
- Easier testing and mocking

### **OAuth 2.0 + JWT**
- Stateless authentication
- Supports multiple identity providers
- Industry standard security

---

## External Integrations

| Service | Purpose | Used By |
|---------|---------|---------|
| **Google OAuth** | Social login | AuthManager |
| **Email Service** | Invitations, notifications | SharingManager, InvitationsController |
| **Encryption Service** | External encryption provider | CredentialManager |
| **Subscription API** | Manage user tiers | UserManager |
| **Swagger/OpenAPI** | API documentation | Development & Testing |

---

## Technology Stack

| Layer | Technology |
|-------|-----------|
| **Framework** | ASP.NET Core 8.x |
| **ORM** | Entity Framework Core |
| **Authentication** | ASP.NET Identity + OAuth 2.0 |
| **Authorization** | Policy-based authorization |
| **Encryption** | BCrypt (passwords), AES (data) |
| **Database** | PostgreSQL / MySQL |
| **API Docs** | Swagger/OpenAPI |
| **Logging** | Structured logging (Serilog) |

---

## Deployment Architecture (Production)

```
┌────────────────────────────────────────┐
│         Load Balancer / CDN            │
└────────────────┬───────────────────────┘
                 │
    ┌────────────┼────────────┐
    ↓            ↓            ↓
┌─────────┐ ┌─────────┐ ┌─────────┐
│  API    │ │  API    │ │  API    │
│Instance1│ │Instance2│ │Instance3│
└────┬────┘ └────┬────┘ └────┬────┘
     │           │           │
     └───────────┼───────────┘
                 │
        ┌────────┴───────┐
        │                │
    ┌───▼────┐    ┌──────▼───┐
    │ Cache  │    │ Database │
    │(Redis) │    │ Cluster  │
    └────────┘    └──────────┘
```

---

## Security Considerations

**HTTPS/TLS** - All communications encrypted
**JWT Tokens** - Stateless authentication
**Password Hashing** - BCrypt with salt
**Data Encryption** - AES encryption for sensitive data
**Authorization Policies** - Fine-grained access control
**Audit Logging** - All actions tracked
**2FA/TOTP** - Multi-factor authentication support
**Soft Deletes** - Data retention for compliance
**CORS** - Cross-origin request protection
