namespace PassManAPI.Managers;

/// <summary>
/// Result wrapper for credential operations.
/// </summary>
public class CredentialOperationResult<T>
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public T? Data { get; init; }

    public static CredentialOperationResult<T> Ok(T data) =>
        new() { Success = true, Data = data };

    public static CredentialOperationResult<T> Fail(string error) =>
        new() { Success = false, Error = error };
}

/// <summary>
/// Interface for credential business logic operations.
/// Handles CRUD operations with encryption and audit logging.
/// </summary>
public interface ICredentialManager
{
    /// <summary>
    /// Creates a new credential in the specified vault.
    /// </summary>
    Task<CredentialOperationResult<CredentialDto>> CreateCredentialAsync(
        int vaultId,
        int userId,
        string title,
        string encryptedPassword,
        string? username = null,
        string? url = null,
        string? notes = null,
        int? categoryId = null);

    /// <summary>
    /// Gets all credentials in a vault if the user has access.
    /// </summary>
    Task<CredentialOperationResult<IEnumerable<CredentialDto>>> GetCredentialsByVaultAsync(int vaultId, int userId);

    /// <summary>
    /// Gets a specific credential by ID if the user has access.
    /// </summary>
    Task<CredentialOperationResult<CredentialDto>> GetCredentialByIdAsync(int credentialId, int userId);

    /// <summary>
    /// Updates a credential's metadata (not password). Only vault owner or admin can update.
    /// </summary>
    Task<CredentialOperationResult<CredentialDto>> UpdateCredentialAsync(
        int credentialId,
        int userId,
        string title,
        string? username = null,
        string? url = null,
        string? notes = null,
        int? categoryId = null);

    /// <summary>
    /// Updates a credential's encrypted password. Only vault owner or admin can update.
    /// </summary>
    Task<CredentialOperationResult<CredentialDto>> UpdateCredentialPasswordAsync(
        int credentialId,
        int userId,
        string encryptedPassword);

    /// <summary>
    /// Deletes a credential. Only vault owner can delete.
    /// </summary>
    Task<CredentialOperationResult<bool>> DeleteCredentialAsync(int credentialId, int userId);

    /// <summary>
    /// Searches credentials across all accessible vaults for the user.
    /// </summary>
    Task<CredentialOperationResult<IEnumerable<CredentialDto>>> SearchCredentialsAsync(int userId, string query);

    /// <summary>
    /// Checks if a user has access to a credential (via vault access).
    /// </summary>
    Task<bool> HasAccessAsync(int credentialId, int userId);

    /// <summary>
    /// Checks if a user can modify a credential (vault owner or admin share).
    /// </summary>
    Task<bool> CanModifyAsync(int credentialId, int userId);
}
