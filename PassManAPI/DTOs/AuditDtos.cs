using PassManAPI.Models;

namespace PassManAPI.DTOs;

/// <summary>
/// DTO for returning audit log entries.
/// </summary>
public record AuditLogDto(
    int Id,
    AuditAction Action,
    string ActionName,
    string? EntityType,
    int? EntityId,
    string? Details,
    int UserId,
    string? UserEmail,
    int? VaultId,
    string? VaultName,
    int? CredentialId,
    string? CredentialTitle,
    string? IpAddress,
    string? UserAgent,
    DateTime Timestamp
);

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
