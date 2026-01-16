namespace PassManGUI.Models;

/// <summary>
/// Request model for user login
/// </summary>
public class LoginRequest
{
    public required string Email { get; set; }
    public required string Password { get; set; }
}

/// <summary>
/// Request model for user registration
/// </summary>
public class RegisterRequest
{
    public required string Email { get; set; }
    public required string Password { get; set; }
    public required string ConfirmPassword { get; set; }
    public string? UserName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? EncryptedVaultKey { get; set; }
}

/// <summary>
/// Response from authentication endpoints (login/register)
/// </summary>
public class AuthResponse
{
    public required string AccessToken { get; set; }
    public required UserProfile User { get; set; }
}

/// <summary>
/// User profile information
/// </summary>
public class UserProfile
{
    public int Id { get; set; }
    public required string Email { get; set; }
    public string? UserName { get; set; }
    public string? PhoneNumber { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public string? EncryptedVaultKey { get; set; }
}
