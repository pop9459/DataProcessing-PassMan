using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PassManAPI.Data;
using PassManAPI.DTOs;
using PassManAPI.Helpers;
using PassManAPI.Models;

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
    /// The insert is performed via the sp_AddVaultShare stored procedure, which validates
    /// vault/user existence and uses INSERT IGNORE for idempotency.
    /// </remarks>
    /// <param name="vaultId">The unique identifier of the vault.</param>
    /// <param name="request">The sharing details (user email).</param>
    /// <response code="200">If the share is successful.</response>
    /// <response code="400">If the request is invalid.</response>
    /// <response code="401">If the user is not authenticated.</response>
    /// <response code="403">If the user does not have permission to share the vault.</response>
    /// <response code="404">If the vault or user is not found.</response>
    [HttpPost]
    [Authorize(Policy = PermissionConstants.VaultShare)]
    public async Task<IActionResult> ShareVault(int vaultId, [FromBody] ShareRequest request)
    {
        if (!User.TryGetCurrentUserId(out var currentUserId))
        {
            return this.UnauthorizedProblem();
        }

        var vault = await _db.Vaults.FirstOrDefaultAsync(v => v.Id == vaultId);
        if (vault is null)
        {
            return this.NotFoundProblem("Vault not found.");
        }

        if (vault.UserId != currentUserId)
        {
            return this.ForbiddenProblem();
        }

        var targetUser = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.UserEmail);
        if (targetUser is null)
        {
            return this.NotFoundProblem("Target user not found.");
        }

        if (_db.Database.IsMySql())
        {
            // On MySQL: delegate to sp_AddVaultShare. The procedure resolves the user by email,
            // validates vault ownership, and uses INSERT IGNORE for idempotency.
            await _db.Database.ExecuteSqlRawAsync(
                "CALL sp_AddVaultShare({0}, {1})",
                vaultId,
                request.UserEmail);
        }
        else
        {
            // Fallback for SQLite (tests): direct EF insert.
            var shareExists = await _db.VaultShares.AnyAsync(vs => vs.VaultId == vaultId && vs.UserId == targetUser.Id);
            if (!shareExists)
            {
                _db.VaultShares.Add(new VaultShare { VaultId = vaultId, UserId = targetUser.Id });
                await _db.SaveChangesAsync();
            }
        }

        return Ok(new VaultShareResponse { VaultId = vaultId, TargetUser = targetUser.Email! });
    }

    /// <summary>
    /// Lists all vaults the current user can access (owned or shared), using the
    /// vwUserVaultAccess database view.
    /// </summary>
    /// <response code="200">A list of vault access rows for the current user.</response>
    /// <response code="401">If the user is not authenticated.</response>
    [HttpGet("/api/vaults/my-access")]
    [Authorize]
    public async Task<IActionResult> GetMyVaultAccess()
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        if (!_db.Database.IsMySql())
            return StatusCode(501, "This endpoint requires MySQL.");

        // Query the vwUserVaultAccess view — returns all vaults this user owns or has been shared into.
        var accessible = await _db.VaultAccess
            .Where(va => va.AccessUserId == currentUserId)
            .ToListAsync();

        return Ok(accessible);
    }

    /// <summary>
    /// Revoke a user's access to a vault. Admin or owner can revoke.
    /// </summary>
    [HttpDelete("{userId}")]
    [Authorize(Policy = PermissionConstants.VaultShare)]
    public async Task<IActionResult> RevokeShare(int vaultId, int userId)
    {
        if (!User.TryGetCurrentUserId(out var currentUserId))
        {
            return this.UnauthorizedProblem();
        }

        var vault = await _db.Vaults.FirstOrDefaultAsync(v => v.Id == vaultId);
        if (vault is null)
        {
            return this.NotFoundProblem("Vault not found.");
        }

        if (vault.UserId != currentUserId)
        {
            return this.ForbiddenProblem();
        }

        var share = await _db.VaultShares.FirstOrDefaultAsync(vs => vs.VaultId == vaultId && vs.UserId == userId);
        if (share is null)
        {
            return this.NotFoundProblem("Share not found.");
        }

        _db.VaultShares.Remove(share);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    public class ShareRequest
    {
        public string UserEmail { get; set; } = string.Empty;
    }

}
