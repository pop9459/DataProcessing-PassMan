using PassManAPI.Models;

namespace PassManAPI.DTOs;

/// <summary>
/// DTO for returning audit log entries.
/// </summary>
public record AuditLogDto
{
    public int Id { get; init; }
    public AuditAction Action { get; init; }
    public string ActionName { get; init; } = string.Empty;
    public string? EntityType { get; init; }
    public int? EntityId { get; init; }
    public string? Details { get; init; }
    public int UserId { get; init; }
    public string? UserEmail { get; init; }
    public int? VaultId { get; init; }
    public string? VaultName { get; init; }
    public int? CredentialId { get; init; }
    public string? CredentialTitle { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public DateTime Timestamp { get; init; }
}

/// <summary>
/// Filter parameters for querying audit logs.
/// </summary>
public class AuditLogFilter
{
    /// <summary>
    /// Filter by action type.
    /// </summary>
    public AuditAction? Action { get; set; }

    /// <summary>
    /// Filter logs after this date (inclusive).
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// Filter logs before this date (inclusive).
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Filter by specific vault ID.
    /// </summary>
    public int? VaultId { get; set; }

    /// <summary>
    /// Filter by specific credential ID.
    /// </summary>
    public int? CredentialId { get; set; }

    /// <summary>
    /// Filter by entity type (e.g., "Vault", "Credential", "User").
    /// </summary>
    public string? EntityType { get; set; }
}

/// <summary>
/// Paginated result wrapper for audit logs.
/// </summary>
public class PaginatedAuditResult
{
    public IEnumerable<AuditLogDto> Items { get; set; } = new List<AuditLogDto>();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}
