using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PassManAPI.DTOs;
using PassManAPI.Managers;
using PassManAPI.Models;

namespace PassManAPI.Controllers;

/// <summary>
/// Controller for managing audit logs.
/// </summary>
[ApiController]
[Route("api/audit")]
[Authorize]
public class AuditController : ControllerBase
{
    private readonly IAuditService _auditService;

    public AuditController(IAuditService auditService)
    {
        _auditService = auditService;
    }

    /// <summary>
    /// Gets the current user's audit logs with optional filtering and pagination.
    /// </summary>
    /// <param name="action">Filter by action type.</param>
    /// <param name="startDate">Filter logs after this date.</param>
    /// <param name="endDate">Filter logs before this date.</param>
    /// <param name="vaultId">Filter by vault ID.</param>
    /// <param name="credentialId">Filter by credential ID.</param>
    /// <param name="page">Page number (default: 1).</param>
    /// <param name="pageSize">Items per page (default: 20, max: 100).</param>
    /// <response code="200">Returns the paginated list of audit logs.</response>
    /// <response code="401">If the user is not authenticated.</response>
    [HttpGet("logs")]
    [ProducesResponseType(typeof(PaginatedAuditResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyLogs(
        [FromQuery] AuditAction? action = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] int? vaultId = null,
        [FromQuery] int? credentialId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized();

        var filter = new AuditLogFilter
        {
            Action = action,
            StartDate = startDate,
            EndDate = endDate,
            VaultId = vaultId,
            CredentialId = credentialId
        };

        var result = await _auditService.GetUserAuditLogsAsync(userId.Value, filter, page, pageSize);

        if (!result.Success)
            return BadRequest(new { error = result.Error });

        return Ok(result.Data);
    }

    /// <summary>
    /// Gets a specific audit log entry by ID.
    /// </summary>
    /// <param name="id">The audit log ID.</param>
    /// <response code="200">Returns the audit log entry.</response>
    /// <response code="403">If access is denied.</response>
    /// <response code="404">If the audit log is not found.</response>
    [HttpGet("logs/{id:int}")]
    [ProducesResponseType(typeof(AuditLogDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLogById(int id)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized();

        var hasAuditRead = HasPermission(PermissionConstants.AuditRead);

        var result = await _auditService.GetAuditLogByIdAsync(id, userId.Value, hasAuditRead);

        if (!result.Success)
        {
            if (result.Error == "Audit log not found")
                return NotFound(new { error = result.Error });
            return Forbid();
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Gets audit logs for a specific vault.
    /// Requires vault ownership or share access.
    /// </summary>
    /// <param name="vaultId">The vault ID.</param>
    /// <param name="action">Filter by action type.</param>
    /// <param name="startDate">Filter logs after this date.</param>
    /// <param name="endDate">Filter logs before this date.</param>
    /// <param name="page">Page number (default: 1).</param>
    /// <param name="pageSize">Items per page (default: 20, max: 100).</param>
    /// <response code="200">Returns the paginated list of vault audit logs.</response>
    /// <response code="403">If access to the vault is denied.</response>
    /// <response code="404">If the vault is not found.</response>
    [HttpGet("logs/vault/{vaultId:int}")]
    [ProducesResponseType(typeof(PaginatedAuditResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetVaultLogs(
        int vaultId,
        [FromQuery] AuditAction? action = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized();

        var filter = new AuditLogFilter
        {
            Action = action,
            StartDate = startDate,
            EndDate = endDate
        };

        var result = await _auditService.GetVaultAuditLogsAsync(vaultId, userId.Value, filter, page, pageSize);

        if (!result.Success)
        {
            if (result.Error == "Vault not found")
                return NotFound(new { error = result.Error });
            if (result.Error == "Access denied to vault audit logs")
                return Forbid();
            return BadRequest(new { error = result.Error });
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Gets all audit logs (admin only).
    /// Requires audit.read permission.
    /// </summary>
    /// <param name="userId">Filter by user ID.</param>
    /// <param name="action">Filter by action type.</param>
    /// <param name="startDate">Filter logs after this date.</param>
    /// <param name="endDate">Filter logs before this date.</param>
    /// <param name="vaultId">Filter by vault ID.</param>
    /// <param name="credentialId">Filter by credential ID.</param>
    /// <param name="page">Page number (default: 1).</param>
    /// <param name="pageSize">Items per page (default: 20, max: 100).</param>
    /// <response code="200">Returns the paginated list of all audit logs.</response>
    /// <response code="403">If the user doesn't have audit.read permission.</response>
    [HttpGet("logs/all")]
    [Authorize(Policy = PermissionConstants.AuditRead)]
    [ProducesResponseType(typeof(PaginatedAuditResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAllLogs(
        [FromQuery] int? userId = null,
        [FromQuery] AuditAction? action = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] int? vaultId = null,
        [FromQuery] int? credentialId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var filter = new AuditLogFilter
        {
            Action = action,
            StartDate = startDate,
            EndDate = endDate,
            VaultId = vaultId,
            CredentialId = credentialId
        };

        // If userId is specified, get that user's logs; otherwise get all
        if (userId.HasValue)
        {
            var result = await _auditService.GetUserAuditLogsAsync(userId.Value, filter, page, pageSize);
            if (!result.Success)
                return BadRequest(new { error = result.Error });
            return Ok(result.Data);
        }
        else
        {
            var result = await _auditService.GetAllAuditLogsAsync(filter, page, pageSize);
            if (!result.Success)
                return BadRequest(new { error = result.Error });
            return Ok(result.Data);
        }
    }

    private int? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdClaim, out var userId) ? userId : null;
    }

    private bool HasPermission(string permission)
    {
        return User.Claims.Any(c => c.Type == PermissionConstants.ClaimType && c.Value == permission);
    }
}
