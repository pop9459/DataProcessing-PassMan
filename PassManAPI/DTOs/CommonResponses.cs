namespace PassManAPI.DTOs;

/// <summary>
/// Response containing a simple ID.
/// </summary>
public record IdResponse
{
    public int Id { get; init; }
}

/// <summary>
/// Response for successful vault share operation.
/// </summary>
public record VaultShareResponse
{
    public int VaultId { get; init; }
    public string TargetUser { get; init; } = string.Empty;
}

/// <summary>
/// Response for successful tag assignment.
/// </summary>
public record TagAssignmentResponse
{
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// Response for role assignment operation.
/// </summary>
public record RoleAssignmentResponse
{
    public int UserId { get; init; }
    public string Role { get; init; } = string.Empty;
}
