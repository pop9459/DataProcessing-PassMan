using System.ComponentModel.DataAnnotations;

namespace PassManAPI.DTOs;

/// <summary>
/// Response DTO for Tag information.
/// </summary>
public record TagDto(
    int Id,
    string Name
);

/// <summary>
/// Request DTO for creating a new tag.
/// </summary>
public class CreateTagRequest
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Request DTO for updating (renaming) a tag.
/// </summary>
public class UpdateTagRequest
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Request DTO for assigning tags to a credential.
/// </summary>
public class AssignTagsRequest
{
    [Required]
    public List<int> TagIds { get; set; } = new();
}
