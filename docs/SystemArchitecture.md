# PassMan System Architecture

## High-Level Architecture

```
                            ┌──────────────────┐
                            │     CLIENT       │
                            │   (Web Browser)  │
                            └────────┬─────────┘
                                     ↑
                                     │ HTTP (port 5246)
                                     ↓
┌─────────────────────────────────────────────────────────────────────────────┐
│                        ASP.NET Core Application                             │
│                                                                             │
│  ┌──────────────────── SECURITY LAYER ────────────────────────────────────┐ │
│  │                                                                        │ │
│  │            [JWT Validation] → [Authorization] → [ASP.NET Identity]     │ │
│  │                                                                        │ │
│  └────────────────────────────────────────────────────────────────────────┘ │
│                                      ↑                                      │
│                                      │ in-process                           │
│                                      ↓                                      │
│  ┌──────────────────── PRESENTATION LAYER ────────────────────────────────┐ │
│  │                                                                        │ │
│  │  [AUTH_CTRL]  [USER_CTRL]  [VAULT_CTRL]  [CRED_CTRL]  [SHARE_CTRL]    │ │
│  │                                                                        │ │
│  │  [INV_CTRL]  [TAG_CTRL]  [AUDIT_CTRL]                                  │ │
│  │                                                                        │ │
│  └────────────────────────────────────────────────────────────────────────┘ │
│                                      ↑                                      │
│                                      │ in-process                           │
│                                      ↓                                      │
│  ┌────────────────── BUSINESS LOGIC LAYER ────────────────────────────────┐ │
│  │                                                                        │ │
│  │  [VAULT_MGR] → [AUDIT_LOG]                                             │ │
│  │                                                                        │ │
│  │  [CRED_MGR] → [ENCRYPT], [ATTACH], [AUDIT_LOG]                         │ │
│  │                                                                        │ │
│  │  [SHARE_MGR] → [AUDIT_LOG], [EMAIL (SMTP)]                             │ │
│  │                                                                        │ │
│  │  [AUDIT_MGR] → [AUDIT_LOG]                                             │ │
│  │                                                                        │ │
│  │  [USER_MGR]                                                            │ │
│  │                                                                        │ │
│  └────────────────────────────────────────────────────────────────────────┘ │
│                                      ↑                                      │
│                                      │ in-process                           │
│                                      ↓                                      │
│  ┌─────────────────── DATA ACCESS LAYER ──────────────────────────────────┐ │
│  │                                                                        │ │
│  │              [ApplicationDbContext] → [Entity Framework Core]          │ │
│  │                                                                        │ │
│  └────────────────────────────────────────────────────────────────────────┘ │
│                                      ↑                                      │
│                                      │ MySQL protocol / TCP:3306            │
└──────────────────────────────────────┼──────────────────────────────────────┘
                                       ↓
                            ┌──────────────────┐
                            │   MySQL 8.0      │
                            └──────────────────┘
```

**Security runs before controllers.** Every inbound request passes through JWT validation, authorization policy checks, and ASP.NET Identity resolution before any controller action executes.

> **Note on scope:** A Blazor Server GUI was developed as a companion frontend but is outside the assessment scope — the graded deliverable is the REST API. The GUI is incomplete and not part of the grading rubric.

---

## Controller → Manager Mapping

| Controller | Primary Manager | Notes |
|-----------|-----------------|-------|
| **AUTH_CTRL** (Auth) | — | Uses ASP.NET Identity directly (`UserManager<User>`, `SignInManager<User>`) |
| **USER_CTRL** (Users) | UserManager | |
| **VAULT_CTRL** (Vaults) | VaultManager | |
| **CRED_CTRL** (Credentials) | CredentialManager | Also uses VaultManager for access checks |
| **SHARE_CTRL** (VaultShares) | SharingManager | |
| **INV_CTRL** (Invitations) | SharingManager | |
| **TAG_CTRL** (Tags) | — | Accesses ApplicationDbContext directly |
| **AUDIT_CTRL** (Audit) | AuditManager | |

---

## Manager Responsibilities

### **VaultManager**
- Creates, updates, soft-deletes vaults
- Enforces subscription tier limits (vault count)
- **Logs to**: Audit Log
- **Accesses**: ApplicationDbContext

### **CredentialManager**
- CRUD for credentials within vaults
- Encrypts/decrypts passwords (AES-256-GCM)
- Handles file attachments
- **Uses**: PasswordEncryptionService
- **Logs to**: Audit Log
- **Accesses**: ApplicationDbContext

