using Microsoft.EntityFrameworkCore;
using PassManAPI.Data;
using PassManAPI.DTOs;
using PassManAPI.Models;

namespace PassManAPI.Managers;

/// <summary>
/// Manager for credential business logic operations.
/// Handles CRUD operations with encryption, authorization, and audit logging.
/// </summary>
public class CredentialManager : ICredentialManager
{
    private readonly ApplicationDbContext _db;

    public CredentialManager(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<CredentialOperationResult<CredentialDto>> CreateCredentialAsync(
        int vaultId,
        int userId,
        string title,
        string encryptedPassword,
        string? username = null,
        string? url = null,
        string? notes = null,
        int? categoryId = null)
    {
        // Validate required fields
        if (string.IsNullOrWhiteSpace(title))
        {
            return CredentialOperationResult<CredentialDto>.Fail("Credential title is required.");
        }

        if (string.IsNullOrWhiteSpace(encryptedPassword))
        {
            return CredentialOperationResult<CredentialDto>.Fail("Encrypted password is required.");
        }

        // Check vault exists and user has access
        var vault = await _db.Vaults.AsNoTracking().FirstOrDefaultAsync(v => v.Id == vaultId);
        if (vault is null)
        {
            return CredentialOperationResult<CredentialDto>.Fail("Vault not found.");
        }

        var canModify = await CanModifyVaultAsync(vaultId, userId);
        if (!canModify)
        {
            return CredentialOperationResult<CredentialDto>.Fail("You do not have permission to add credentials to this vault.");
        }

        // Validate category if provided
        if (categoryId.HasValue)
        {
            var categoryExists = await _db.Categories.AsNoTracking().AnyAsync(c => c.Id == categoryId.Value);
            if (!categoryExists)
            {
                return CredentialOperationResult<CredentialDto>.Fail($"Category with id {categoryId.Value} does not exist.");
            }
        }

        // Create credential entity
        var credential = new Credential
        {
            Title = title.Trim(),
            Username = string.IsNullOrWhiteSpace(username) ? null : username.Trim(),
            EncryptedPassword = encryptedPassword,
            Url = string.IsNullOrWhiteSpace(url) ? null : url.Trim(),
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            VaultId = vaultId,
            CategoryId = categoryId,
            CreatedAt = DateTime.UtcNow
        };

        _db.Credentials.Add(credential);
        await _db.SaveChangesAsync();

        // Log audit event
        await LogAuditAsync(AuditAction.CredentialCreated, userId, credential.Id, vaultId, $"Created credential: {credential.Title}");

        // Reload with includes for DTO conversion
        var createdCredential = await _db.Credentials
            .AsNoTracking()
            .Include(c => c.Category)
            .Include(c => c.CredentialTags)
                .ThenInclude(ct => ct.Tag)
            .FirstOrDefaultAsync(c => c.Id == credential.Id);

        return CredentialOperationResult<CredentialDto>.Ok(ToDto(createdCredential!));
    }

    public async Task<CredentialOperationResult<IEnumerable<CredentialDto>>> GetCredentialsByVaultAsync(int vaultId, int userId)
    {
        // Check vault exists and user has access
        var hasAccess = await HasAccessToVaultAsync(vaultId, userId);
        if (!hasAccess)
        {
            return CredentialOperationResult<IEnumerable<CredentialDto>>.Fail("Vault not found or access denied.");
        }

        var credentials = await _db.Credentials
            .AsNoTracking()
            .Include(c => c.Category)
            .Include(c => c.CredentialTags)
                .ThenInclude(ct => ct.Tag)
            .Where(c => c.VaultId == vaultId)
            .OrderBy(c => c.Title)
            .ToListAsync();

        var credentialDtos = credentials.Select(ToDto).ToList();

        return CredentialOperationResult<IEnumerable<CredentialDto>>.Ok(credentialDtos);
    }

    public async Task<CredentialOperationResult<CredentialDto>> GetCredentialByIdAsync(int credentialId, int userId)
    {
        var credential = await _db.Credentials
            .AsNoTracking()
            .Include(c => c.Category)
            .Include(c => c.CredentialTags)
                .ThenInclude(ct => ct.Tag)
            .FirstOrDefaultAsync(c => c.Id == credentialId);

        if (credential is null)
        {
            return CredentialOperationResult<CredentialDto>.Fail("Credential not found.");
        }

        var hasAccess = await HasAccessAsync(credentialId, userId);
        if (!hasAccess)
        {
            return CredentialOperationResult<CredentialDto>.Fail("Access denied.");
        }

        // Update last accessed timestamp
        var credentialToUpdate = await _db.Credentials.FirstOrDefaultAsync(c => c.Id == credentialId);
        if (credentialToUpdate != null)
        {
            credentialToUpdate.LastAccessed = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        // Log audit event
        await LogAuditAsync(AuditAction.CredentialViewed, userId, credentialId, credential.VaultId, $"Viewed credential: {credential.Title}");

        return CredentialOperationResult<CredentialDto>.Ok(ToDto(credential));
    }

    public async Task<CredentialOperationResult<CredentialDto>> UpdateCredentialAsync(
        int credentialId,
        int userId,
        string title,
        string? username = null,
        string? url = null,
        string? notes = null,
        int? categoryId = null)
    {
        // Validate required fields
        if (string.IsNullOrWhiteSpace(title))
        {
            return CredentialOperationResult<CredentialDto>.Fail("Credential title is required.");
        }

        var credential = await _db.Credentials
            .Include(c => c.Category)
            .Include(c => c.CredentialTags)
                .ThenInclude(ct => ct.Tag)
            .FirstOrDefaultAsync(c => c.Id == credentialId);

        if (credential is null)
        {
            return CredentialOperationResult<CredentialDto>.Fail("Credential not found.");
        }

        var canModify = await CanModifyAsync(credentialId, userId);
        if (!canModify)
        {
            return CredentialOperationResult<CredentialDto>.Fail("You do not have permission to update this credential.");
        }

        // Validate category if provided
        if (categoryId.HasValue)
        {
            var categoryExists = await _db.Categories.AsNoTracking().AnyAsync(c => c.Id == categoryId.Value);
            if (!categoryExists)
            {
                return CredentialOperationResult<CredentialDto>.Fail($"Category with id {categoryId.Value} does not exist.");
            }
        }

        // Update credential properties
        credential.Title = title.Trim();
        credential.Username = string.IsNullOrWhiteSpace(username) ? null : username.Trim();
        credential.Url = string.IsNullOrWhiteSpace(url) ? null : url.Trim();
        credential.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        credential.CategoryId = categoryId;
        credential.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        // Log audit event
        await LogAuditAsync(AuditAction.CredentialUpdated, userId, credentialId, credential.VaultId, $"Updated credential: {credential.Title}");

        // Reload for fresh DTO conversion
        var updatedCredential = await _db.Credentials
            .AsNoTracking()
            .Include(c => c.Category)
            .Include(c => c.CredentialTags)
                .ThenInclude(ct => ct.Tag)
            .FirstOrDefaultAsync(c => c.Id == credentialId);

        return CredentialOperationResult<CredentialDto>.Ok(ToDto(updatedCredential!));
    }

    public async Task<CredentialOperationResult<CredentialDto>> UpdateCredentialPasswordAsync(
        int credentialId,
        int userId,
        string encryptedPassword)
    {
        if (string.IsNullOrWhiteSpace(encryptedPassword))
        {
            return CredentialOperationResult<CredentialDto>.Fail("Encrypted password is required.");
        }

        var credential = await _db.Credentials
            .Include(c => c.Category)
            .Include(c => c.CredentialTags)
                .ThenInclude(ct => ct.Tag)
            .FirstOrDefaultAsync(c => c.Id == credentialId);

        if (credential is null)
        {
            return CredentialOperationResult<CredentialDto>.Fail("Credential not found.");
        }

        var canModify = await CanModifyAsync(credentialId, userId);
        if (!canModify)
        {
            return CredentialOperationResult<CredentialDto>.Fail("You do not have permission to update this credential's password.");
        }

        // Update password
        credential.EncryptedPassword = encryptedPassword;
        credential.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        // Log audit event
        await LogAuditAsync(AuditAction.CredentialUpdated, userId, credentialId, credential.VaultId, $"Updated password for credential: {credential.Title}");

        // Reload for fresh DTO conversion
        var updatedCredential = await _db.Credentials
            .AsNoTracking()
            .Include(c => c.Category)
            .Include(c => c.CredentialTags)
                .ThenInclude(ct => ct.Tag)
            .FirstOrDefaultAsync(c => c.Id == credentialId);

        return CredentialOperationResult<CredentialDto>.Ok(ToDto(updatedCredential!));
    }

    public async Task<CredentialOperationResult<bool>> DeleteCredentialAsync(int credentialId, int userId)
    {
        var credential = await _db.Credentials
            .Include(c => c.Vault)
            .FirstOrDefaultAsync(c => c.Id == credentialId);

        if (credential is null)
        {
            return CredentialOperationResult<bool>.Fail("Credential not found.");
        }

        // Only vault owner can delete credentials
        if (credential.Vault.UserId != userId)
        {
            return CredentialOperationResult<bool>.Fail("Only the vault owner can delete credentials.");
        }

        var credentialTitle = credential.Title;
        var vaultId = credential.VaultId;

        _db.Credentials.Remove(credential);
        await _db.SaveChangesAsync();

        // Log audit event
        await LogAuditAsync(AuditAction.CredentialDeleted, userId, credentialId, vaultId, $"Deleted credential: {credentialTitle}");

        return CredentialOperationResult<bool>.Ok(true);
    }

    public async Task<CredentialOperationResult<IEnumerable<CredentialDto>>> SearchCredentialsAsync(int userId, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return CredentialOperationResult<IEnumerable<CredentialDto>>.Fail("Search query is required.");
        }

        var searchTerm = query.Trim().ToLower();

        // Get user's accessible vault IDs (owned + shared)
        var ownedVaultIds = await _db.Vaults
            .AsNoTracking()
            .Where(v => v.UserId == userId)
            .Select(v => v.Id)
            .ToListAsync();

        var sharedVaultIds = await _db.VaultShares
            .AsNoTracking()
            .Where(vs => vs.UserId == userId)
            .Select(vs => vs.VaultId)
            .ToListAsync();

        var accessibleVaultIds = ownedVaultIds.Concat(sharedVaultIds).Distinct().ToList();

        // Search credentials in accessible vaults
        var credentials = await _db.Credentials
            .AsNoTracking()
            .Include(c => c.Category)
            .Include(c => c.CredentialTags)
                .ThenInclude(ct => ct.Tag)
            .Where(c => accessibleVaultIds.Contains(c.VaultId))
            .Where(c =>
                c.Title.ToLower().Contains(searchTerm) ||
                (c.Username != null && c.Username.ToLower().Contains(searchTerm)) ||
                (c.Url != null && c.Url.ToLower().Contains(searchTerm)) ||
                (c.Notes != null && c.Notes.ToLower().Contains(searchTerm)))
            .OrderBy(c => c.Title)
            .ToListAsync();

        var credentialDtos = credentials.Select(ToDto).ToList();

        return CredentialOperationResult<IEnumerable<CredentialDto>>.Ok(credentialDtos);
    }

    public async Task<bool> HasAccessAsync(int credentialId, int userId)
    {
        var credential = await _db.Credentials
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == credentialId);

        if (credential is null)
        {
            return false;
        }

        return await HasAccessToVaultAsync(credential.VaultId, userId);
    }

