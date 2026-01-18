using System.ComponentModel.DataAnnotations;

namespace PassManAPI.DTOs;

/// <summary>
/// Response DTO for Credential information.
/// Note: Does not include the encrypted password for security reasons.
/// </summary>
public record CredentialDto(
    int Id,
    string Title,
    string? Username,
    string? Url,
    string? Notes,
    int? CategoryId,
    string? CategoryName,
    int VaultId,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? LastAccessed,
    List<TagDto> Tags
);

/// <summary>
/// Request DTO for creating a new credential.
/// </summary>
public class CreateCredentialRequest
{
    [Required]
    [MaxLength(255)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(255)]
    public string? Username { get; set; }

    [Required]
    public string EncryptedPassword { get; set; } = string.Empty;

    [MaxLength(500)]
    [Url]
    public string? Url { get; set; }

    public string? Notes { get; set; }

    public int? CategoryId { get; set; }
}

/// <summary>
/// Request DTO for updating an existing credential (without password change).
/// </summary>
public class UpdateCredentialRequest
{
    [Required]
    [MaxLength(255)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(255)]
    public string? Username { get; set; }

    [MaxLength(500)]
    [Url]
    public string? Url { get; set; }

    public string? Notes { get; set; }

    public int? CategoryId { get; set; }
}

/// <summary>
/// Request DTO for updating a credential's password.
/// </summary>
public class UpdateCredentialPasswordRequest
{
    [Required]
    public string EncryptedPassword { get; set; } = string.Empty;
}

/// <summary>
/// Response DTO for decrypted password.
/// </summary>
public record DecryptedPasswordDto(
    int CredentialId,
    string DecryptedPassword
);
