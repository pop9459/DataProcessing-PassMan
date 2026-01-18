namespace PassManAPI.Managers;

/// <summary>
/// Result wrapper for vault operations.
/// </summary>
public class VaultOperationResult<T>
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public T? Data { get; init; }

    public static VaultOperationResult<T> Ok(T data) =>
        new() { Success = true, Data = data };

    public static VaultOperationResult<T> Fail(string error) =>
        new() { Success = false, Error = error };
}

/// <summary>
/// Interface for vault business logic operations.
/// </summary>
public interface IVaultManager
{
    /// <summary>
    /// Creates a new vault for the specified user.
    /// </summary>
    Task<VaultOperationResult<VaultDto>> CreateVaultAsync(int userId, string name, string? description = null, string? icon = null);

    /// <summary>
    /// Gets all vaults accessible by the user (owned + shared).
    /// </summary>
    Task<VaultOperationResult<IEnumerable<VaultDto>>> GetUserVaultsAsync(int userId);

    /// <summary>
    /// Gets a specific vault by ID if the user has access.
    /// </summary>
    Task<VaultOperationResult<VaultDto>> GetVaultByIdAsync(int vaultId, int userId);

    /// <summary>
    /// Updates a vault's metadata. Only the owner can update.
    /// </summary>
    Task<VaultOperationResult<VaultDto>> UpdateVaultAsync(int vaultId, int userId, string name, string? description = null, string? icon = null);

    /// <summary>
    /// Soft-deletes a vault. Only the owner can delete.
    /// </summary>
    Task<VaultOperationResult<bool>> DeleteVaultAsync(int vaultId, int userId);

    /// <summary>
    /// Checks if a user has access to a vault (owner or shared).
    /// </summary>
    Task<bool> HasAccessAsync(int vaultId, int userId);

    /// <summary>
    /// Checks if a user is the owner of a vault.
    /// </summary>
    Task<bool> IsOwnerAsync(int vaultId, int userId);
}

/// <summary>
/// DTO for vault data transfer.
/// </summary>
public record VaultDto(
    int Id,
    string Name,
    string? Description,
    string? Icon,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    int UserId,
    bool IsOwner
);
