using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PassManAPI.Data;
using PassManAPI.Models;
using PassManAPI.Services;

namespace PassManAPI.Controllers;

[ApiController]
[Route("api/vaults/{vaultId:int}/share")]
public class VaultSharesController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ILookupNormalizer _normalizer;
    private readonly IAuditLogger _auditLogger;

    public VaultSharesController(ApplicationDbContext db, ILookupNormalizer normalizer, IAuditLogger auditLogger)
    {
        _db = db;
        _normalizer = normalizer;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// Share a vault with another user (view/edit/admin). Admin or owner can share.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(VaultShareResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ShareVault(
        int vaultId,
        [FromHeader(Name = "X-UserId")] int? currentUserId,
        [FromBody] ShareVaultRequest request
    )
    {
        if (currentUserId is null)
        {
            return Unauthorized("Missing X-UserId header (dev placeholder auth).");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var vault = await _db.Vaults
            .Include(v => v.SharedUsers)
            .AsTracking()
            .FirstOrDefaultAsync(v => v.Id == vaultId);
        if (vault is null)
        {
            return NotFound("Vault not found.");
        }

        var canShare = vault.UserId == currentUserId
            || await _db.VaultShares.AnyAsync(vs =>
                vs.VaultId == vaultId
                && vs.UserId == currentUserId
                && vs.Permission == SharePermission.Admin
            );
        if (!canShare)
        {
            return StatusCode(StatusCodes.Status403Forbidden, "You do not have permission to share this vault.");
        }

        var normalizedEmail =
            _normalizer.NormalizeEmail(request.Email.Trim()) ?? request.Email.Trim().ToUpperInvariant();
        var targetUser = await _db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail);
        if (targetUser is null)
        {
            return NotFound("Target user not found.");
        }

        if (targetUser.Id == vault.UserId)
        {
            return BadRequest("Owner already has access.");
        }

        var share = await _db.VaultShares.FirstOrDefaultAsync(vs =>
            vs.VaultId == vaultId && vs.UserId == targetUser.Id
        );

        if (share is null)
        {
            share = new VaultShare
            {
                VaultId = vaultId,
                UserId = targetUser.Id,
                Permission = request.Permission,
                SharedAt = DateTime.UtcNow,
                SharedByUserId = currentUserId
            };
            _db.VaultShares.Add(share);
        }
        else
        {
            share.Permission = request.Permission;
            share.SharedByUserId = currentUserId;
            share.SharedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        await _auditLogger.LogAsync(
            currentUserId.Value,
            AuditAction.VaultShared,
            "VaultShare",
            vaultId,
            $"Shared with user {targetUser.Id} as {share.Permission}"
        );

        return Ok(ToResponse(share, targetUser));
    }

    /// <summary>
    /// Revoke a user's access to a vault. Admin or owner can revoke.
    /// </summary>
    [HttpDelete("{userId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeShare(
        int vaultId,
        int userId,
        [FromHeader(Name = "X-UserId")] int? currentUserId
    )
    {
        if (currentUserId is null)
        {
            return Unauthorized("Missing X-UserId header (dev placeholder auth).");
        }

        var vault = await _db.Vaults.AsNoTracking().FirstOrDefaultAsync(v => v.Id == vaultId);
        if (vault is null)
        {
            return NotFound("Vault not found.");
        }

        var canShare = vault.UserId == currentUserId
            || await _db.VaultShares.AnyAsync(vs =>
                vs.VaultId == vaultId
                && vs.UserId == currentUserId
                && vs.Permission == SharePermission.Admin
            );
        if (!canShare)
        {
            return StatusCode(StatusCodes.Status403Forbidden, "You do not have permission to revoke access.");
        }

        var share = await _db.VaultShares.FirstOrDefaultAsync(vs =>
            vs.VaultId == vaultId && vs.UserId == userId
        );
        if (share is null)
        {
            return NotFound("Share not found.");
        }

        _db.VaultShares.Remove(share);
        await _db.SaveChangesAsync();
        await _auditLogger.LogAsync(
            currentUserId.Value,
            AuditAction.VaultShareRevoked,
            "VaultShare",
            vaultId,
            $"Revoked user {userId}"
        );
        return NoContent();
    }

    private static VaultShareResponse ToResponse(VaultShare share, User user) =>
        new(
            share.VaultId,
            user.Id,
            user.Email ?? string.Empty,
            share.Permission,
            share.SharedAt,
            share.SharedByUserId
        );

    public class ShareVaultRequest
    {
        [Required]
        [EmailAddress]
        [StringLength(256)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [EnumDataType(typeof(SharePermission))]
        public SharePermission Permission { get; set; } = SharePermission.View;
    }

    public record VaultShareResponse(
        int VaultId,
        int UserId,
        string Email,
        SharePermission Permission,
        DateTime SharedAt,
        int? SharedByUserId
    );
}
