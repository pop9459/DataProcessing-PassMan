using Microsoft.EntityFrameworkCore;
using PassManAPI.Data;
using PassManAPI.Models;

namespace PassManAPI.Managers;

/// <summary>
/// Manager for vault business logic operations.
/// Handles CRUD operations with proper authorization and soft delete.
/// </summary>
public class VaultManager : IVaultManager
{
    private readonly ApplicationDbContext _db;

    public VaultManager(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<VaultOperationResult<VaultDto>> CreateVaultAsync(
        int userId,
        string name,
        string? description = null,
        string? icon = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return VaultOperationResult<VaultDto>.Fail("Vault name is required.");
        }

        var userExists = await _db.Users.AsNoTracking().AnyAsync(u => u.Id == userId);
        if (!userExists)
        {
            return VaultOperationResult<VaultDto>.Fail($"User with id {userId} does not exist.");
        }

        var vault = new Vault
        {
            Name = name.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            Icon = string.IsNullOrWhiteSpace(icon) ? null : icon.Trim(),
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };

        _db.Vaults.Add(vault);
        await _db.SaveChangesAsync();

        return VaultOperationResult<VaultDto>.Ok(ToDto(vault, isOwner: true));
    }

    public async Task<VaultOperationResult<IEnumerable<VaultDto>>> GetUserVaultsAsync(int userId)
    {
        // Get owned vaults
        var ownedVaults = await _db.Vaults
            .AsNoTracking()
            .Where(v => v.UserId == userId)
            .ToListAsync();

        // Get shared vault IDs
        var sharedVaultIds = await _db.VaultShares
            .AsNoTracking()
            .Where(vs => vs.UserId == userId)
            .Select(vs => vs.VaultId)
            .ToListAsync();

        // Get shared vaults (must use IgnoreQueryFilters to get vaults even if they have soft-delete issues)
        var sharedVaults = sharedVaultIds.Count > 0
            ? await _db.Vaults
                .AsNoTracking()
                .Where(v => sharedVaultIds.Contains(v.Id))
                .ToListAsync()
            : new List<Vault>();

        var allVaults = ownedVaults
            .Select(v => ToDto(v, isOwner: true))
            .Concat(sharedVaults.Select(v => ToDto(v, isOwner: false)))
            .OrderBy(v => v.Id)
            .ToList();

        return VaultOperationResult<IEnumerable<VaultDto>>.Ok(allVaults);
    }

    public async Task<VaultOperationResult<VaultDto>> GetVaultByIdAsync(int vaultId, int userId)
    {
        var vault = await _db.Vaults.AsNoTracking().FirstOrDefaultAsync(v => v.Id == vaultId);
        if (vault is null)
        {
            return VaultOperationResult<VaultDto>.Fail("Vault not found.");
        }

        var hasAccess = await HasAccessAsync(vaultId, userId);
        if (!hasAccess)
        {
            return VaultOperationResult<VaultDto>.Fail("Access denied.");
        }

        var isOwner = vault.UserId == userId;
        return VaultOperationResult<VaultDto>.Ok(ToDto(vault, isOwner));
    }

    public async Task<VaultOperationResult<VaultDto>> UpdateVaultAsync(
        int vaultId,
        int userId,
        string name,
        string? description = null,
        string? icon = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return VaultOperationResult<VaultDto>.Fail("Vault name is required.");
        }

        var vault = await _db.Vaults.FirstOrDefaultAsync(v => v.Id == vaultId);
        if (vault is null)
        {
            return VaultOperationResult<VaultDto>.Fail("Vault not found.");
        }

        if (vault.UserId != userId)
        {
            return VaultOperationResult<VaultDto>.Fail("Only the vault owner can update the vault.");
        }

        vault.Name = name.Trim();
        vault.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        vault.Icon = string.IsNullOrWhiteSpace(icon) ? null : icon.Trim();
        vault.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return VaultOperationResult<VaultDto>.Ok(ToDto(vault, isOwner: true));
    }

    public async Task<VaultOperationResult<bool>> DeleteVaultAsync(int vaultId, int userId)
    {
        var vault = await _db.Vaults.FirstOrDefaultAsync(v => v.Id == vaultId);
        if (vault is null)
        {
            return VaultOperationResult<bool>.Fail("Vault not found.");
        }

        if (vault.UserId != userId)
        {
            return VaultOperationResult<bool>.Fail("Only the vault owner can delete the vault.");
        }

        // Soft delete - mark as deleted instead of removing
        vault.IsDeleted = true;
        vault.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return VaultOperationResult<bool>.Ok(true);
    }

    public async Task<bool> HasAccessAsync(int vaultId, int userId)
    {
        // Check ownership first
        var isOwner = await _db.Vaults
            .AsNoTracking()
            .AnyAsync(v => v.Id == vaultId && v.UserId == userId);

        if (isOwner)
        {
            return true;
        }

        // Check if vault is shared with user
        return await _db.VaultShares
            .AsNoTracking()
            .AnyAsync(vs => vs.VaultId == vaultId && vs.UserId == userId);
    }

    public async Task<bool> IsOwnerAsync(int vaultId, int userId)
    {
        return await _db.Vaults
            .AsNoTracking()
            .AnyAsync(v => v.Id == vaultId && v.UserId == userId);
    }

    private static VaultDto ToDto(Vault vault, bool isOwner) =>
        new(
            Id: vault.Id,
            Name: vault.Name,
            Description: vault.Description,
            Icon: vault.Icon,
            CreatedAt: vault.CreatedAt,
            UpdatedAt: vault.UpdatedAt,
            UserId: vault.UserId,
            IsOwner: isOwner
        );
}
