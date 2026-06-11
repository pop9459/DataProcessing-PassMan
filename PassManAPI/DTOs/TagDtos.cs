using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace PassManAPI.DTOs;

/// <summary>
/// Response DTO for Tag information.
/// A class (not a positional record) with a parameterless constructor so it is XML-serializable
/// by XmlSerializer; the (id, name) constructor keeps existing call sites and EF projections working.
/// </summary>
public class TagDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public TagDto() { }

    public TagDto(int id, string name)
    {
        Id = id;
        Name = name;
    }
}

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
    [ValidateNever]
    public List<int> TagIds { get; set; } = new();
}
