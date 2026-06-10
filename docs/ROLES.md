# Role-Based Access Control (RBAC) in PassMan

> **Scope:** This document covers *application-level* roles and permissions — the rules that decide what an authenticated user can do inside the API.
> Database-account privileges (the least-privilege MySQL user the API connects with) are a separate concern covered in issue #179.

---

## Roles

PassMan defines four roles. Each role is a named set of permissions following the **principle of least privilege** — roles only grant what they actually need.

| Role | Purpose |
|------|---------|
| **Admin** | Full access — all 17 permissions |
| **SecurityAuditor** | Read-only access to audit logs, vaults, credentials, and system health |
| **VaultOwner** | Create and manage vaults, credentials, tags, and share vaults with others |
| **VaultReader** | Read-only access to vaults, credentials, and tags they have been shared into |

---

## Permissions

Permissions are string constants defined in `PassManAPI/Models/Permissions.cs`.

| Permission | Admin | SecurityAuditor | VaultOwner | VaultReader |
|------------|:-----:|:---------------:|:----------:|:-----------:|
| `vault.read` | ✅ | ✅ | ✅ | ✅ |
| `vault.create` | ✅ | | ✅ | |
| `vault.update` | ✅ | | ✅ | |
| `vault.delete` | ✅ | | ✅ | |
| `vault.share` | ✅ | | ✅ | |
| `credential.read` | ✅ | ✅ | ✅ | ✅ |
| `credential.create` | ✅ | | ✅ | |
| `credential.update` | ✅ | | ✅ | |
| `credential.delete` | ✅ | | ✅ | |
| `tag.read` | ✅ | | ✅ | ✅ |
| `tag.create` | ✅ | | ✅ | |
| `tag.update` | ✅ | | ✅ | |
| `tag.delete` | ✅ | | ✅ | |
| `audit.read` | ✅ | ✅ | | |
| `user.manage` | ✅ | | | |
| `role.manage` | ✅ | | | |
| `system.health` | ✅ | ✅ | | |

---

## How Roles Are Seeded

Roles and their permission claims are created at startup by `DbSeeder.SeedAsync` (`PassManAPI/Data/DbSeeder.cs`).

```
App startup
  └── DbSeeder.SeedAsync(services)
        ├── For each role in PermissionConstants.RolePermissions:
        │     ├── Create the role in AspNetRoles if it does not exist
        │     └── Attach each permission as a claim (type: "permission") in AspNetRoleClaims
        └── (Optional) seed demo users when seedDemoUsers: true
```

The seeder is **idempotent** — running it multiple times does not create duplicate roles or claims.

### Demo users (development only)

| Username | Email | Password | Role |
|----------|-------|----------|------|
| admin | admin@passman.test | Admin123! | Admin |
| auditor | auditor@passman.test | Audit123! | SecurityAuditor |
| owner | owner@passman.test | Owner123! | VaultOwner |
| reader | reader@passman.test | Reader123! | VaultReader |

Demo user seeding is **disabled by default**. Enable it in `Program.cs` by passing `seedDemoUsers: true` to `DbSeeder.SeedAsync`.

---

## How Roles Are Enforced

### 1. JWT Claims

When a user logs in, the JWT issued by `AuthController` includes all permission strings as individual claims of type `"permission"`. Example token payload:

```json
{
  "sub": "42",
  "email": "owner@passman.test",
  "permission": ["vault.read", "vault.create", "vault.update", "vault.delete",
                 "vault.share", "credential.read", "credential.create",
                 "credential.update", "credential.delete",
                 "tag.read", "tag.create", "tag.update", "tag.delete"],
  "exp": 1749600000
}
```

Embedding permissions directly in the token means every request is self-contained — the API does not need an extra database round-trip to authorise most operations.

### 2. Authorization Policies

At startup (`Program.cs`), one policy is registered for every permission string:

```csharp
builder.Services.AddAuthorization(options =>
{
    foreach (var permission in PermissionConstants.All)
    {
        options.AddPolicy(permission, policy =>
            policy.RequireClaim(PermissionConstants.ClaimType, permission));
    }
});
```

### 3. Controller Attributes

Controllers and actions declare their required permission via `[Authorize(Policy = ...)]`:

```csharp
// Only users with vault.share permission can call this endpoint
[Authorize(Policy = PermissionConstants.VaultShare)]
public async Task<IActionResult> ShareVault(...) { ... }

// Only users with audit.read permission can read audit logs
[Authorize(Policy = PermissionConstants.AuditRead)]
public async Task<IActionResult> GetAuditLogs(...) { ... }
```

---

## Key Source Files

| File | Purpose |
|------|---------|
| `PassManAPI/Models/Permissions.cs` | Permission string constants and role→permissions map |
| `PassManAPI/Data/DbSeeder.cs` | Idempotent role and demo-user seeding |
| `PassManAPI/Program.cs` (lines ~144–161) | Authorization policy registration |
| `PassManAPI/Controllers/AuthController.cs` | JWT issuance with permission claims; role assignment endpoint |
| `PassManAPI/Controllers/VaultSharesController.cs` | Example of `[Authorize(Policy = PermissionConstants.VaultShare)]` |
