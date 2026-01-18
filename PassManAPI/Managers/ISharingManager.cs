using PassManAPI.Models;

namespace PassManAPI.Managers;

/// <summary>
/// Result object for sharing operations.
/// </summary>
public class SharingResult<T>
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public T? Data { get; init; }

    public static SharingResult<T> Ok(T data) =>
        new() { Success = true, Data = data };

    public static SharingResult<T> Fail(string error) =>
        new() { Success = false, Error = error };
}

/// <summary>
/// DTO for vault share information.
/// </summary>
public record VaultShareInfo(
    int VaultId,
    string VaultName,
    int UserId,
    string UserEmail,
    SharePermission Permission,
    DateTime SharedAt,
    int? SharedByUserId,
    string? SharedByUserEmail
);

/// <summary>
/// DTO for sharing invitation.
/// </summary>
public record ShareInvitation(
    Guid Token,
    int VaultId,
    string Email,
    SharePermission Permission,
    DateTime CreatedAt,
    DateTime ExpiresAt
);

/// <summary>
/// Interface for vault sharing business logic.
/// </summary>
public interface ISharingManager
{
    /// <summary>
    /// Shares a vault with another user by email.
    /// </summary>
    /// <param name="vaultId">The vault to share.</param>
    /// <param name="ownerId">The current owner/admin requesting the share.</param>
    /// <param name="targetEmail">The email of the user to share with.</param>
    /// <param name="permission">The permission level to grant.</param>
    /// <returns>Result containing the share info or error.</returns>
    Task<SharingResult<VaultShareInfo>> ShareVaultAsync(int vaultId, int ownerId, string targetEmail, SharePermission permission);

    /// <summary>
    /// Creates a sharing invitation link for a vault.
    /// </summary>
    /// <param name="vaultId">The vault to create invitation for.</param>
    /// <param name="email">The email of the intended recipient.</param>
    /// <param name="permission">The permission level to grant upon acceptance.</param>
    /// <param name="expiresAt">When the invitation expires.</param>
    /// <returns>Result containing the invitation or error.</returns>
    Task<SharingResult<ShareInvitation>> CreateInvitationAsync(int vaultId, string email, SharePermission permission, DateTime expiresAt);

    /// <summary>
    /// Accepts a sharing invitation.
    /// </summary>
    /// <param name="token">The invitation token.</param>
    /// <param name="userId">The user accepting the invitation.</param>
    /// <returns>Result containing the share info or error.</returns>
    Task<SharingResult<VaultShareInfo>> AcceptInvitationAsync(Guid token, int userId);

    /// <summary>
    /// Revokes a user's access to a vault.
    /// </summary>
    /// <param name="vaultId">The vault to revoke access from.</param>
    /// <param name="ownerId">The owner/admin performing the revocation.</param>
    /// <param name="targetUserId">The user whose access is being revoked.</param>
    /// <returns>Result indicating success or failure.</returns>
    Task<SharingResult<bool>> RevokeAccessAsync(int vaultId, int ownerId, int targetUserId);

    /// <summary>
    /// Changes a user's permission level for a vault.
    /// </summary>
    /// <param name="vaultId">The vault to modify permissions for.</param>
    /// <param name="ownerId">The owner/admin performing the change.</param>
    /// <param name="targetUserId">The user whose permission is being changed.</param>
    /// <param name="newPermission">The new permission level.</param>
    /// <returns>Result containing the updated share info or error.</returns>
    Task<SharingResult<VaultShareInfo>> ChangeRoleAsync(int vaultId, int ownerId, int targetUserId, SharePermission newPermission);

    /// <summary>
    /// Gets all shares for a vault.
    /// </summary>
    /// <param name="vaultId">The vault to get shares for.</param>
    /// <param name="userId">The user requesting the shares (must be owner/admin).</param>
    /// <returns>Result containing the list of shares or error.</returns>
    Task<SharingResult<IList<VaultShareInfo>>> GetVaultSharesAsync(int vaultId, int userId);

    /// <summary>
    /// Checks if a user has access to a vault.
    /// </summary>
    /// <param name="vaultId">The vault to check access for.</param>
    /// <param name="userId">The user to check.</param>
    /// <param name="requiredPermission">Optional minimum permission level required.</param>
    /// <returns>True if user has access (at required level), false otherwise.</returns>
    Task<bool> HasAccessAsync(int vaultId, int userId, SharePermission? requiredPermission = null);
}
