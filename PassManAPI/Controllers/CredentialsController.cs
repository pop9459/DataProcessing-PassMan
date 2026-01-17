using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PassManAPI.Data;
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
            .Select(c => new
            {
                c.Id,
                c.Title,
                c.Username,
                c.Url,
                c.CreatedAt,
                c.UpdatedAt,
                c.LastAccessed
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