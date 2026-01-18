namespace PassManGUI.Models;

/// <summary>
/// Vault response from API
/// </summary>
public class VaultResponse
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public int UserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsOwner { get; set; }
}

/// <summary>
/// Request to create a new vault
/// </summary>
public class CreateVaultRequest
{
    public required string Name { get; set; }
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public int UserId { get; set; }
}

/// <summary>
/// Request to update an existing vault
/// </summary>
public class UpdateVaultRequest
{
    public required string Name { get; set; }
    public string? Description { get; set; }
    public string? Icon { get; set; }
}

/// <summary>
/// Vault item (credential) model - maps to API response
/// </summary>
public class VaultItemModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? Password { get; set; } // For form input - maps to EncryptedPassword in API
    public string? Url { get; set; }
    public string? Notes { get; set; }
    public int VaultId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? LastAccessed { get; set; }
    public List<TagDto>? Tags { get; set; }
    
    // For UI compatibility - map Title to Name
    public string Name => Title;
    public string? Label => Tags?.FirstOrDefault()?.Name;
    
    public string FormatTimeAgo()
    {
        return FormatTimeAgo(CreatedAt);
    }

    private static string FormatTimeAgo(DateTime date)
    {
        var timeSpan = DateTime.UtcNow - date;
        if (timeSpan.TotalDays > 365)
            return $"{(int)(timeSpan.TotalDays / 365)} years ago";
        if (timeSpan.TotalDays > 30)
            return $"{(int)(timeSpan.TotalDays / 30)} months ago";
        if (timeSpan.TotalDays > 1)
            return $"{(int)timeSpan.TotalDays} days ago";
        if (timeSpan.TotalHours > 1)
            return $"{(int)timeSpan.TotalHours} hours ago";
        if (timeSpan.TotalMinutes > 1)
            return $"{(int)timeSpan.TotalMinutes} minutes ago";
        return "just now";
    }
}

/// <summary>
/// Tag DTO
/// </summary>
public class TagDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    
    public TagDto() { }
    public TagDto(int id, string name) { Id = id; Name = name; }
}

/// <summary>
/// Request to create a new credential
/// </summary>
public class CreateVaultItemRequest
{
    public required string Title { get; set; }
    public string? Username { get; set; }
    public string? EncryptedPassword { get; set; }
    public string? Url { get; set; }
    public string? Notes { get; set; }
    public int? CategoryId { get; set; }
}

/// <summary>
/// Request to update an existing credential
/// </summary>
public class UpdateVaultItemRequest
{
    public string? Title { get; set; }
    public string? Username { get; set; }
    public string? EncryptedPassword { get; set; }
    public string? Url { get; set; }
    public string? Notes { get; set; }
}
