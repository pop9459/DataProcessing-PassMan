using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PassManAPI.Data;
using PassManAPI.DTOs;
using PassManAPI.Managers;
using PassManAPI.Models;

namespace PassManAPI.Controllers;

/// <summary>
/// Controller for administrative user management.
/// Separates user CRUD operations from authentication (AuthController).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UserController : ControllerBase
{
    private readonly UserManager _userManager;
    private readonly ApplicationDbContext _db;

    public UserController(UserManager userManager, ApplicationDbContext db)
    {
        _userManager = userManager;
        _db = db;
    }

    /// <summary>
    /// Lists all users (admin only).
    /// </summary>
    /// <response code="200">Returns the list of users.</response>
    /// <response code="403">If the user doesn't have UserManage permission.</response>
    [HttpGet]
    [Authorize(Policy = PermissionConstants.UserManage)]
    [ProducesResponseType(typeof(IEnumerable<UserProfileResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<UserProfileResponse>>> GetAllUsers()
    {
        var users = await _db.Users
            .AsNoTracking()
            .OrderBy(u => u.Email)
            .ToListAsync();

        var response = users.Select(ToProfile);
        return Ok(response);
    }

    /// <summary>
    /// Gets a specific user by ID.
    /// </summary>
    /// <param name="id">The user ID.</param>
    /// <response code="200">User found.</response>
    /// <response code="403">If the user doesn't have permission to view this user.</response>
    /// <response code="404">User not found.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserProfileResponse>> GetUser(int id)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        // Users can view their own profile, or admins can view any profile
        if (currentUserId != id && !HasPermission(PermissionConstants.UserManage))
        {
            return Forbid();
        }

        var result = await _userManager.GetUserByIdAsync(id);
        if (!result.Success || result.Data is null)
        {
            return NotFound(result.Error ?? "User not found.");
        }

        return Ok(ToProfile(result.Data));
    }

    /// <summary>
    /// Updates a user's profile.
    /// </summary>
    /// <param name="id">The user ID.</param>
    /// <param name="request">The update request.</param>
    /// <response code="200">User updated.</response>
    /// <response code="400">Invalid request.</response>
    /// <response code="403">If the user doesn't have permission to update this user.</response>
    /// <response code="404">User not found.</response>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserProfileResponse>> UpdateUser(int id, [FromBody] UpdateProfileRequest request)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        // Users can update their own profile, or admins can update any profile
        if (currentUserId != id && !HasPermission(PermissionConstants.UserManage))
        {
            return Forbid();
        }

        var updateRequest = new UpdateUserRequest(
            request.Email,
            request.UserName,
            request.PhoneNumber,
            request.EncryptedVaultKey
        );

        var result = await _userManager.UpdateUserAsync(id, updateRequest);
        if (!result.Success || result.Data is null)
        {
            if (result.Error?.Contains("not found") == true)
            {
                return NotFound(result.Error);
            }
            return BadRequest(result.Error ?? "Update failed.");
        }

        await LogAuditAsync(AuditAction.UserPasswordChanged, currentUserId, id, "Profile updated");
        return Ok(ToProfile(result.Data));
    }

    /// <summary>
    /// Deletes a user account.
    /// </summary>
    /// <param name="id">The user ID.</param>
    /// <response code="204">User deleted.</response>
    /// <response code="403">If the user doesn't have permission to delete this user.</response>
    /// <response code="404">User not found.</response>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteUser(int id)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        // Users can delete their own account, or admins can delete any account
        if (currentUserId != id && !HasPermission(PermissionConstants.UserManage))
        {
            return Forbid();
        }

        var result = await _userManager.DeleteUserAsync(id);
        if (!result.Success)
        {
            return NotFound(result.Error ?? "User not found.");
        }

        return NoContent();
    }

    /// <summary>
    /// Gets all vaults owned by or shared with a user.
    /// </summary>
    /// <param name="id">The user ID.</param>
    /// <response code="200">Returns the list of vaults.</response>
    /// <response code="403">If the user doesn't have permission to view this user's vaults.</response>
    /// <response code="404">User not found.</response>
    [HttpGet("{id:int}/vaults")]
    [ProducesResponseType(typeof(IEnumerable<VaultSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<VaultSummaryDto>>> GetUserVaults(int id)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        // Users can view their own vaults, or admins can view any user's vaults
        if (currentUserId != id && !HasPermission(PermissionConstants.UserManage))
        {
            return Forbid();
        }

        var userExists = await _db.Users.AnyAsync(u => u.Id == id);
        if (!userExists)
        {
            return NotFound("User not found.");
        }

        var vaults = await _db.Vaults
            .AsNoTracking()
            .Where(v => v.UserId == id && !v.IsDeleted)
            .Select(v => new VaultSummaryDto(v.Id, v.Name, v.Description, v.CreatedAt))
            .ToListAsync();

        return Ok(vaults);
    }

    /// <summary>
    /// Gets all tags owned by a user.
    /// </summary>
    /// <param name="id">The user ID.</param>
    /// <response code="200">Returns the list of tags.</response>
    /// <response code="403">If the user doesn't have permission to view this user's tags.</response>
    /// <response code="404">User not found.</response>
    [HttpGet("{id:int}/tags")]
    [ProducesResponseType(typeof(IEnumerable<TagDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<TagDto>>> GetUserTags(int id)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        // Users can view their own tags, or admins can view any user's tags
        if (currentUserId != id && !HasPermission(PermissionConstants.UserManage))
        {
            return Forbid();
        }

        var userExists = await _db.Users.AnyAsync(u => u.Id == id);
        if (!userExists)
        {
            return NotFound("User not found.");
        }

        var tags = await _db.Tags
            .AsNoTracking()
            .Where(t => t.UserId == id)
            .Select(t => new TagDto(t.Id, t.Name))
            .ToListAsync();

        return Ok(tags);
    }

    // Helper methods
    private bool TryGetCurrentUserId(out int userId)
    {
        userId = 0;
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return claim != null && int.TryParse(claim.Value, out userId);
    }

    private bool HasPermission(string permission)
    {
        return User.Claims.Any(c => c.Type == PermissionConstants.ClaimType && c.Value == permission);
    }

    private async Task LogAuditAsync(AuditAction action, int actorUserId, int targetUserId, string? details = null)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            Action = action,
            EntityType = "User",
            EntityId = targetUserId,
            UserId = actorUserId,
            Details = details ?? $"User {actorUserId} performed {action} on user {targetUserId}",
            Timestamp = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }

    private static UserProfileResponse ToProfile(User user) =>
        new(
            user.Id,
            user.Email ?? string.Empty,
            user.UserName,
            user.PhoneNumber,
            user.CreatedAt,
            user.UpdatedAt,
            user.LastLoginAt,
            user.EncryptedVaultKey,
            user.SubscriptionTierId
        );

    private static UserProfileResponse ToProfile(Managers.UserResponse user) =>
        new(
            user.Id,
            user.Email,
            user.UserName,
            user.PhoneNumber,
            user.CreatedAt,
            user.UpdatedAt,
            user.LastLoginAt,
            user.EncryptedVaultKey,
            user.SubscriptionTierId
        );
}

/// <summary>
/// Summary DTO for vault information in user context.
/// </summary>
public record VaultSummaryDto(int Id, string Name, string? Description, DateTime CreatedAt);
