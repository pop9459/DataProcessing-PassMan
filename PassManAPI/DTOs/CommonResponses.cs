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

/// <summary>
/// Generic message response (replaces anonymous <c>{ message }</c> bodies so they serialize as XML too).
/// </summary>
public class MessageResponse
{
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Response for accepting an invitation.
/// </summary>
public class InvitationAcceptedResponse
{
    public string Message { get; set; } = string.Empty;
    public int VaultId { get; set; }
}

/// <summary>
/// Response carrying a credential's decrypted password (kept as a single <c>password</c> field
/// for backward compatibility, now XML-serializable).
/// </summary>
public class PasswordResponse
{
    public string Password { get; set; } = string.Empty;
}
