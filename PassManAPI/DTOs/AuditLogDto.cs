using PassManAPI.Models;

namespace PassManAPI.DTOs;

public record AuditLogDto(
    int Id,
    string Action,
    string? EntityType,
    int? EntityId,
    string? Details,
    DateTime Timestamp
);
