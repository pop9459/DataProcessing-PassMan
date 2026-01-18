using Microsoft.EntityFrameworkCore;
using PassManAPI.Data;
using PassManAPI.Models;

namespace PassManAPI.Managers;

/// <summary>
/// Implementation of vault sharing business logic.
/// </summary>
public class SharingManager : ISharingManager
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<SharingManager> _logger;

    // In-memory store for invitations (in production, use database table)
    private static readonly Dictionary<Guid, InvitationRecord> _invitations = new();

    private record InvitationRecord(
        Guid Token,
        int VaultId,
        string Email,
        SharePermission Permission,
        DateTime CreatedAt,
        DateTime ExpiresAt,
        int CreatedByUserId
    );

    public SharingManager(ApplicationDbContext db, ILogger<SharingManager> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<SharingResult<VaultShareInfo>> ShareVaultAsync(
        int vaultId, int ownerId, string targetEmail, SharePermission permission)
    {
        // Verify vault exists and user has permission to share
        var vault = await _db.Vaults.AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == vaultId);

        if (vault == null)
        {
            return SharingResult<VaultShareInfo>.Fail("Vault not found.");
        }

        // Check if user is owner or has admin permission
        var isOwner = vault.UserId == ownerId;
        var hasAdminShare = await _db.VaultShares.AsNoTracking()
            .AnyAsync(vs => vs.VaultId == vaultId && vs.UserId == ownerId && vs.Permission == SharePermission.Admin);

        if (!isOwner && !hasAdminShare)
        {
            return SharingResult<VaultShareInfo>.Fail("You do not have permission to share this vault.");
        }

        // Find target user
        var targetUser = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == targetEmail);

        if (targetUser == null)
        {
            return SharingResult<VaultShareInfo>.Fail("User not found with that email address.");
        }

        // Cannot share with the owner
        if (targetUser.Id == vault.UserId)
        {
            return SharingResult<VaultShareInfo>.Fail("Cannot share vault with its owner.");
        }

        // Check if already shared
        var existingShare = await _db.VaultShares
            .FirstOrDefaultAsync(vs => vs.VaultId == vaultId && vs.UserId == targetUser.Id);

        if (existingShare != null)
        {
            // Update existing share permission
            existingShare.Permission = permission;
            existingShare.SharedByUserId = ownerId;
            await _db.SaveChangesAsync();

            _logger.LogInformation("Updated vault share: VaultId={VaultId}, UserId={UserId}, Permission={Permission}",
                vaultId, targetUser.Id, permission);
        }
        else
        {
            // Create new share
            var share = new VaultShare
            {
                VaultId = vaultId,
                UserId = targetUser.Id,
                Permission = permission,
                SharedAt = DateTime.UtcNow,
                SharedByUserId = ownerId
            };
            _db.VaultShares.Add(share);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Created vault share: VaultId={VaultId}, UserId={UserId}, Permission={Permission}",
                vaultId, targetUser.Id, permission);
        }

        // Get sharer info for response
        var sharer = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == ownerId);

        return SharingResult<VaultShareInfo>.Ok(new VaultShareInfo(
            vaultId,
            vault.Name,
            targetUser.Id,
            targetUser.Email!,
            permission,
            DateTime.UtcNow,
            ownerId,
            sharer?.Email
        ));
    }

    public async Task<SharingResult<ShareInvitation>> CreateInvitationAsync(
        int vaultId, string email, SharePermission permission, DateTime expiresAt)
    {
        // Verify vault exists
        var vault = await _db.Vaults.AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == vaultId);

        if (vault == null)
        {
            return SharingResult<ShareInvitation>.Fail("Vault not found.");
        }

        // Create invitation token
        var token = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var record = new InvitationRecord(
            token,
            vaultId,
            email.ToLowerInvariant(),
            permission,
            now,
            expiresAt,
            vault.UserId
        );

        _invitations[token] = record;

        _logger.LogInformation("Created invitation: VaultId={VaultId}, Email={Email}, Token={Token}, ExpiresAt={ExpiresAt}",
            vaultId, email, token, expiresAt);

        return SharingResult<ShareInvitation>.Ok(new ShareInvitation(
            token,
            vaultId,
            email,
            permission,
            now,
            expiresAt
        ));
    }

    public async Task<SharingResult<VaultShareInfo>> AcceptInvitationAsync(Guid token, int userId)
    {
        if (!_invitations.TryGetValue(token, out var invitation))
        {
            return SharingResult<VaultShareInfo>.Fail("Invalid or expired invitation.");
        }

        if (DateTime.UtcNow > invitation.ExpiresAt)
        {
            _invitations.Remove(token);
            return SharingResult<VaultShareInfo>.Fail("Invitation has expired.");
        }

        // Get user and verify email matches
        var user = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
        {
            return SharingResult<VaultShareInfo>.Fail("User not found.");
        }

        if (!string.Equals(user.Email, invitation.Email, StringComparison.OrdinalIgnoreCase))
        {
            return SharingResult<VaultShareInfo>.Fail("This invitation was sent to a different email address.");
        }

        // Get vault
        var vault = await _db.Vaults.AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == invitation.VaultId);

        if (vault == null)
        {
            _invitations.Remove(token);
            return SharingResult<VaultShareInfo>.Fail("Vault no longer exists.");
        }

        // Cannot share with the owner
        if (userId == vault.UserId)
        {
            _invitations.Remove(token);
            return SharingResult<VaultShareInfo>.Fail("You already own this vault.");
        }

        // Create or update share
        var existingShare = await _db.VaultShares
            .FirstOrDefaultAsync(vs => vs.VaultId == invitation.VaultId && vs.UserId == userId);

        if (existingShare != null)
        {
            existingShare.Permission = invitation.Permission;
            existingShare.SharedByUserId = invitation.CreatedByUserId;
        }
        else
        {
            var share = new VaultShare
            {
                VaultId = invitation.VaultId,
                UserId = userId,
                Permission = invitation.Permission,
                SharedAt = DateTime.UtcNow,
                SharedByUserId = invitation.CreatedByUserId
            };
            _db.VaultShares.Add(share);
        }

        await _db.SaveChangesAsync();

        // Remove used invitation
        _invitations.Remove(token);

        _logger.LogInformation("Invitation accepted: VaultId={VaultId}, UserId={UserId}", invitation.VaultId, userId);

        // Get sharer info
        var sharer = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == invitation.CreatedByUserId);

        return SharingResult<VaultShareInfo>.Ok(new VaultShareInfo(
            invitation.VaultId,
            vault.Name,
            userId,
            user.Email!,
            invitation.Permission,
            DateTime.UtcNow,
            invitation.CreatedByUserId,
            sharer?.Email
        ));
    }

    public async Task<SharingResult<bool>> RevokeAccessAsync(int vaultId, int ownerId, int targetUserId)
    {
        // Verify vault exists and user has permission to revoke
        var vault = await _db.Vaults.AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == vaultId);

        if (vault == null)
        {
            return SharingResult<bool>.Fail("Vault not found.");
        }

        // Check if user is owner or has admin permission
        var isOwner = vault.UserId == ownerId;
        var hasAdminShare = await _db.VaultShares.AsNoTracking()
            .AnyAsync(vs => vs.VaultId == vaultId && vs.UserId == ownerId && vs.Permission == SharePermission.Admin);

        if (!isOwner && !hasAdminShare)
        {
            return SharingResult<bool>.Fail("You do not have permission to revoke access to this vault.");
        }

        // Cannot revoke owner's access
        if (targetUserId == vault.UserId)
        {
            return SharingResult<bool>.Fail("Cannot revoke owner's access to their own vault.");
        }

        // Find and remove the share
        var share = await _db.VaultShares
            .FirstOrDefaultAsync(vs => vs.VaultId == vaultId && vs.UserId == targetUserId);

        if (share == null)
        {
            return SharingResult<bool>.Fail("User does not have access to this vault.");
        }

        _db.VaultShares.Remove(share);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Revoked access: VaultId={VaultId}, UserId={UserId}", vaultId, targetUserId);

        return SharingResult<bool>.Ok(true);
    }

    public async Task<SharingResult<VaultShareInfo>> ChangeRoleAsync(
        int vaultId, int ownerId, int targetUserId, SharePermission newPermission)
    {
        // Verify vault exists and user has permission to change roles
        var vault = await _db.Vaults.AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == vaultId);

        if (vault == null)
        {
            return SharingResult<VaultShareInfo>.Fail("Vault not found.");
        }

        // Check if user is owner or has admin permission
        var isOwner = vault.UserId == ownerId;
        var hasAdminShare = await _db.VaultShares.AsNoTracking()
            .AnyAsync(vs => vs.VaultId == vaultId && vs.UserId == ownerId && vs.Permission == SharePermission.Admin);

        if (!isOwner && !hasAdminShare)
        {
            return SharingResult<VaultShareInfo>.Fail("You do not have permission to change roles for this vault.");
        }

        // Cannot change owner's role
        if (targetUserId == vault.UserId)
        {
            return SharingResult<VaultShareInfo>.Fail("Cannot change owner's role.");
        }

        // Find and update the share
        var share = await _db.VaultShares
            .FirstOrDefaultAsync(vs => vs.VaultId == vaultId && vs.UserId == targetUserId);

        if (share == null)
        {
            return SharingResult<VaultShareInfo>.Fail("User does not have access to this vault.");
        }

        share.Permission = newPermission;
        await _db.SaveChangesAsync();

        // Get user info
        var user = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == targetUserId);

        var sharer = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == share.SharedByUserId);

        _logger.LogInformation("Changed role: VaultId={VaultId}, UserId={UserId}, NewPermission={Permission}",
            vaultId, targetUserId, newPermission);

        return SharingResult<VaultShareInfo>.Ok(new VaultShareInfo(
            vaultId,
            vault.Name,
            targetUserId,
            user?.Email ?? "",
            newPermission,
            share.SharedAt,
            share.SharedByUserId,
            sharer?.Email
        ));
    }

    public async Task<SharingResult<IList<VaultShareInfo>>> GetVaultSharesAsync(int vaultId, int userId)
    {
        // Verify vault exists and user has permission to view shares
        var vault = await _db.Vaults.AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == vaultId);

        if (vault == null)
        {
            return SharingResult<IList<VaultShareInfo>>.Fail("Vault not found.");
        }

        // Check if user is owner or has admin permission or has any share
        var isOwner = vault.UserId == userId;
        var hasAdminShare = await _db.VaultShares.AsNoTracking()
            .AnyAsync(vs => vs.VaultId == vaultId && vs.UserId == userId && vs.Permission == SharePermission.Admin);

        if (!isOwner && !hasAdminShare)
        {
            return SharingResult<IList<VaultShareInfo>>.Fail("You do not have permission to view shares for this vault.");
        }

        // Get all shares
        var shares = await _db.VaultShares
            .AsNoTracking()
            .Where(vs => vs.VaultId == vaultId)
            .Include(vs => vs.User)
            .Include(vs => vs.SharedByUser)
            .ToListAsync();

        var shareInfos = shares.Select(s => new VaultShareInfo(
            s.VaultId,
            vault.Name,
            s.UserId,
            s.User?.Email ?? "",
            s.Permission,
            s.SharedAt,
            s.SharedByUserId,
            s.SharedByUser?.Email
        )).ToList();

        return SharingResult<IList<VaultShareInfo>>.Ok(shareInfos);
    }

    public async Task<bool> HasAccessAsync(int vaultId, int userId, SharePermission? requiredPermission = null)
    {
        // Check if user is owner
        var vault = await _db.Vaults.AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == vaultId);

        if (vault == null)
        {
            return false;
        }

        if (vault.UserId == userId)
        {
            return true; // Owner has all permissions
        }

        // Check shared access
        var share = await _db.VaultShares.AsNoTracking()
            .FirstOrDefaultAsync(vs => vs.VaultId == vaultId && vs.UserId == userId);

        if (share == null)
        {
            return false;
        }

        if (requiredPermission.HasValue)
        {
            // Higher permission level = higher enum value, so user needs >= required
            return share.Permission >= requiredPermission.Value;
        }

        return true; // Has some access
    }
}
