using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PassManAPI.Data;
using PassManAPI.DTOs;
using PassManAPI.Helpers;
using PassManAPI.Models;
using PassManAPI.Services;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;

namespace PassManAPI.Controllers;

[ApiController]
[Route("api/vaults/{vaultId:int}/credentials")]
public class CredentialsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IPasswordEncryptionService _encryptionService;
    private readonly string _masterKey;

    public CredentialsController(
        ApplicationDbContext db,
        IPasswordEncryptionService encryptionService,
        IOptions<EncryptionOptions> encryptionOptions)
    {
        _db = db;
        _encryptionService = encryptionService;
        _masterKey = encryptionOptions.Value.MasterKey;
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
        if (!User.TryGetCurrentUserId(out var currentUserId))
        {
            return this.UnauthorizedProblem();
        }

        var access = await CheckVaultAccessAsync(vaultId, currentUserId);
        if (access is not null)
        {
            return access;
        }

        var items = await _db.Credentials
            .AsNoTracking()
            .Where(c => c.VaultId == vaultId)
            .Include(c => c.CredentialTags)
                .ThenInclude(ct => ct.Tag)
            .Select(c => new CredentialListItemDto
            {
                Id = c.Id,
                Title = c.Title,
                Username = c.Username,
                Url = c.Url,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt,
                LastAccessed = c.LastAccessed,
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
    /// <param name="request">The credential object to be created. The password within this object will be encrypted.</param>
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

        if (!User.TryGetCurrentUserId(out var currentUserId))
        {
            return this.UnauthorizedProblem();
        }

        var access = await CheckVaultModifyAccessAsync(vaultId, currentUserId);
        if (access is not null)
        {
            return access;
        }

        // Generate a per-credential key (32 bytes) and encrypt the plaintext password with it.
        // Note: request.EncryptedPassword contains the plaintext password from the client.
        var perCredentialKey = _encryptionService.GeneratePerCredentialKey();
        var encryptedPasswordBytes = _encryptionService.EncryptPassword(request.EncryptedPassword, perCredentialKey);
        var encryptedPasswordBase64 = Convert.ToBase64String(encryptedPasswordBytes);

        // Wrap the per-credential key under the server master key so it is never persisted in the
        // clear. Stored as "{wrappedKey}:{ciphertext}" (both Base64).
        var wrappedKeyBase64 = Convert.ToBase64String(
            _encryptionService.EncryptPerCredentialKey(perCredentialKey, _masterKey));

        var credential = new Credential
        {
            Title = request.Title.Trim(),
            Username = string.IsNullOrWhiteSpace(request.Username) ? null : request.Username.Trim(),
            EncryptedPassword = $"{wrappedKeyBase64}:{encryptedPasswordBase64}",
            Url = string.IsNullOrWhiteSpace(request.Url) ? null : request.Url.Trim(),
            Notes = request.Notes,
            CategoryId = request.CategoryId,
            VaultId = vaultId,
            CreatedAt = DateTime.UtcNow
        };

        _db.Credentials.Add(credential);
        await _db.SaveChangesAsync();

        return Created($"/api/vaults/{vaultId}/credentials/{credential.Id}", new IdResponse { Id = credential.Id });
    }

    /// <summary>
    /// Retrieves a single credential by its ID (without the password).
    /// </summary>
    /// <param name="id">The unique identifier of the credential.</param>
    /// <response code="200">Returns the credential detail.</response>
    /// <response code="401">If the user is not authenticated.</response>
    /// <response code="403">If the user does not have access to the vault that owns this credential.</response>
    /// <response code="404">If the credential with the specified ID is not found.</response>
    [HttpGet("/api/credentials/{id:int}")]
    [Authorize(Policy = PermissionConstants.CredentialRead)]
    [ProducesResponseType(typeof(CredentialDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        if (!User.TryGetCurrentUserId(out var currentUserId))
        {
            return this.UnauthorizedProblem();
        }

        var credential = await _db.Credentials
            .AsNoTracking()
            .Include(c => c.Category)
            .Include(c => c.CredentialTags)
                .ThenInclude(ct => ct.Tag)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (credential is null)
        {
            return this.NotFoundProblem("Credential not found.");
        }

        var canAccess = await CanAccessVault(credential.VaultId, currentUserId);
        if (!canAccess)
        {
            return this.ForbiddenProblem();
        }

        var dto = new CredentialDto
        {
            Id = credential.Id,
            Title = credential.Title,
            Username = credential.Username,
            Url = credential.Url,
            Notes = credential.Notes,
            CategoryId = credential.CategoryId,
            CategoryName = credential.Category?.Name,
            VaultId = credential.VaultId,
            CreatedAt = credential.CreatedAt,
            UpdatedAt = credential.UpdatedAt,
            LastAccessed = credential.LastAccessed,
            Tags = credential.CredentialTags
                .Select(ct => new TagDto(ct.Tag.Id, ct.Tag.Name))
                .ToList()
        };

        return Ok(dto);
    }

    /// <summary>
    /// Retrieves the decrypted password for a specific credential.
    /// </summary>
    /// <remarks>
    /// This endpoint returns the plaintext password after decryption.
    /// Use with caution and ensure secure transmission.
    /// </remarks>
    /// <param name="id">The unique identifier of the credential.</param>
    /// <response code="200">Returns the decrypted password.</response>
    /// <response code="401">If the user is not authenticated.</response>
    /// <response code="403">If the user does not have permission to access the credential.</response>
    /// <response code="404">If the credential with the specified ID is not found.</response>
    [HttpGet("/api/credentials/{id:int}/password")]
    [Authorize(Policy = PermissionConstants.CredentialRead)]
    public async Task<IActionResult> GetPassword(int id)
    {
        if (!User.TryGetCurrentUserId(out var currentUserId))
        {
            return this.UnauthorizedProblem();
        }

        var credential = await _db.Credentials
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id);

        if (credential is null)
        {
            return this.NotFoundProblem("Credential not found.");
        }

        var canAccess = await CanAccessVault(credential.VaultId, currentUserId);
        if (!canAccess)
        {
            return this.ForbiddenProblem();
        }

        // Update last accessed timestamp
        var credentialToUpdate = await _db.Credentials.FindAsync(id);
        if (credentialToUpdate != null)
        {
            credentialToUpdate.LastAccessed = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        // Stored as "{wrappedKey}:{ciphertext}" (both Base64). Unwrap the per-credential key with
        // the server master key, then decrypt the password with it.
        var parts = credential.EncryptedPassword.Split(':', 2);
        if (parts.Length != 2)
        {
            // Handle legacy format (unencrypted)
            return Ok(new PasswordResponse { Password = credential.EncryptedPassword });
        }

        var wrappedKeyBytes = Convert.FromBase64String(parts[0]);
        var encryptedPasswordBytes = Convert.FromBase64String(parts[1]);

        var perCredentialKey = _encryptionService.DecryptPerCredentialKey(wrappedKeyBytes, _masterKey);
        var decryptedPassword = _encryptionService.DecryptPassword(encryptedPasswordBytes, perCredentialKey);

        return Ok(new PasswordResponse { Password = decryptedPassword });
    }

    /// <summary>
    /// Updates an existing credential.
    /// </summary>
    /// <remarks>
    /// This endpoint updates the details of an existing credential identified by its ID.
    /// Any sensitive information will be re-encrypted upon update.
    /// </remarks>
    /// <param name="id">The unique identifier of the credential to update.</param>
    /// <param name="update">The updated credential object.</param>
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

        if (!User.TryGetCurrentUserId(out var currentUserId))
        {
            return this.UnauthorizedProblem();
        }

        var credential = await _db.Credentials.FirstOrDefaultAsync(c => c.Id == id);
        if (credential is null)
        {
            return this.NotFoundProblem("Credential not found.");
        }

        var canModify = await CanModifyVault(credential.VaultId, currentUserId);
        if (!canModify)
        {
            return this.ForbiddenProblem();
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
    /// Updates the password of an existing credential.
    /// </summary>
    /// <remarks>
    /// This endpoint updates only the password field of a credential.
    /// The new password will be encrypted before storage.
    /// </remarks>
    /// <param name="id">The unique identifier of the credential to update.</param>
    /// <param name="request">The new password (plaintext).</param>
    /// <response code="204">If the password was updated successfully.</response>
    /// <response code="400">If the provided password data is invalid.</response>
    /// <response code="401">If the user is not authenticated.</response>
    /// <response code="403">If the user does not have permission to modify the credential.</response>
    /// <response code="404">If the credential with the specified ID is not found.</response>
    [HttpPut("/api/credentials/{id:int}/password")]
    [Authorize(Policy = PermissionConstants.CredentialUpdate)]
    public async Task<IActionResult> UpdatePassword(int id, [FromBody] UpdateCredentialPasswordRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (!User.TryGetCurrentUserId(out var currentUserId))
        {
            return this.UnauthorizedProblem();
        }

        var credential = await _db.Credentials.FirstOrDefaultAsync(c => c.Id == id);
        if (credential is null)
        {
            return this.NotFoundProblem("Credential not found.");
        }

        var canModify = await CanModifyVault(credential.VaultId, currentUserId);
        if (!canModify)
        {
            return this.ForbiddenProblem();
        }

        // Generate a new per-credential key, encrypt the new password with it, and wrap the key
        // under the server master key so it is never persisted in the clear.
        var perCredentialKey = _encryptionService.GeneratePerCredentialKey();
        var encryptedPasswordBytes = _encryptionService.EncryptPassword(request.EncryptedPassword, perCredentialKey);
        var encryptedPasswordBase64 = Convert.ToBase64String(encryptedPasswordBytes);
        var wrappedKeyBase64 = Convert.ToBase64String(
            _encryptionService.EncryptPerCredentialKey(perCredentialKey, _masterKey));

        credential.EncryptedPassword = $"{wrappedKeyBase64}:{encryptedPasswordBase64}";
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
        if (!User.TryGetCurrentUserId(out var currentUserId))
        {
            return this.UnauthorizedProblem();
        }

        var credential = await _db.Credentials.FirstOrDefaultAsync(c => c.Id == id);
        if (credential is null)
        {
            return this.NotFoundProblem("Credential not found.");
        }

        var canModify = await CanModifyVault(credential.VaultId, currentUserId);
        if (!canModify)
        {
            return this.ForbiddenProblem();
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
        if (!User.TryGetCurrentUserId(out var currentUserId))
        {
            return this.UnauthorizedProblem();
        }

        var credential = await _db.Credentials
            .AsNoTracking()
            .Include(c => c.CredentialTags)
                .ThenInclude(ct => ct.Tag)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (credential is null)
        {
            return this.NotFoundProblem("Credential not found.");
        }

        var canAccess = await CanAccessVault(credential.VaultId, currentUserId);
        if (!canAccess)
        {
            return this.ForbiddenProblem();
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
        request.TagIds ??= new();

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (!User.TryGetCurrentUserId(out var currentUserId))
        {
            return this.UnauthorizedProblem();
        }

        var credential = await _db.Credentials
            .Include(c => c.CredentialTags)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (credential is null)
        {
            return this.NotFoundProblem("Credential not found.");
        }

        var canModify = await CanModifyVault(credential.VaultId, currentUserId);
        if (!canModify)
        {
            return this.ForbiddenProblem();
        }

        // Validate all tag ids belong to the current user.
        // Pomelo MySQL EF Core 9 preview does not support primitive-collection Contains in LINQ-to-SQL,
        // so fetch all of the user's tag IDs first and validate in memory.
        var userTagIds = await _db.Tags
            .AsNoTracking()
            .Where(t => t.UserId == currentUserId)
            .Select(t => t.Id)
            .ToListAsync();

        var invalidTagIds = request.TagIds.Except(userTagIds).ToList();
        if (invalidTagIds.Any())
        {
            return this.BadRequestProblem($"Invalid or unauthorized tag ids: {string.Join(", ", invalidTagIds)}");
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
        if (!User.TryGetCurrentUserId(out var currentUserId))
        {
            return this.UnauthorizedProblem();
        }

        var credential = await _db.Credentials.FirstOrDefaultAsync(c => c.Id == id);
        if (credential is null)
        {
            return this.NotFoundProblem("Credential not found.");
        }

        var canModify = await CanModifyVault(credential.VaultId, currentUserId);
        if (!canModify)
        {
            return this.ForbiddenProblem();
        }

        var tag = await _db.Tags.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tagId);
        if (tag is null)
        {
            return this.NotFoundProblem("Tag not found.");
        }

        if (tag.UserId != currentUserId)
        {
            return this.BadRequestProblem("Tag does not belong to the current user.");
        }

        var existing = await _db.CredentialTags
            .AnyAsync(ct => ct.CredentialId == id && ct.TagId == tagId);
        if (existing)
        {
            return this.BadRequestProblem("Tag is already assigned to this credential.");
        }

        _db.CredentialTags.Add(new CredentialTag(id, tagId));
        await _db.SaveChangesAsync();

        return Ok(new MessageResponse { Message = "Tag added successfully." });
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
        if (!User.TryGetCurrentUserId(out var currentUserId))
        {
            return this.UnauthorizedProblem();
        }

        var credential = await _db.Credentials.FirstOrDefaultAsync(c => c.Id == id);
        if (credential is null)
        {
            return this.NotFoundProblem("Credential not found.");
        }

        var canModify = await CanModifyVault(credential.VaultId, currentUserId);
        if (!canModify)
        {
            return this.ForbiddenProblem();
        }

        var credentialTag = await _db.CredentialTags
            .FirstOrDefaultAsync(ct => ct.CredentialId == id && ct.TagId == tagId);
        if (credentialTag is null)
        {
            return this.NotFoundProblem("Tag is not assigned to this credential.");
        }

        _db.CredentialTags.Remove(credentialTag);
        await _db.SaveChangesAsync();

        return NoContent();
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

    /// <summary>
    /// Whether the user may modify the vault's contents. The owner always may; a user the vault is
    /// shared with may only when their share grants Edit or Admin — a View share is read-only.
    /// Read access (CanAccessVault) is deliberately broader than modify access.
    /// </summary>
    private async Task<bool> CanModifyVault(int vaultId, int currentUserId)
    {
        var isOwner = await _db.Vaults.AsNoTracking().AnyAsync(v => v.Id == vaultId && v.UserId == currentUserId);
        if (isOwner)
        {
            return true;
        }

        return await _db.VaultShares.AsNoTracking()
            .AnyAsync(vs => vs.VaultId == vaultId
                            && vs.UserId == currentUserId
                            && vs.Permission >= SharePermission.Edit);
    }

    /// <summary>
    /// Returns null when the user may access the vault; otherwise the appropriate error result:
    /// 404 when the vault does not exist, 403 when it exists but is not accessible. Mirrors
    /// VaultsController so a missing vault and an unauthorized one are distinguished consistently.
    /// </summary>
    private async Task<IActionResult?> CheckVaultAccessAsync(int vaultId, int currentUserId)
    {
        if (await CanAccessVault(vaultId, currentUserId))
        {
            return null;
        }

        var vaultExists = await _db.Vaults.AsNoTracking().AnyAsync(v => v.Id == vaultId);
        return vaultExists ? this.ForbiddenProblem() : this.NotFoundProblem("Vault not found.");
    }

    /// <summary>
    /// Like <see cref="CheckVaultAccessAsync"/> but for write operations: returns null only when the
    /// user may modify the vault. A user with read-only (View) access to a shared vault receives 403.
    /// </summary>
    private async Task<IActionResult?> CheckVaultModifyAccessAsync(int vaultId, int currentUserId)
    {
        if (await CanModifyVault(vaultId, currentUserId))
        {
            return null;
        }

        var vaultExists = await _db.Vaults.AsNoTracking().AnyAsync(v => v.Id == vaultId);
        return vaultExists ? this.ForbiddenProblem() : this.NotFoundProblem("Vault not found.");
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