using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PassManAPI.Data;
using PassManAPI.Models;

namespace PassManAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VaultsController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public VaultsController(ApplicationDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Lists vaults for a given user.
    /// </summary>
    /// <param name="userId">Owner user id. If omitted, all vaults are returned (dev-only behavior).</param>
    /// <response code="200">Returns the list of vaults.</response>
    [HttpGet]
    [Authorize(Policy = PermissionConstants.VaultRead)]
    [ProducesResponseType(typeof(IEnumerable<VaultResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<VaultResponse>>> GetVaults([FromQuery] int? userId)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var query = _db.Vaults.AsNoTracking().Where(v => v.UserId == currentUserId);

        // Include shares where current user is a recipient
        var sharedVaultIds = await _db.VaultShares
            .AsNoTracking()
            .Where(vs => vs.UserId == currentUserId)
            .Select(vs => vs.VaultId)
            .ToListAsync();

        if (sharedVaultIds.Count > 0)
        {
            query = query.Union(_db.Vaults.AsNoTracking().Where(v => sharedVaultIds.Contains(v.Id)));
        }

        var items = await query
            .OrderBy(v => v.Id)
            .Select(v => ToResponse(v))
            .ToListAsync();

        return Ok(items);
    }

    /// <summary>
    /// Retrieves a single vault by id.
    /// </summary>
    /// <response code="200">Vault found.</response>
    /// <response code="404">Vault not found.</response>
    [HttpGet("{id:int}")]
    [Authorize(Policy = PermissionConstants.VaultRead)]
    [ProducesResponseType(typeof(VaultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VaultResponse>> GetVault(int id)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var vault = await _db.Vaults.AsNoTracking().FirstOrDefaultAsync(v => v.Id == id);
        if (vault is null)
        {
            return NotFound();
        }

        var canAccess = vault.UserId == currentUserId ||
            await _db.VaultShares.AsNoTracking().AnyAsync(vs => vs.VaultId == id && vs.UserId == currentUserId);
        if (!canAccess)
        {
            return Forbid();
        }

        return Ok(ToResponse(vault));
    }

    /// <summary>
    /// Creates a new vault for a user.
    /// </summary>
    /// <response code="201">Vault created.</response>
    /// <response code="400">Invalid payload or unknown user.</response>
    [HttpPost]
    [Authorize(Policy = PermissionConstants.VaultCreate)]
    [ProducesResponseType(typeof(VaultResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<VaultResponse>> CreateVault([FromBody] CreateVaultRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        if (request.UserId != currentUserId)
        {
            return Forbid();
        }

        var userExists = await _db.Users.AsNoTracking().AnyAsync(u => u.Id == request.UserId);
        if (!userExists)
        {
            return BadRequest($"User with id {request.UserId} does not exist.");
        }

        var vault = new Vault
        {
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            UserId = request.UserId,
            CreatedAt = DateTime.UtcNow
        };

        _db.Vaults.Add(vault);
        await _db.SaveChangesAsync();

        await LogAudit(AuditAction.VaultCreated, currentUserId, nameof(Vault), vault.Id, $"Vault '{vault.Name}' created");

        var response = ToResponse(vault);
        return CreatedAtAction(nameof(GetVault), new { id = vault.Id }, response);
    }

    /// <summary>
    /// Updates an existing vault's metadata.
    /// </summary>
    /// <response code="200">Vault updated.</response>
    /// <response code="400">Invalid payload.</response>
    /// <response code="404">Vault not found.</response>
    [HttpPut("{id:int}")]
    [Authorize(Policy = PermissionConstants.VaultUpdate)]
    [ProducesResponseType(typeof(VaultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VaultResponse>> UpdateVault(int id, [FromBody] UpdateVaultRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var vault = await _db.Vaults.FirstOrDefaultAsync(v => v.Id == id);
        if (vault is null)
        {
            return NotFound();
        }

        if (vault.UserId != currentUserId)
        {
            return Forbid();
        }

        vault.Name = request.Name.Trim();
        vault.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        vault.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        await LogAudit(AuditAction.VaultUpdated, currentUserId, nameof(Vault), vault.Id, $"Vault '{vault.Name}' updated");

        return Ok(ToResponse(vault));
    }

    /// <summary>
    /// Deletes a vault and its contents.
    /// </summary>
    /// <response code="204">Vault deleted.</response>
    /// <response code="404">Vault not found.</response>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = PermissionConstants.VaultDelete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteVault(int id)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var vault = await _db.Vaults.FirstOrDefaultAsync(v => v.Id == id);
        if (vault is null)
        {
            return NotFound();
        }

        if (vault.UserId != currentUserId)
        {
            return Forbid();
        }

        _db.Vaults.Remove(vault);
        await _db.SaveChangesAsync();
        await LogAudit(AuditAction.VaultDeleted, currentUserId, nameof(Vault), vault.Id, $"Vault '{vault.Name}' deleted");
        return NoContent();
    }

    private static VaultResponse ToResponse(Vault v) =>
        new()
        {
            Id = v.Id,
            Name = v.Name,
            Description = v.Description,
            CreatedAt = v.CreatedAt,
            UpdatedAt = v.UpdatedAt,
            UserId = v.UserId
        };

    public class CreateVaultRequest
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        /// <summary>User id (owner). Replace with auth context once JWT is in place.</summary>
        [Required]
        public int UserId { get; set; }
    }

    public class UpdateVaultRequest
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }
    }

    public class VaultResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int UserId { get; set; }
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
