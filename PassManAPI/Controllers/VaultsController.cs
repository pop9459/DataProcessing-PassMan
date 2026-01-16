using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PassManAPI.Data;
using PassManAPI.Models;
using PassManAPI.Services;

namespace PassManAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VaultsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditLogger _auditLogger;

    public VaultsController(ApplicationDbContext db, IAuditLogger auditLogger)
    {
        _db = db;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// Lists vaults for a given user.
    /// </summary>
    /// <param name="userId">Owner user id. If omitted, all vaults are returned (dev-only behavior).</param>
    /// <response code="200">Returns the list of vaults.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<VaultResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<VaultResponse>>> GetVaults([FromQuery] int? userId)
    {
        // Include vaults the user owns or that are shared with them
        var query = _db.Vaults
            .AsNoTracking()
            .Include(v => v.SharedUsers)
            .AsQueryable();

        if (userId.HasValue)
        {
            var uid = userId.Value;
            query = query.Where(v => v.UserId == uid || v.SharedUsers.Any(su => su.UserId == uid));
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
    [ProducesResponseType(typeof(VaultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VaultResponse>> GetVault(int id)
    {
        var vault = await _db.Vaults.AsNoTracking().FirstOrDefaultAsync(v => v.Id == id);
        if (vault is null)
        {
            return NotFound();
        }

        return Ok(ToResponse(vault));
    }

    /// <summary>
    /// Creates a new vault for a user.
    /// </summary>
    /// <response code="201">Vault created.</response>
    /// <response code="400">Invalid payload or unknown user.</response>
    [HttpPost]
    [ProducesResponseType(typeof(VaultResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<VaultResponse>> CreateVault([FromBody] CreateVaultRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
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

        await _auditLogger.LogAsync(request.UserId, AuditAction.VaultCreated, "Vault", vault.Id, $"Vault created: {vault.Name}");

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
    [ProducesResponseType(typeof(VaultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VaultResponse>> UpdateVault(int id, [FromBody] UpdateVaultRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var vault = await _db.Vaults.FirstOrDefaultAsync(v => v.Id == id);
        if (vault is null)
        {
            return NotFound();
        }

        vault.Name = request.Name.Trim();
        vault.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        vault.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        await _auditLogger.LogAsync(vault.UserId, AuditAction.VaultUpdated, "Vault", vault.Id, $"Vault updated: {vault.Name}");

        return Ok(ToResponse(vault));
    }

    /// <summary>
    /// Deletes a vault and its contents.
    /// </summary>
    /// <response code="204">Vault deleted.</response>
    /// <response code="404">Vault not found.</response>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteVault(int id)
    {
        var vault = await _db.Vaults.FirstOrDefaultAsync(v => v.Id == id);
        if (vault is null)
        {
            return NotFound();
        }

        _db.Vaults.Remove(vault);
        await _db.SaveChangesAsync();
        await _auditLogger.LogAsync(vault.UserId, AuditAction.VaultDeleted, "Vault", vault.Id, $"Vault deleted: {vault.Name}");
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
}
