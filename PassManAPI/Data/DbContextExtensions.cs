using PassManAPI.Models;

namespace PassManAPI.Data;

public static class DbContextExtensions
{
    public static async Task AddAuditLogAsync(
        this ApplicationDbContext db,
        AuditAction action,
        int userId,
        string? entityType,
        int? entityId,
        string? details)
    {
        db.AuditLogs.Add(new AuditLog
        {
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = details,
            UserId = userId,
            Timestamp = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }
}
