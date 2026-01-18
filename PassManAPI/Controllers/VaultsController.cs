using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PassManAPI.Data;
using PassManAPI.Managers;
using PassManAPI.Models;

namespace PassManAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VaultsController : ControllerBase
{
    private readonly IVaultManager _vaultManager;
    private readonly ApplicationDbContext _db;

    public VaultsController(IVaultManager vaultManager, ApplicationDbContext db)
    {
        _vaultManager = vaultManager;
        _db = db;
    }

    /// <summary>
    /// Lists vaults for the current user (owned and shared).
    /// </summary>
    /// <param name="userId">Ignored. Vaults are filtered by authenticated user.</param>
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

        var result = await _vaultManager.GetUserVaultsAsync(currentUserId);
        if (!result.Success)
        {
            return BadRequest(result.Error);
        }

        var response = result.Data!.Select(ToResponse);
        return Ok(response);
    }

    /// <summary>
    /// Retrieves a single vault by id.
    /// </summary>
    /// <response code="200">Vault found.</response>
    /// <response code="403">Access denied.</response>
    /// <response code="404">Vault not found.</response>
    [HttpGet("{id:int}")]
    [Authorize(Policy = PermissionConstants.VaultRead)]
    [ProducesResponseType(typeof(VaultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VaultResponse>> GetVault(int id)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _vaultManager.GetVaultByIdAsync(id, currentUserId);
        if (!result.Success)
        {
            if (result.Error == "Vault not found.")
            {
                return NotFound();
            }
            if (result.Error == "Access denied.")
            {
                return Forbid();
            }
            return BadRequest(result.Error);
        }

        return Ok(ToResponse(result.Data!));
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

        // Ensure user can only create vaults for themselves
        if (request.UserId != currentUserId)
        {
            return Forbid();
        }

        var result = await _vaultManager.CreateVaultAsync(
            currentUserId,
            request.Name,
            request.Description,
            request.Icon);

        if (!result.Success)
        {
            return BadRequest(result.Error);
        }

        await LogAudit(AuditAction.VaultCreated, currentUserId, nameof(Vault), result.Data!.Id, $"Vault '{result.Data.Name}' created");

        var response = ToResponse(result.Data);
        return CreatedAtAction(nameof(GetVault), new { id = result.Data.Id }, response);
    }

    /// <summary>
    /// Updates an existing vault's metadata.
    /// </summary>
    /// <response code="200">Vault updated.</response>
    /// <response code="400">Invalid payload.</response>
    /// <response code="403">Only owner can update.</response>
    /// <response code="404">Vault not found.</response>
    [HttpPut("{id:int}")]
    [Authorize(Policy = PermissionConstants.VaultUpdate)]
    [ProducesResponseType(typeof(VaultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
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

        var result = await _vaultManager.UpdateVaultAsync(
            id,
            currentUserId,
            request.Name,
            request.Description,
            request.Icon);

        if (!result.Success)
        {
            if (result.Error == "Vault not found.")
            {
                return NotFound();
            }
            if (result.Error == "Only the vault owner can update the vault.")
            {
                return Forbid();
            }
            return BadRequest(result.Error);
        }

        await LogAudit(AuditAction.VaultUpdated, currentUserId, nameof(Vault), id, $"Vault '{result.Data!.Name}' updated");

        return Ok(ToResponse(result.Data));
    }

    /// <summary>
    /// Soft-deletes a vault (marks as deleted but retains in database).
    /// </summary>
    /// <response code="204">Vault deleted.</response>
    /// <response code="403">Only owner can delete.</response>
    /// <response code="404">Vault not found.</response>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = PermissionConstants.VaultDelete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteVault(int id)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        // Get vault name before deletion for audit log
        var vault = await _vaultManager.GetVaultByIdAsync(id, currentUserId);
        var vaultName = vault.Data?.Name ?? "Unknown";

        var result = await _vaultManager.DeleteVaultAsync(id, currentUserId);
        if (!result.Success)
        {
            if (result.Error == "Vault not found.")
            {
                return NotFound();
            }
            if (result.Error == "Only the vault owner can delete the vault.")
            {
                return Forbid();
            }
            return BadRequest(result.Error);
        }

        await LogAudit(AuditAction.VaultDeleted, currentUserId, nameof(Vault), id, $"Vault '{vaultName}' deleted (soft delete)");
        return NoContent();
    }

    private static VaultResponse ToResponse(VaultDto dto) =>
        new()
        {
            Id = dto.Id,
            Name = dto.Name,
            Description = dto.Description,
            Icon = dto.Icon,
            CreatedAt = dto.CreatedAt,
            UpdatedAt = dto.UpdatedAt,
            UserId = dto.UserId,
            IsOwner = dto.IsOwner
        };

    /// <summary>
    /// Request model for creating a vault.
    /// </summary>
    public class CreateVaultRequest
    {
        /// <summary>Name of the vault (required, max 100 chars).</summary>
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        /// <summary>Optional description (max 500 chars).</summary>
        [StringLength(500)]
        public string? Description { get; set; }

        /// <summary>Optional icon identifier (emoji or icon name, max 50 chars).</summary>
        [StringLength(50)]
        public string? Icon { get; set; }

        /// <summary>User id (owner). Replace with auth context once JWT is in place.</summary>
        [Required]
        public int UserId { get; set; }
    }

    /// <summary>
    /// Request model for updating a vault.
    /// </summary>
    public class UpdateVaultRequest
    {
        /// <summary>Name of the vault (required, max 100 chars).</summary>
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        /// <summary>Optional description (max 500 chars).</summary>
        [StringLength(500)]
        public string? Description { get; set; }

        /// <summary>Optional icon identifier (emoji or icon name, max 50 chars).</summary>
        [StringLength(50)]
        public string? Icon { get; set; }
    }

    /// <summary>
    /// Response model for vault data.
    /// </summary>
    public class VaultResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Icon { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int UserId { get; set; }
        public bool IsOwner { get; set; }
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
