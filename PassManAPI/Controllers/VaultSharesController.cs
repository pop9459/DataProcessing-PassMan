using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PassManAPI.Data;
using PassManAPI.Models;
using System.Security.Claims;

namespace PassManAPI.Controllers;

[ApiController]
[Route("api/vaults/{vaultId:int}/share")]
public class VaultSharesController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    public VaultSharesController(ApplicationDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Shares a vault with another user.
    /// </summary>
    /// <remarks>
    /// Grants access to a specific vault to another user via their email.
    /// </remarks>
    /// <param name="vaultId">The unique identifier of the vault.</param>
    /// <param name="request">The sharing details (user email, permission level).</param>
    /// <response code="200">If the share is successful.</response>
    /// <response code="400">If the request is invalid.</response>
    /// <response code="401">If the user is not authenticated.</response>
    /// <response code="403">If the user does not have permission to share the vault.</response>
    /// <response code="404">If the vault or user is not found.</response>
    [HttpPost]
    [Authorize(Policy = PermissionConstants.VaultShare)]
    public async Task<IActionResult> ShareVault(int vaultId, [FromBody] ShareRequest request)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var vault = await _db.Vaults.FirstOrDefaultAsync(v => v.Id == vaultId);
        if (vault is null)
        {
            return NotFound("Vault not found.");
        }

        if (vault.UserId != currentUserId)
        {
            return Forbid();
        }

        var targetUser = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.UserEmail);
        if (targetUser is null)
        {
            return NotFound("Target user not found.");
        }

        var shareExists = await _db.VaultShares.AnyAsync(vs => vs.VaultId == vaultId && vs.UserId == targetUser.Id);
        if (!shareExists)
        {
            _db.VaultShares.Add(new VaultShare
            {
                VaultId = vaultId,
                UserId = targetUser.Id
            });
            await _db.SaveChangesAsync();
        }

        return Ok(new { vaultId, targetUser = targetUser.Email });
    }

    /// <summary>
    /// Revoke a user's access to a vault. Admin or owner can revoke.
    /// </summary>
    [HttpDelete("{userId}")]
    [Authorize(Policy = PermissionConstants.VaultShare)]
    public async Task<IActionResult> RevokeShare(int vaultId, int userId)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var vault = await _db.Vaults.FirstOrDefaultAsync(v => v.Id == vaultId);
        if (vault is null)
        {
            return NotFound("Vault not found.");
        }

        if (vault.UserId != currentUserId)
        {
            return Forbid();
        }

        var share = await _db.VaultShares.FirstOrDefaultAsync(vs => vs.VaultId == vaultId && vs.UserId == userId);
        if (share is null)
        {
            return NotFound("Share not found.");
        }

        _db.VaultShares.Remove(share);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    public class ShareRequest
    {
        public string UserEmail { get; set; } = string.Empty;
    }

    private bool TryGetCurrentUserId(out int userId)
    {
        userId = 0;
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return claim != null && int.TryParse(claim.Value, out userId);
    }
}
