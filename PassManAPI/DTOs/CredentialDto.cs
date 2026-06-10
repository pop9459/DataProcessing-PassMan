using System.ComponentModel.DataAnnotations;

namespace PassManAPI.DTOs;

/// <summary>
/// Lightweight credential response used in list endpoints (no notes/category/vaultId).
/// </summary>
public class CredentialListItemDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? Url { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? LastAccessed { get; set; }
    public List<TagDto> Tags { get; set; } = new();
}

/// <summary>
/// Response DTO for Credential information.
/// Note: Does not include the encrypted password for security reasons.
/// </summary>
public record CredentialDto
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Username { get; init; }
    public string? Url { get; init; }
    public string? Notes { get; init; }
    public int? CategoryId { get; init; }
    public string? CategoryName { get; init; }
    public int VaultId { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public DateTime? LastAccessed { get; init; }
    public List<TagDto> Tags { get; init; } = new();
}

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
public record DecryptedPasswordDto
{
    public int CredentialId { get; init; }
    public string DecryptedPassword { get; init; } = string.Empty;
}