### **SharingManager**
- Manages vault share records and invitations
- Enforces share permissions (read/write)
- Sends invitation emails
- **Uses**: Email Service
- **Logs to**: Audit Log
- **Accesses**: ApplicationDbContext

### **AuditManager**
- Writes to the audit log via `sp_LogAudit` stored procedure
- Used by all other managers
- **Accesses**: ApplicationDbContext

### **UserManager**
- Manages user profiles and subscription tier
- **Accesses**: ApplicationDbContext

---

## Security Flow

```
┌──────────────────────────────────────┐
│  CLIENT REQUEST                      │
│  HTTP + Authorization: Bearer <jwt>  │
└──────┬───────────────────────────────┘
       │ HTTP
       ↓
┌──────────────────────┐
│  JWT Validation      │
│  (Bearer token)      │
└──────┬───────────────┘
       │ in-process
       ↓
┌──────────────────────┐
│  Authorization       │
│  (Permission claims) │
└──────┬───────────────┘
       │ in-process
       ↓
┌──────────────────────┐
│  Identity Resolution │
│  (User + Roles)      │
└──────┬───────────────┘
       │ in-process
       ↓
┌──────────────────────┐
│  Controller Action   │
└──────────────────────┘
```

Authentication uses JWT Bearer tokens. After login, the client includes the token in every request via the `Authorization: Bearer` header.

---

## Data Flow Example: Create Credential

```
1. CLIENT
   └─→ [HTTP] POST /api/credentials  Authorization: Bearer <jwt>
       │
       ├─→ SECURITY LAYER              [in-process]
       │   ├─ JWT token validated
       │   ├─ credential.create permission checked
       │   └─ User identity resolved
       │
       ├─→ PRESENTATION LAYER          [in-process]
       │   └─ CredentialsController.CreateCredential()
       │
       ├─→ BUSINESS LOGIC LAYER        [in-process]
       │   ├─ CredentialManager.CreateAsync()
       │   ├─ Encrypts password (PasswordEncryptionService / AES-256-GCM)
       │   ├─ Handles attachment if present
       │   └─ Logs action via AuditManager
       │
       ├─→ DATA ACCESS LAYER           [in-process]
       │   └─ ApplicationDbContext (Entity Framework Core)
       │
       └─→ DATABASE                    [MySQL protocol / TCP:3306]
           └─ Persists to Credentials table (MySQL 8.0)
```

---

## Infrastructure Services

| Service | Implementation | Protocol | Used By |
|---------|---------------|----------|---------|
| **Password Hashing** | BCrypt (`BCryptPasswordHasher`) | in-process | ASP.NET Identity (AuthController) |
| **Credential Encryption** | AES-256-GCM (`PasswordEncryptionService`) | in-process | CredentialManager |
| **2FA / TOTP** | `TwoFactorService` | in-process | AuthController |
| **Breach Check** | Have I Been Pwned API (`BreachCheckService`) | HTTPS | AuthController |
| **Email** | `EmailService` | SMTP | SharingManager |
| **Audit Logging** | `AuditManager` + `sp_LogAudit` stored proc | in-process | All managers |

---

## Database Details

### ApplicationDbContext
- EF Core DbContext; single point of data access for all managers
- Runs at `READ COMMITTED` isolation level (via `ReadCommittedInterceptor`)

### MySQL 8.0 artifacts
- **View**: `vwUserVaultAccess` — vault access queries
- **Stored Procedures**: `sp_AddVaultShare`, `sp_LogAudit`
- **Trigger**: `trg_Credentials_SetUpdatedAt` — automatic timestamp updates
- Soft deletes on Vaults (IsDeleted flag + cascade)

---

## Technology Stack

| Layer | Technology |
|-------|-----------|
| **Framework** | ASP.NET Core 10 |
| **ORM** | Entity Framework Core |
| **Authentication** | ASP.NET Identity + JWT Bearer |
| **Authorization** | Permission-claim-based policies |
| **Password hashing** | BCrypt |
| **Credential encryption** | AES-256-GCM |
| **Database** | MySQL 8.0 |
| **API Docs** | Swagger / OpenAPI |
| **Logging** | ASP.NET Core built-in logging |

---

## Security Considerations

- **JWT tokens** — stateless authentication; signed with HS256
- **BCrypt** — password hashing with salt
- **AES-256-GCM** — authenticated encryption for stored credentials
- **Authorization policies** — fine-grained permission claims per role
- **Audit logging** — all security-relevant actions tracked via `sp_LogAudit`
- **2FA / TOTP** — multi-factor authentication
- **Soft deletes** — vault data retained after deletion
- **CORS** — restricted to known frontend origins
- **Least-privilege DB account** — API uses `passman_app` (not root)
