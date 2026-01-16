namespace PassManGUI.Models;

/// <summary>
/// Vault response from API
/// </summary>
public class VaultResponse
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public int UserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Vault item (credential) model
/// </summary>
public class VaultItemModel
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? Url { get; set; }
    public string? Notes { get; set; }
    public string? Label { get; set; }
    public int VaultId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
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
/// Request to create a new vault item
/// </summary>
public class CreateVaultItemRequest
{
    public required string Name { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? Url { get; set; }
    public string? Notes { get; set; }
    public int VaultId { get; set; }
}

/// <summary>
/// Request to update an existing vault item
/// </summary>
public class UpdateVaultItemRequest
{
    public string? Name { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? Url { get; set; }
    public string? Notes { get; set; }
}
