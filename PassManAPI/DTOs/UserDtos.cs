using System.ComponentModel.DataAnnotations;
using PassManAPI.Validation;

namespace PassManAPI.DTOs;

public class RegisterRequest
{
    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 8)]
    [PasswordComplexity]
    public string Password { get; set; } = string.Empty;

    [Required]
    [Compare(nameof(Password), ErrorMessage = "Passwords must match.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [StringLength(256)]
    public string? UserName { get; set; }

    [Phone]
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// Encrypted per-user vault key, if the client provides it at registration time.
    /// </summary>
    public string? EncryptedVaultKey { get; set; }
}

public class LoginRequest
{
    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public class UpdateProfileRequest
{
    [EmailAddress]
    [StringLength(256)]
    public string? Email { get; set; }

    [StringLength(256)]
    public string? UserName { get; set; }

    [Phone]
    public string? PhoneNumber { get; set; }

    public string? EncryptedVaultKey { get; set; }
}

public record UserProfileResponse(
    int Id,
    string Email,
    string? UserName,
    string? PhoneNumber,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? LastLoginAt,
    string? EncryptedVaultKey,
    Guid? SubscriptionTierId
);

/// <summary>
/// Authentication response payload; accessToken is a placeholder until JWT is added.
/// </summary>
public record AuthResponse(
    string AccessToken,
    UserProfileResponse User
);

