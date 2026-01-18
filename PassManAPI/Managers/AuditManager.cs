using Microsoft.EntityFrameworkCore;
using PassManAPI.Data;
using PassManAPI.DTOs;
using PassManAPI.Models;

namespace PassManAPI.Managers;

/// <summary>
/// Implementation of IAuditService for managing audit logs.
/// </summary>
public class AuditManager : IAuditService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AuditManager> _logger;

    public AuditManager(ApplicationDbContext context, ILogger<AuditManager> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<AuditOperationResult<AuditLogDto>> LogEventAsync(
        int userId,
        AuditAction action,
        string? ipAddress = null,
        string? userAgent = null,
        int? vaultId = null,
        int? credentialId = null,
        string? entityType = null,
        int? entityId = null,
        string? details = null)
    {
        try
        {
            var auditLog = new AuditLog
            {
                UserId = userId,
                Action = action,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                VaultId = vaultId,
                CredentialId = credentialId,
                EntityType = entityType,
                EntityId = entityId,
                Details = details,
                Timestamp = DateTime.UtcNow
            };

            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Audit log created: {Action} by user {UserId}, VaultId: {VaultId}, CredentialId: {CredentialId}",
                action, userId, vaultId, credentialId);

            // Reload with navigation properties
            await _context.Entry(auditLog).Reference(a => a.User).LoadAsync();
            if (vaultId.HasValue)
                await _context.Entry(auditLog).Reference(a => a.Vault).LoadAsync();
            if (credentialId.HasValue)
                await _context.Entry(auditLog).Reference(a => a.Credential).LoadAsync();

            return AuditOperationResult<AuditLogDto>.Ok(MapToDto(auditLog));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create audit log for action {Action} by user {UserId}", action, userId);
            return AuditOperationResult<AuditLogDto>.Fail("Failed to create audit log");
        }
    }

    public async Task<AuditOperationResult<PaginatedAuditResult>> GetUserAuditLogsAsync(
        int userId,
        AuditLogFilter? filter = null,
        int page = 1,
        int pageSize = 20)
    {
        try
        {
            pageSize = Math.Clamp(pageSize, 1, 100);
            page = Math.Max(1, page);

            var query = _context.AuditLogs
                .Where(a => a.UserId == userId)
                .AsQueryable();

            query = ApplyFilters(query, filter);

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(a => a.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Include(a => a.User)
                .Include(a => a.Vault)
                .Include(a => a.Credential)
                .ToListAsync();

            return AuditOperationResult<PaginatedAuditResult>.Ok(new PaginatedAuditResult
            {
                Items = items.Select(MapToDto),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get audit logs for user {UserId}", userId);
            return AuditOperationResult<PaginatedAuditResult>.Fail("Failed to retrieve audit logs");
        }
    }

    public async Task<AuditOperationResult<PaginatedAuditResult>> GetVaultAuditLogsAsync(
        int vaultId,
        int requestingUserId,
        AuditLogFilter? filter = null,
        int page = 1,
        int pageSize = 20)
    {
        try
        {
            // Check if user has access to the vault
            var vault = await _context.Vaults
                .IgnoreQueryFilters() // Include soft-deleted vaults for audit history
                .FirstOrDefaultAsync(v => v.Id == vaultId);

            if (vault == null)
                return AuditOperationResult<PaginatedAuditResult>.Fail("Vault not found");

            // Check ownership or share access
            var hasAccess = vault.UserId == requestingUserId ||
                await _context.VaultShares.AnyAsync(vs => vs.VaultId == vaultId && vs.UserId == requestingUserId);

            if (!hasAccess)
                return AuditOperationResult<PaginatedAuditResult>.Fail("Access denied to vault audit logs");

            pageSize = Math.Clamp(pageSize, 1, 100);
            page = Math.Max(1, page);

            var query = _context.AuditLogs
                .Where(a => a.VaultId == vaultId)
                .AsQueryable();

            query = ApplyFilters(query, filter);

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(a => a.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Include(a => a.User)
                .Include(a => a.Vault)
                .Include(a => a.Credential)
                .ToListAsync();

            return AuditOperationResult<PaginatedAuditResult>.Ok(new PaginatedAuditResult
            {
                Items = items.Select(MapToDto),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get audit logs for vault {VaultId}", vaultId);
            return AuditOperationResult<PaginatedAuditResult>.Fail("Failed to retrieve vault audit logs");
        }
    }

    public async Task<AuditOperationResult<AuditLogDto>> GetAuditLogByIdAsync(
        int logId,
        int requestingUserId,
        bool hasAuditReadPermission = false)
    {
        try
        {
            var auditLog = await _context.AuditLogs
                .Include(a => a.User)
                .Include(a => a.Vault)
                .Include(a => a.Credential)
                .FirstOrDefaultAsync(a => a.Id == logId);

            if (auditLog == null)
                return AuditOperationResult<AuditLogDto>.Fail("Audit log not found");

            // Check access: user owns the log, has audit.read permission, or owns the related vault
            bool hasAccess = hasAuditReadPermission || auditLog.UserId == requestingUserId;

            if (!hasAccess && auditLog.VaultId.HasValue)
            {
                var vault = await _context.Vaults
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(v => v.Id == auditLog.VaultId);
                hasAccess = vault?.UserId == requestingUserId;
            }

            if (!hasAccess)
                return AuditOperationResult<AuditLogDto>.Fail("Access denied to audit log");

            return AuditOperationResult<AuditLogDto>.Ok(MapToDto(auditLog));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get audit log {LogId}", logId);
            return AuditOperationResult<AuditLogDto>.Fail("Failed to retrieve audit log");
        }
    }

    public async Task<AuditOperationResult<PaginatedAuditResult>> GetAllAuditLogsAsync(
        AuditLogFilter? filter = null,
        int page = 1,
        int pageSize = 20)
    {
        try
        {
            pageSize = Math.Clamp(pageSize, 1, 100);
            page = Math.Max(1, page);

            var query = _context.AuditLogs.AsQueryable();

            query = ApplyFilters(query, filter);

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(a => a.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Include(a => a.User)
                .Include(a => a.Vault)
                .Include(a => a.Credential)
                .ToListAsync();

            return AuditOperationResult<PaginatedAuditResult>.Ok(new PaginatedAuditResult
            {
                Items = items.Select(MapToDto),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get all audit logs");
            return AuditOperationResult<PaginatedAuditResult>.Fail("Failed to retrieve audit logs");
        }
    }

    private static IQueryable<AuditLog> ApplyFilters(IQueryable<AuditLog> query, AuditLogFilter? filter)
    {
        if (filter == null)
            return query;

        if (filter.Action.HasValue)
            query = query.Where(a => a.Action == filter.Action.Value);

        if (filter.StartDate.HasValue)
            query = query.Where(a => a.Timestamp >= filter.StartDate.Value);

        if (filter.EndDate.HasValue)
            query = query.Where(a => a.Timestamp <= filter.EndDate.Value);

        if (filter.VaultId.HasValue)
            query = query.Where(a => a.VaultId == filter.VaultId.Value);

        if (filter.CredentialId.HasValue)
            query = query.Where(a => a.CredentialId == filter.CredentialId.Value);

        if (!string.IsNullOrWhiteSpace(filter.EntityType))
            query = query.Where(a => a.EntityType == filter.EntityType);

        return query;
    }

    private static AuditLogDto MapToDto(AuditLog auditLog)
    {
        return new AuditLogDto(
            Id: auditLog.Id,
            Action: auditLog.Action,
            ActionName: auditLog.Action.ToString(),
            EntityType: auditLog.EntityType,
            EntityId: auditLog.EntityId,
            Details: auditLog.Details,
            UserId: auditLog.UserId,
            UserEmail: auditLog.User?.Email,
            VaultId: auditLog.VaultId,
            VaultName: auditLog.Vault?.Name,
            CredentialId: auditLog.CredentialId,
            CredentialTitle: auditLog.Credential?.Title,
            IpAddress: auditLog.IpAddress,
            UserAgent: auditLog.UserAgent,
            Timestamp: auditLog.Timestamp
        );
    }
}
