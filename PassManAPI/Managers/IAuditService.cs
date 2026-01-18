using PassManAPI.DTOs;
using PassManAPI.Models;

namespace PassManAPI.Managers;

/// <summary>
/// Result wrapper for audit operations.
/// </summary>
public class AuditOperationResult<T>
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public T? Data { get; init; }

    public static AuditOperationResult<T> Ok(T data) =>
        new() { Success = true, Data = data };

    public static AuditOperationResult<T> Fail(string error) =>
        new() { Success = false, Error = error };
}

/// <summary>
/// Interface for audit logging business logic operations.
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// Logs an audit event.
    /// </summary>
    /// <param name="userId">The user who performed the action.</param>
    /// <param name="action">The type of action performed.</param>
    /// <param name="ipAddress">Client IP address (optional).</param>
    /// <param name="userAgent">Client user agent (optional).</param>
    /// <param name="vaultId">Related vault ID (optional).</param>
    /// <param name="credentialId">Related credential ID (optional).</param>
    /// <param name="entityType">Generic entity type for backward compatibility (optional).</param>
    /// <param name="entityId">Generic entity ID for backward compatibility (optional).</param>
    /// <param name="details">Additional details about the action (optional).</param>
    Task<AuditOperationResult<AuditLogDto>> LogEventAsync(
        int userId,
        AuditAction action,
        string? ipAddress = null,
        string? userAgent = null,
        int? vaultId = null,
        int? credentialId = null,
        string? entityType = null,
        int? entityId = null,
        string? details = null);

    /// <summary>
    /// Gets audit logs for a specific user with optional filtering and pagination.
    /// </summary>
    /// <param name="userId">The user whose logs to retrieve.</param>
    /// <param name="filter">Optional filter parameters.</param>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Number of items per page.</param>
    Task<AuditOperationResult<PaginatedAuditResult>> GetUserAuditLogsAsync(
        int userId,
        AuditLogFilter? filter = null,
        int page = 1,
        int pageSize = 20);

    /// <summary>
    /// Gets audit logs for a specific vault. 
    /// Only accessible by vault owner or users with audit.read permission.
    /// </summary>
    /// <param name="vaultId">The vault to get logs for.</param>
    /// <param name="requestingUserId">The user making the request.</param>
    /// <param name="filter">Optional filter parameters.</param>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Number of items per page.</param>
    Task<AuditOperationResult<PaginatedAuditResult>> GetVaultAuditLogsAsync(
        int vaultId,
        int requestingUserId,
        AuditLogFilter? filter = null,
        int page = 1,
        int pageSize = 20);

    /// <summary>
    /// Gets a specific audit log entry by ID.
    /// </summary>
    /// <param name="logId">The audit log ID.</param>
    /// <param name="requestingUserId">The user making the request.</param>
    /// <param name="hasAuditReadPermission">Whether the user has audit.read permission.</param>
    Task<AuditOperationResult<AuditLogDto>> GetAuditLogByIdAsync(
        int logId,
        int requestingUserId,
        bool hasAuditReadPermission = false);

    /// <summary>
    /// Gets all audit logs (admin only) with optional filtering and pagination.
    /// </summary>
    /// <param name="filter">Optional filter parameters.</param>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Number of items per page.</param>
    Task<AuditOperationResult<PaginatedAuditResult>> GetAllAuditLogsAsync(
        AuditLogFilter? filter = null,
        int page = 1,
        int pageSize = 20);
}
