using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PassManAPI.Data;
using PassManAPI.DTOs;
using PassManAPI.Helpers;
using PassManAPI.Models;
using System.Security.Claims;

namespace PassManAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class InvitationsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;

        public InvitationsController(ApplicationDbContext context, UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // POST: api/invitations
        [HttpPost]
        public async Task<IActionResult> CreateInvitation([FromBody] CreateInvitationRequest request)
        {
            if (!User.TryGetCurrentUserId(out var userId))
            {
                return this.UnauthorizedProblem();
            }

            // Verify vault ownership or admin access
            var vault = await _context.Vaults
                .Include(v => v.SharedUsers)
                .FirstOrDefaultAsync(v => v.Id == request.VaultId);

            if (vault == null)
            {
                return this.NotFoundProblem("Vault not found.");
            }

            // Check if user is owner or has Admin access
            bool isOwner = vault.UserId == userId;
            bool isAdmin = vault.SharedUsers.Any(vs => vs.UserId == userId && vs.Permission == SharePermission.Admin);

            if (!isOwner && !isAdmin)
            {
                return this.ForbiddenProblem("You do not have permission to manage invitations for this vault.");
            }

            // Parse role
            if (!Enum.TryParse<AccessRole>(request.Role, true, out var role))
            {
                return this.BadRequestProblem("Invalid role.");
            }

            // Remove existing pending invitations for this email/vault
            var existingInvites = await _context.Invitations
                .Where(i => i.VaultId == request.VaultId && i.InvitedEmail == request.Email && i.AcceptedAt == null && i.ExpiresAt > DateTime.UtcNow)
                .ToListAsync();
            
            _context.Invitations.RemoveRange(existingInvites);

            // Create new invitation
            var token = Guid.NewGuid().ToString();
            var invitation = new Invitation(request.VaultId, request.Email, role, DateTime.UtcNow.AddDays(7), token);

            _context.Invitations.Add(invitation);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(ListInvitations), new { id = invitation.Id }, new InvitationResponse
            {
                Id = invitation.Id,
                VaultId = invitation.VaultId,
                VaultName = vault.Name,
                InvitedEmail = invitation.InvitedEmail,
                Role = invitation.Role.ToString(),
                Status = "Pending",
                ExpiresAt = invitation.ExpiresAt,
                InviteToken = invitation.InviteToken 
            });
        }

        // GET: api/invitations
        [HttpGet]
        public async Task<ActionResult<IEnumerable<InvitationResponse>>> ListInvitations()
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrEmpty(userEmail))
            {
                if (!User.TryGetCurrentUserId(out var userId))
                {
                    return this.UnauthorizedProblem();
                }
                var user = await _userManager.FindByIdAsync(userId.ToString());
                if (user is null)
                {
                    return this.UnauthorizedProblem("User not found or token invalid.");
                }
                userEmail = user.Email;
            }

            var invitations = await _context.Invitations
                .Include(i => i.Vault)
                .Where(i => i.InvitedEmail == userEmail && i.AcceptedAt == null && i.ExpiresAt > DateTime.UtcNow)
                .ToListAsync();

            return Ok(invitations.Select(i => new InvitationResponse
            {
                Id = i.Id,
                VaultId = i.VaultId,
                VaultName = i.Vault.Name,
                InvitedEmail = i.InvitedEmail,
                Role = i.Role.ToString(),
                Status = "Pending",
                ExpiresAt = i.ExpiresAt,
                InviteToken = i.InviteToken
            }).ToList());
        }

        // POST: api/invitations/{token}/accept
        [HttpPost("{token}/accept")]
        public async Task<IActionResult> AcceptInvitation(string token)
        {
            if (!User.TryGetCurrentUserId(out var userId))
            {
                return this.UnauthorizedProblem();
            }
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user is null)
            {
                return this.UnauthorizedProblem("User not found or token invalid.");
            }
            
            var invitation = await _context.Invitations
                .Include(i => i.Vault)
                .FirstOrDefaultAsync(i => i.InviteToken == token);

            if (invitation == null)
            {
                return this.NotFoundProblem("Invitation not found.");
            }

            if (invitation.InvitedEmail.ToLower() != user!.Email!.ToLower())
            {
                return this.BadRequestProblem("This invitation is for a different email address.");
            }

            try
            {
                invitation.MarkAccepted();
            }
            catch (InvalidOperationException ex)
            {
                return this.BadRequestProblem(ex.Message);
            }

            // Create VaultShare
            // Map AccessRole to SharePermission
            SharePermission permission = invitation.Role switch
            {
                AccessRole.Admin => SharePermission.Admin,
                AccessRole.Edit => SharePermission.Edit,
                _ => SharePermission.View
            };

            var vaultShare = new VaultShare
            {
                VaultId = invitation.VaultId,
                UserId = userId,
                Permission = permission
            };

            // Check if already shared
            var existingShare = await _context.VaultShares.FindAsync(invitation.VaultId, userId);
            if (existingShare != null)
            {
                existingShare.Permission = permission; // Update existing share
            }
            else
            {
                _context.VaultShares.Add(vaultShare);
            }

            await _context.SaveChangesAsync();

            return Ok(new InvitationAcceptedResponse { Message = "Invitation accepted.", VaultId = invitation.VaultId });
        }

        // DELETE: api/invitations/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> RevokeInvitation(int id)
        {
            if (!User.TryGetCurrentUserId(out var userId))
            {
                return this.UnauthorizedProblem();
            }

            var invitation = await _context.Invitations
                .Include(i => i.Vault)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (invitation == null)
            {
                return this.NotFoundProblem("Invitation not found.");
            }

            // Check if requester is vault owner
            // Note: Currently simple logic, could also allow Admins to revoke
            if (invitation.Vault.UserId != userId)
            {
                // Also check if it's the invited user declining
                var user = await _userManager.FindByIdAsync(userId.ToString());
                if (user is null || user.Email!.ToLower() != invitation.InvitedEmail.ToLower())
                {
                    return this.ForbiddenProblem("You do not have permission to revoke this invitation.");
                }
            }

            _context.Invitations.Remove(invitation);
            await _context.SaveChangesAsync();

            return NoContent();
        }

    }
}
