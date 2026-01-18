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

public record UserProfileResponse
{
    public int Id { get; init; }
    public string Email { get; init; } = string.Empty;
    public string? UserName { get; init; }
    public string? PhoneNumber { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public DateTime? LastLoginAt { get; init; }
    public string? EncryptedVaultKey { get; init; }
    public Guid? SubscriptionTierId { get; init; }
}

/// <summary>
/// Authentication response payload; accessToken is a placeholder until JWT is added.
/// </summary>
public record AuthResponse
{
    public string AccessToken { get; init; } = string.Empty;
    public UserProfileResponse User { get; init; } = new();
}