    public async Task<bool> CanModifyAsync(int credentialId, int userId)
    {
        var credential = await _db.Credentials
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == credentialId);

        if (credential is null)
        {
            return false;
        }

        return await CanModifyVaultAsync(credential.VaultId, userId);
    }

    // Private helper methods

    private async Task<bool> HasAccessToVaultAsync(int vaultId, int userId)
    {
        // Check if user is owner
        var isOwner = await _db.Vaults
            .AsNoTracking()
            .AnyAsync(v => v.Id == vaultId && v.UserId == userId);

        if (isOwner)
        {
            return true;
        }

        // Check if vault is shared with user
        var isShared = await _db.VaultShares
            .AsNoTracking()
            .AnyAsync(vs => vs.VaultId == vaultId && vs.UserId == userId);

        return isShared;
    }

    private async Task<bool> CanModifyVaultAsync(int vaultId, int userId)
    {
        // Check if user is owner
        var isOwner = await _db.Vaults
            .AsNoTracking()
            .AnyAsync(v => v.Id == vaultId && v.UserId == userId);

        if (isOwner)
        {
            return true;
        }

        // Check if user has Admin permission on shared vault
        var hasAdminPermission = await _db.VaultShares
            .AsNoTracking()
            .AnyAsync(vs => vs.VaultId == vaultId && vs.UserId == userId && vs.Permission == SharePermission.Admin);

        return hasAdminPermission;
    }

    private async Task LogAuditAsync(AuditAction action, int userId, int credentialId, int vaultId, string? details = null)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            Action = action,
            EntityType = "Credential",
            EntityId = credentialId,
            UserId = userId,
            VaultId = vaultId,
            CredentialId = credentialId,
            Details = details,
            Timestamp = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }

    private static CredentialDto ToDto(Credential credential) =>
        new()
        {
            Id = credential.Id,
            Title = credential.Title,
            Username = credential.Username,
            Url = credential.Url,
            Notes = credential.Notes,
            CategoryId = credential.CategoryId,
            CategoryName = credential.Category?.Name,
            VaultId = credential.VaultId,
            CreatedAt = credential.CreatedAt,
            UpdatedAt = credential.UpdatedAt,
            LastAccessed = credential.LastAccessed,
            Tags = credential.CredentialTags?.Select(ct => new TagDto { Id = ct.Tag.Id, Name = ct.Tag.Name }).ToList() ?? new List<TagDto>()
        };
}
