namespace PassManAPI.Models;

/// <summary>
/// Centralized permission catalog used for role-based access control.
/// Claim type is intentionally named "permission" to align with common
/// API gateway and policy engines.
/// </summary>
public static class PermissionConstants
{
    public const string ClaimType = "permission";

    // Vault permissions
    public const string VaultRead = "vault.read";
    public const string VaultCreate = "vault.create";
    public const string VaultUpdate = "vault.update";
    public const string VaultDelete = "vault.delete";
    public const string VaultShare = "vault.share";

    // Credential permissions
    public const string CredentialRead = "credential.read";
    public const string CredentialCreate = "credential.create";
    public const string CredentialUpdate = "credential.update";
    public const string CredentialDelete = "credential.delete";

    // Audit / admin permissions
    public const string AuditRead = "audit.read";
    public const string UserManage = "user.manage";
    public const string RoleManage = "role.manage";
    public const string SystemHealth = "system.health";

    public static readonly string[] All =
    [
        VaultRead,
        VaultCreate,
        VaultUpdate,
        VaultDelete,
        VaultShare,
        CredentialRead,
        CredentialCreate,
        CredentialUpdate,
        CredentialDelete,
        AuditRead,
        UserManage,
        RoleManage,
        SystemHealth
    ];

    /// <summary>
    /// Principle of least privilege role map. Roles are additive sets of permissions.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string[]> RolePermissions =
        new Dictionary<string, string[]>
        {
            {
                "Admin",
                All
            },
            {
                "SecurityAuditor",
                new[]
                {
                    AuditRead,
                    VaultRead,
                    CredentialRead,
                    SystemHealth
                }
            },
            {
                "VaultOwner",
                new[]
                {
                    VaultRead,
                    VaultCreate,
                    VaultUpdate,
                    VaultDelete,
                    VaultShare,
                    CredentialRead,
                    CredentialCreate,
                    CredentialUpdate,
                    CredentialDelete
                }
            },
            {
                "VaultReader",
                new[]
                {
                    VaultRead,
                    CredentialRead
                }
            }
        };
}

