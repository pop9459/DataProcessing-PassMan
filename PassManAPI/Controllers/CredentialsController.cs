using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PassManAPI.Data;
using PassManAPI.DTOs;
using PassManAPI.Models;
using System.Security.Claims;
using System.ComponentModel.DataAnnotations;

namespace PassManAPI.Controllers;

[ApiController]
[Route("api/vaults/{vaultId:int}/credentials")]
public class CredentialsController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public CredentialsController(ApplicationDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Retrieves all credentials stored within a specific vault.
    /// </summary>
    /// <remarks>
    /// This endpoint returns a list of credentials associated with the given vault ID.
    /// The actual passwords are not returned for security reasons; only metadata is provided.
    /// </remarks>
    /// <param name="vaultId">The unique identifier of the vault.</param>
    /// <response code="200">Returns the list of credentials successfully.</response>
    /// <response code="401">If the user is not authenticated.</response>
    /// <response code="403">If the user does not have permission to access the vault.</response>
    /// <response code="404">If the vault with the specified ID is not found.</response>
    // GET /api/vaults/{vaultId}/credentials
    [HttpGet]
    [Route("/api/vaults/{vaultId}/credentials")]
    [Authorize(Policy = PermissionConstants.CredentialRead)]
    public async Task<IActionResult> Get(int vaultId)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var canAccess = await CanAccessVault(vaultId, currentUserId);
        if (!canAccess)
        {
            return Forbid();
        }

        var items = await _db.Credentials
            .AsNoTracking()
            .Where(c => c.VaultId == vaultId)
            .Include(c => c.CredentialTags)
                .ThenInclude(ct => ct.Tag)
            .Select(c => new
            {
                c.Id,
                c.Title,
                c.Username,
                c.Url,
                c.CreatedAt,
                c.UpdatedAt,
                c.LastAccessed,
                Tags = c.CredentialTags.Select(ct => new TagDto(ct.Tag.Id, ct.Tag.Name)).ToList()
            })
            .ToListAsync();

        return Ok(items);
    }

    /// <summary>
    /// Adds a new credential to a specific vault.
    /// </summary>
    /// <remarks>
    /// This endpoint creates a new credential and associates it with the given vault ID.
    /// The provided credential data will be encrypted before being stored.
    /// </remarks>
    /// <param name="vaultId">The unique identifier of the vault where the credential will be stored.</param>
    /// <param name="credential">The credential object to be created. The password within this object will be encrypted.</param>
    /// <response code="201">Returns the newly created credential's location.</response>
    /// <response code="400">If the provided credential data is invalid.</response>
    /// <response code="401">If the user is not authenticated.</response>
    /// <response code="403">If the user does not have permission to add a credential to the vault.</response>
    /// <response code="404">If the vault with the specified ID is not found.</response>
    // POST /api/vaults/{vaultId}/credentials
    [HttpPost]
    [Route("/api/vaults/{vaultId}/credentials")]
    [Authorize(Policy = PermissionConstants.CredentialCreate)]
    public async Task<IActionResult> Post(int vaultId, [FromBody] CreateCredentialRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var canAccess = await CanAccessVault(vaultId, currentUserId);
        if (!canAccess)
        {
            return Forbid();
        }

        var credential = new Credential
        {
            Title = request.Title.Trim(),
            Username = string.IsNullOrWhiteSpace(request.Username) ? null : request.Username.Trim(),
            EncryptedPassword = request.EncryptedPassword,
            Url = string.IsNullOrWhiteSpace(request.Url) ? null : request.Url.Trim(),
            Notes = request.Notes,
            CategoryId = request.CategoryId,
            VaultId = vaultId,
            CreatedAt = DateTime.UtcNow
        };

        _db.Credentials.Add(credential);
        await _db.SaveChangesAsync();

        return Created($"/api/vaults/{vaultId}/credentials/{credential.Id}", new { credential.Id });
    }

    /// <summary>
    /// Updates an existing credential.
    /// </summary>
    /// <remarks>
    /// This endpoint updates the details of an existing credential identified by its ID.
    /// Any sensitive information will be re-encrypted upon update.
    /// </remarks>
    /// <param name="id">The unique identifier of the credential to update.</param>
    /// <param name="credential">The updated credential object.</param>
    /// <response code="204">If the credential was updated successfully.</response>
    /// <response code="400">If the provided credential data is invalid.</response>
    /// <response code="401">If the user is not authenticated.</response>
    /// <response code="403">If the user does not have permission to modify the credential.</response>
    /// <response code="404">If the credential with the specified ID is not found.</response>
    // PUT /api/credentials/{id}
    [HttpPut("/api/credentials/{id:int}")]
    [Authorize(Policy = PermissionConstants.CredentialUpdate)]
    public async Task<IActionResult> Put(int id, [FromBody] UpdateCredentialRequest update)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var credential = await _db.Credentials.FirstOrDefaultAsync(c => c.Id == id);
        if (credential is null)
        {
            return NotFound();
        }

        var canAccess = await CanAccessVault(credential.VaultId, currentUserId);
        if (!canAccess)
        {
            return Forbid();
        }

        credential.Title = update.Title.Trim();
        credential.Username = string.IsNullOrWhiteSpace(update.Username) ? null : update.Username.Trim();
        credential.Url = string.IsNullOrWhiteSpace(update.Url) ? null : update.Url.Trim();
        credential.Notes = update.Notes;
        credential.CategoryId = update.CategoryId;
        credential.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Deletes a specific credential.
    /// </summary>
    /// <remarks>
    /// This endpoint permanently deletes a credential identified by its ID. This action cannot be undone.
    /// </remarks>
    /// <param name="id">The unique identifier of the credential to delete.</param>
    /// <response code="204">If the credential was deleted successfully.</response>
    /// <response code="401">If the user is not authenticated.</response>
    /// <response code="403">If the user does not have permission to delete the credential.</response>
    /// <response code="404">If the credential with the specified ID is not found.</response>
    // DELETE /api/credentials/{id}
    [HttpDelete("/api/credentials/{id:int}")]
    [Authorize(Policy = PermissionConstants.CredentialDelete)]
    public async Task<IActionResult> Delete(int id)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var credential = await _db.Credentials.FirstOrDefaultAsync(c => c.Id == id);
        if (credential is null)
        {
            return NotFound();
        }

        var canAccess = await CanAccessVault(credential.VaultId, currentUserId);
        if (!canAccess)
        {
            return Forbid();
        }

        _db.Credentials.Remove(credential);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Gets all tags assigned to a credential.
    /// </summary>
    /// <param name="id">The credential id.</param>
    /// <response code="200">Returns the list of tags.</response>
    /// <response code="401">If the user is not authenticated.</response>
    /// <response code="403">If the user does not have permission to access the credential.</response>
    /// <response code="404">If the credential is not found.</response>
    [HttpGet("/api/credentials/{id:int}/tags")]
    [Authorize(Policy = PermissionConstants.CredentialRead)]
    [ProducesResponseType(typeof(IEnumerable<TagDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCredentialTags(int id)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var credential = await _db.Credentials
            .AsNoTracking()
            .Include(c => c.CredentialTags)
                .ThenInclude(ct => ct.Tag)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (credential is null)
        {
            return NotFound();
        }

        var canAccess = await CanAccessVault(credential.VaultId, currentUserId);
        if (!canAccess)
        {
            return Forbid();
        }

        var tags = credential.CredentialTags
            .Select(ct => new TagDto(ct.Tag.Id, ct.Tag.Name))
            .ToList();

        return Ok(tags);
    }

    /// <summary>
    /// Assigns tags to a credential. Replaces all existing tag assignments.
    /// </summary>
    /// <param name="id">The credential id.</param>
    /// <param name="request">The list of tag ids to assign.</param>
    /// <response code="200">Tags assigned successfully.</response>
    /// <response code="400">If any tag id is invalid or does not belong to the user.</response>
    /// <response code="401">If the user is not authenticated.</response>
    /// <response code="403">If the user does not have permission to modify the credential.</response>
    /// <response code="404">If the credential is not found.</response>
    [HttpPut("/api/credentials/{id:int}/tags")]
    [Authorize(Policy = PermissionConstants.CredentialUpdate)]
    [ProducesResponseType(typeof(IEnumerable<TagDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetCredentialTags(int id, [FromBody] AssignTagsRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var credential = await _db.Credentials
            .Include(c => c.CredentialTags)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (credential is null)
        {
            return NotFound();
        }

        var canAccess = await CanAccessVault(credential.VaultId, currentUserId);
        if (!canAccess)
        {
            return Forbid();
        }

        // Validate all tag ids belong to the current user
        var validTagIds = await _db.Tags
            .AsNoTracking()
            .Where(t => t.UserId == currentUserId && request.TagIds.Contains(t.Id))
            .Select(t => t.Id)
            .ToListAsync();

        var invalidTagIds = request.TagIds.Except(validTagIds).ToList();
        if (invalidTagIds.Any())
        {
            return BadRequest($"Invalid or unauthorized tag ids: {string.Join(", ", invalidTagIds)}");
        }

        // Remove existing tags
        _db.CredentialTags.RemoveRange(credential.CredentialTags);

        // Add new tags
        foreach (var tagId in request.TagIds.Distinct())
        {
            _db.CredentialTags.Add(new CredentialTag(credential.Id, tagId));
        }

        await _db.SaveChangesAsync();

        // Return updated tags
        var tags = await _db.CredentialTags
            .AsNoTracking()
            .Where(ct => ct.CredentialId == id)
            .Include(ct => ct.Tag)
            .Select(ct => new TagDto(ct.Tag.Id, ct.Tag.Name))
            .ToListAsync();

        return Ok(tags);
    }

    /// <summary>
    /// Adds a single tag to a credential.
    /// </summary>
    /// <param name="id">The credential id.</param>
    /// <param name="tagId">The tag id to add.</param>
    /// <response code="200">Tag added successfully.</response>
    /// <response code="400">If the tag is already assigned or does not belong to the user.</response>
    /// <response code="401">If the user is not authenticated.</response>
    /// <response code="403">If the user does not have permission to modify the credential.</response>
    /// <response code="404">If the credential or tag is not found.</response>
    [HttpPost("/api/credentials/{id:int}/tags/{tagId:int}")]
    [Authorize(Policy = PermissionConstants.CredentialUpdate)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddTagToCredential(int id, int tagId)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var credential = await _db.Credentials.FirstOrDefaultAsync(c => c.Id == id);
        if (credential is null)
        {
            return NotFound("Credential not found.");
        }

        var canAccess = await CanAccessVault(credential.VaultId, currentUserId);
        if (!canAccess)
        {
            return Forbid();
        }

        var tag = await _db.Tags.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tagId);
        if (tag is null)
        {
            return NotFound("Tag not found.");
        }

        if (tag.UserId != currentUserId)
        {
            return BadRequest("Tag does not belong to the current user.");
        }

        var existing = await _db.CredentialTags
            .AnyAsync(ct => ct.CredentialId == id && ct.TagId == tagId);
        if (existing)
        {
            return BadRequest("Tag is already assigned to this credential.");
        }

        _db.CredentialTags.Add(new CredentialTag(id, tagId));
        await _db.SaveChangesAsync();

        return Ok(new { message = "Tag added successfully." });
    }

    /// <summary>
    /// Removes a tag from a credential.
    /// </summary>
    /// <param name="id">The credential id.</param>
    /// <param name="tagId">The tag id to remove.</param>
    /// <response code="204">Tag removed successfully.</response>
    /// <response code="401">If the user is not authenticated.</response>
    /// <response code="403">If the user does not have permission to modify the credential.</response>
    /// <response code="404">If the credential or tag assignment is not found.</response>
    [HttpDelete("/api/credentials/{id:int}/tags/{tagId:int}")]
    [Authorize(Policy = PermissionConstants.CredentialUpdate)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveTagFromCredential(int id, int tagId)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var credential = await _db.Credentials.FirstOrDefaultAsync(c => c.Id == id);
        if (credential is null)
        {
            return NotFound("Credential not found.");
        }

        var canAccess = await CanAccessVault(credential.VaultId, currentUserId);
        if (!canAccess)
        {
            return Forbid();
        }

        var credentialTag = await _db.CredentialTags
            .FirstOrDefaultAsync(ct => ct.CredentialId == id && ct.TagId == tagId);
        if (credentialTag is null)
        {
            return NotFound("Tag is not assigned to this credential.");
        }

        _db.CredentialTags.Remove(credentialTag);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    private bool TryGetCurrentUserId(out int userId)
    {
        userId = 0;
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return claim != null && int.TryParse(claim.Value, out userId);
    }

    private async Task<bool> CanAccessVault(int vaultId, int currentUserId)
    {
        var isOwner = await _db.Vaults.AsNoTracking().AnyAsync(v => v.Id == vaultId && v.UserId == currentUserId);
        if (isOwner)
        {
            return true;
        }

        var isShared = await _db.VaultShares.AsNoTracking().AnyAsync(vs => vs.VaultId == vaultId && vs.UserId == currentUserId);
        return isShared;
    }

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
}