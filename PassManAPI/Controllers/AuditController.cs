using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PassManAPI.Data;
using PassManAPI.Models;

namespace PassManAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuditController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public AuditController(ApplicationDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Lists audit log entries. Uses X-UserId header as the caller identity in dev placeholder auth.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(AuditResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAuditLogs(
        [FromHeader(Name = "X-UserId")] int? currentUserId,
        [FromQuery] int? userId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20
    )
    {
        if (!userId.HasValue && currentUserId is null)
        {
            return Unauthorized("Missing X-UserId header (dev placeholder auth).");
        }

        if (page < 1 || pageSize < 1 || pageSize > 200)
        {
            return BadRequest("Invalid paging parameters.");
        }

        var targetUserId = userId ?? currentUserId!.Value;

        var query = _db.AuditLogs
            .AsNoTracking()
            .Where(a => a.UserId == targetUserId);

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(a => a.Timestamp)
            .ThenByDescending(a => a.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AuditItemResponse(
                a.Id,
                a.UserId,
                a.Action,
                a.EntityType,
                a.EntityId,
                a.Details,
                a.IpAddress,
                a.UserAgent,
                a.Timestamp
            ))
            .ToListAsync();

        var response = new AuditResponse(items, page, pageSize, total);
        return Ok(response);
    }

    public record AuditResponse(
        IEnumerable<AuditItemResponse> Items,
        int Page,
        int PageSize,
        int Total
    );

    public record AuditItemResponse(
        int Id,
        int UserId,
        AuditAction Action,
        string? EntityType,
        int? EntityId,
        string? Details,
        string? IpAddress,
        string? UserAgent,
        DateTime Timestamp
    );
}
