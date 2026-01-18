using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PassManAPI.Data;
using PassManAPI.DTOs;
using PassManAPI.Models;

namespace PassManAPI.Controllers;

/// <summary>
/// Controller for managing user tags.
/// Tags are user-scoped - each user can only see and manage their own tags.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class TagsController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public TagsController(ApplicationDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Lists all tags for the current user.
    /// </summary>
    /// <response code="200">Returns the list of tags.</response>
    /// <response code="401">If the user is not authenticated.</response>
    [HttpGet]
    [Authorize(Policy = PermissionConstants.TagRead)]
    [ProducesResponseType(typeof(IEnumerable<TagDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IEnumerable<TagDto>>> GetTags()
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var tags = await _db.Tags
            .AsNoTracking()
            .Where(t => t.UserId == currentUserId)
            .OrderBy(t => t.Name)
            .Select(t => new TagDto(t.Id, t.Name))
            .ToListAsync();

        return Ok(tags);
    }

    /// <summary>
    /// Retrieves a single tag by id.
    /// </summary>
    /// <param name="id">The tag id.</param>
    /// <response code="200">Tag found.</response>
    /// <response code="401">If the user is not authenticated.</response>
    /// <response code="403">If the user does not own the tag.</response>
    /// <response code="404">Tag not found.</response>
    [HttpGet("{id:int}")]
    [Authorize(Policy = PermissionConstants.TagRead)]
    [ProducesResponseType(typeof(TagDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TagDto>> GetTag(int id)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var tag = await _db.Tags.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
        if (tag is null)
        {
            return NotFound();
        }

        if (tag.UserId != currentUserId)
        {
            return Forbid();
        }

        return Ok(new TagDto(tag.Id, tag.Name));
    }

    /// <summary>
    /// Creates a new tag for the current user.
    /// </summary>
    /// <param name="request">The tag creation request.</param>
    /// <response code="201">Tag created.</response>
    /// <response code="400">Invalid payload or duplicate tag name.</response>
    /// <response code="401">If the user is not authenticated.</response>
    [HttpPost]
    [Authorize(Policy = PermissionConstants.TagCreate)]
    [ProducesResponseType(typeof(TagDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TagDto>> CreateTag([FromBody] CreateTagRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var trimmedName = request.Name.Trim();

        // Check for duplicate tag name for this user
        var duplicate = await _db.Tags.AsNoTracking()
            .AnyAsync(t => t.UserId == currentUserId && t.Name.ToLower() == trimmedName.ToLower());
        if (duplicate)
        {
            return BadRequest($"A tag with name '{trimmedName}' already exists.");
        }

        var tag = new Tag(trimmedName, currentUserId);

        _db.Tags.Add(tag);
        await _db.SaveChangesAsync();

        await LogAudit(AuditAction.TagCreated, currentUserId, nameof(Tag), tag.Id, $"Tag '{tag.Name}' created");

        var response = new TagDto(tag.Id, tag.Name);
        return CreatedAtAction(nameof(GetTag), new { id = tag.Id }, response);
    }

    /// <summary>
    /// Updates (renames) an existing tag.
    /// </summary>
    /// <param name="id">The tag id.</param>
    /// <param name="request">The tag update request.</param>
    /// <response code="200">Tag updated.</response>
    /// <response code="400">Invalid payload or duplicate tag name.</response>
    /// <response code="401">If the user is not authenticated.</response>
    /// <response code="403">If the user does not own the tag.</response>
    /// <response code="404">Tag not found.</response>
    [HttpPut("{id:int}")]
    [Authorize(Policy = PermissionConstants.TagUpdate)]
    [ProducesResponseType(typeof(TagDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TagDto>> UpdateTag(int id, [FromBody] UpdateTagRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var tag = await _db.Tags.FirstOrDefaultAsync(t => t.Id == id);
        if (tag is null)
        {
            return NotFound();
        }

        if (tag.UserId != currentUserId)
        {
            return Forbid();
        }

        var trimmedName = request.Name.Trim();

        // Check for duplicate tag name for this user (excluding current tag)
        var duplicate = await _db.Tags.AsNoTracking()
            .AnyAsync(t => t.UserId == currentUserId && t.Id != id && t.Name.ToLower() == trimmedName.ToLower());
        if (duplicate)
        {
            return BadRequest($"A tag with name '{trimmedName}' already exists.");
        }

        var oldName = tag.Name;
        tag.Rename(trimmedName);
        await _db.SaveChangesAsync();

        await LogAudit(AuditAction.TagUpdated, currentUserId, nameof(Tag), tag.Id, $"Tag renamed from '{oldName}' to '{tag.Name}'");

        return Ok(new TagDto(tag.Id, tag.Name));
    }

    /// <summary>
    /// Deletes a tag. Also removes all credential-tag associations.
    /// </summary>
    /// <param name="id">The tag id.</param>
    /// <response code="204">Tag deleted.</response>
    /// <response code="401">If the user is not authenticated.</response>
    /// <response code="403">If the user does not own the tag.</response>
    /// <response code="404">Tag not found.</response>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = PermissionConstants.TagDelete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTag(int id)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var tag = await _db.Tags
            .Include(t => t.CredentialTags)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (tag is null)
        {
            return NotFound();
        }

        if (tag.UserId != currentUserId)
        {
            return Forbid();
        }

        var tagName = tag.Name;

        // Remove all credential-tag associations first
        _db.CredentialTags.RemoveRange(tag.CredentialTags);
        _db.Tags.Remove(tag);
        await _db.SaveChangesAsync();

        await LogAudit(AuditAction.TagDeleted, currentUserId, nameof(Tag), id, $"Tag '{tagName}' deleted");

        return NoContent();
    }

    private bool TryGetCurrentUserId(out int userId)
    {
        userId = 0;
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return claim != null && int.TryParse(claim.Value, out userId);
    }

    private async Task LogAudit(AuditAction action, int userId, string? entityType, int? entityId, string? details)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = details,
            UserId = userId,
            Timestamp = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }
}
