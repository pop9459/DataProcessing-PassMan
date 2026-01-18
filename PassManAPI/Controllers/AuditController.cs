using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PassManAPI.Data;
using PassManAPI.DTOs;
using PassManAPI.Models;
using System.Security.Claims;

namespace PassManAPI.Controllers;

[ApiController]
[Route("api/audit")]
public class AuditController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public AuditController(ApplicationDbContext db)
    {
        _db = db;
    }
    /// <summary>
    /// Retrieves audit logs.
    /// </summary>
    /// <remarks>
    /// Returns a list of security and access logs for the user's vaults.
    /// </remarks>
    /// <response code="200">Returns the list of audit logs.</response>
    /// <response code="401">If the user is not authenticated.</response>
    [HttpGet("logs")]
    [Authorize(Policy = PermissionConstants.AuditRead)]
    [ProducesResponseType(typeof(IEnumerable<AuditLogDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IEnumerable<AuditLogDto>>> GetLogs()
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var logs = await _db.AuditLogs
            .AsNoTracking()
            .Where(l => l.UserId == currentUserId)
            .OrderByDescending(l => l.Timestamp)
            .Select(l => new AuditLogDto(
                l.Id,
                l.Action.ToString(),
                l.EntityType,
                l.EntityId,
                l.Details,
                l.Timestamp
            ))
            .ToListAsync();

        return Ok(logs);
    }

    private bool TryGetCurrentUserId(out int userId)
    {
        userId = 0;
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return claim != null && int.TryParse(claim.Value, out userId);
    }
}
