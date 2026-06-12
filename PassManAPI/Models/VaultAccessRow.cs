namespace PassManAPI.Models;

/// <summary>
/// Keyless entity mapped to the vwUserVaultAccess database view.
/// Represents a single row showing which user has access to which vault and how (Owner or Shared).
/// </summary>
public class VaultAccessRow
{
    public int VaultId { get; set; }
    public string VaultName { get; set; } = string.Empty;
    public int OwnerId { get; set; }
    public int AccessUserId { get; set; }

    /// <summary>"Owner" or "Shared"</summary>
    public string AccessType { get; set; } = string.Empty;
}
