using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PassManAPI.Data;
using PassManAPI.Models;

namespace PassManAPI.Services;

public interface IAuditLogger
{
    Task LogAsync(
        int userId,
        AuditAction action,
        string? entityType = null,
        int? entityId = null,
        string? details = null
    );
}

public class AuditLogger : IAuditLogger
{
    private readonly ApplicationDbContext _db;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditLogger(ApplicationDbContext db, IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task LogAsync(
        int userId,
        AuditAction action,
        string? entityType = null,
        int? entityId = null,
        string? details = null
    )
    {
        try
        {
            var ctx = _httpContextAccessor.HttpContext;
            var ip = ctx?.Connection.RemoteIpAddress?.ToString();
            var ua = ctx?.Request.Headers.UserAgent.ToString();

            _db.AuditLogs.Add(
                new AuditLog
                {
                    UserId = userId,
                    Action = action,
                    EntityType = entityType,
                    EntityId = entityId,
                    Details = details,
                    IpAddress = ip,
                    UserAgent = ua,
                    Timestamp = DateTime.UtcNow
                }
            );

            await _db.SaveChangesAsync();
        }
        catch
        {
            // Swallow logging failures to avoid breaking main flow.
        }
    }
}

