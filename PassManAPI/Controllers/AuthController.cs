using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PassManAPI.Data;
using PassManAPI.DTOs;
using PassManAPI.Models;
using PassManAPI.Managers;
using Google.Apis.Auth;
using System.Security.Claims;

namespace PassManAPI.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private const string DefaultRole = "VaultOwner";
    private readonly ApplicationDbContext _db;
    private readonly UserManager _userManager;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly ILookupNormalizer _normalizer;
    private readonly Microsoft.AspNetCore.Identity.UserManager<User> _identityUserManager;
    private readonly RoleManager<IdentityRole<int>> _roleManager;

    public AuthController(
        ApplicationDbContext db,
        UserManager userManager,
        IPasswordHasher<User> passwordHasher,
        ILookupNormalizer normalizer,
        Microsoft.AspNetCore.Identity.UserManager<User> identityUserManager,
        RoleManager<IdentityRole<int>> roleManager
    )
    {
        _db = db;
        _userManager = userManager;
        _passwordHasher = passwordHasher;
        _normalizer = normalizer;
        _identityUserManager = identityUserManager;
        _roleManager = roleManager;
    }

    /// <summary>
    /// Registers a new user. Returns a placeholder token until JWT is added.
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await _userManager.CreateUserAsync(
            new CreateUserRequest(
                request.Email,
                request.Password,
                request.UserName,
                request.PhoneNumber,
                request.EncryptedVaultKey
            )
        );

        if (!result.Success || result.Data is null)
        {
            return BadRequest(result.Error ?? "Registration failed.");
        }

        // Assign default role so authorization policies can be exercised.
        var identityUser = await _identityUserManager.FindByIdAsync(result.Data.Id.ToString());
        if (identityUser is null)
        {
            return BadRequest("User not found after creation.");
        }

        var roleResult = await AddUserToRoleAsync(identityUser, DefaultRole);
        if (!roleResult.Success)
        {
            return BadRequest(roleResult.Error);
        }

        var token = $"dev-token-{result.Data.Id}";
        var response = new AuthResponse(token, ToProfile(result.Data));
        return CreatedAtAction(nameof(GetCurrentUser), new { }, response);
    }

    /// <summary>
    /// Authenticates a user and returns a placeholder token.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var normalizedEmail = _normalizer.NormalizeEmail(request.Email.Trim()) ?? request.Email.Trim().ToUpperInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail);
        if (user is null || string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            return Unauthorized("Invalid credentials.");
        }

        var verify = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verify == PasswordVerificationResult.Failed)
        {
            return Unauthorized("Invalid credentials.");
        }

        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var token = $"dev-token-{user.Id}";
        return Ok(new AuthResponse(token, ToProfile(user)));
    }

    /// <summary>
    /// Authenticates a user via Google Id Token.
    /// </summary>
    [HttpPost("google")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request)
    {
        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings()
            {
               // Audience = new List<string> { "<YOUR_CLIENT_ID>" } // For production security
            };

            var payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken, settings);
            
            var normalizedEmail = _normalizer.NormalizeEmail(payload.Email) ?? payload.Email.ToUpperInvariant();
            var user = await _db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail);

            if (user == null)
            {
                // Register new user
                user = new User
                {
                    UserName = payload.Name ?? payload.Email.Split('@')[0],
                    Email = payload.Email,
                    NormalizedEmail = normalizedEmail,
                    NormalizedUserName = payload.Name?.ToUpperInvariant(),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    LastLoginAt = DateTime.UtcNow,
                    EmailConfirmed = true
                };

                _db.Users.Add(user);
                await _db.SaveChangesAsync();

                // Assign default role for new Google signups.
                var identityUser = await _identityUserManager.FindByIdAsync(user.Id.ToString());
                if (identityUser != null)
                {
                    await AddUserToRoleAsync(identityUser, DefaultRole);
                }
            }
            else
            {
                user.LastLoginAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }

            var token = $"dev-token-{user.Id}";
            return Ok(new AuthResponse(token, ToProfile(user)));

        }
        catch (InvalidJwtException ex)
        {
             return BadRequest($"Invalid Google Token: {ex.Message}");
        }
        catch (Exception ex)
        {
             return BadRequest($"Google Login Failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Returns the current user's profile using a dev-only X-UserId header.
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetCurrentUser([FromHeader(Name = "X-UserId")] int? userId)
    {
        if (userId is null)
        {
            return Unauthorized("Missing X-UserId header (dev placeholder auth).");
        }

        var result = await _userManager.GetUserByIdAsync(userId.Value);
        if (!result.Success || result.Data is null)
        {
            return NotFound("User not found.");
        }

        return Ok(ToProfile(result.Data));
    }

    /// <summary>
    /// Updates the current user's profile (email/username/phone/encrypted key).
    /// </summary>
    [HttpPut("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateProfile(
        [FromHeader(Name = "X-UserId")] int? userId,
        [FromBody] UpdateProfileRequest request
    )
    {
        if (userId is null)
        {
            return Unauthorized("Missing X-UserId header (dev placeholder auth).");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await _userManager.UpdateUserAsync(
            userId.Value,
            new UpdateUserRequest(
                request.Email,
                request.UserName,
                request.PhoneNumber,
                request.EncryptedVaultKey
            )
        );

        if (!result.Success || result.Data is null)
        {
            return BadRequest(result.Error ?? "Update failed.");
        }

        return Ok(ToProfile(result.Data));
    }

    /// <summary>
    /// Deletes the current user's account.
    /// </summary>
    [HttpDelete("me")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeleteAccount([FromHeader(Name = "X-UserId")] int? userId)
    {
        if (userId is null)
        {
            return Unauthorized("Missing X-UserId header (dev placeholder auth).");
        }

        var result = await _userManager.DeleteUserAsync(userId.Value);
        if (!result.Success)
        {
            return NotFound(result.Error ?? "User not found.");
        }

        return NoContent();
    }

    /// <summary>
    /// Returns the permissions for the current user (derived from role claims).
    /// </summary>
    [HttpGet("permissions")]
    [Authorize]
    [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
    public IActionResult GetPermissions()
    {
        var permissions = User.Claims
            .Where(c => c.Type == PermissionConstants.ClaimType)
            .Select(c => c.Value)
            .Distinct()
            .OrderBy(c => c)
            .ToArray();

        return Ok(permissions);
    }

    /// <summary>
    /// Assigns a single role to a user (admin-only).
    /// </summary>
    [HttpPost("assign-role")]
    [Authorize(Policy = PermissionConstants.RoleManage)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignRole([FromBody] AssignRoleRequest request)
    {
        var user = await _identityUserManager.FindByIdAsync(request.UserId.ToString());
        if (user is null)
        {
            return NotFound($"User {request.UserId} not found.");
        }

        var exists = await _roleManager.RoleExistsAsync(request.Role);
        if (!exists)
        {
            return BadRequest($"Role '{request.Role}' does not exist.");
        }

        var currentRoles = await _identityUserManager.GetRolesAsync(user);
        if (currentRoles.Count > 0)
        {
            var removeResult = await _identityUserManager.RemoveFromRolesAsync(user, currentRoles);
            if (!removeResult.Succeeded)
            {
                return BadRequest($"Failed to remove existing roles: {string.Join(", ", removeResult.Errors.Select(e => e.Description))}");
            }
        }

        var addResult = await _identityUserManager.AddToRoleAsync(user, request.Role);
        if (!addResult.Succeeded)
        {
            return BadRequest($"Failed to assign role: {string.Join(", ", addResult.Errors.Select(e => e.Description))}");
        }

        var actorId = GetCurrentUserId();
        if (actorId.HasValue)
        {
            await LogAuditAsync(AuditAction.UserRoleChanged, actorId.Value, $"Assigned role '{request.Role}' to user {request.UserId}");
        }

        return Ok(new { request.UserId, Role = request.Role });
    }

    private async Task LogAuditAsync(AuditAction action, int actorUserId, string? details = null)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            Action = action,
            EntityType = "User",
            EntityId = actorUserId,
            UserId = actorUserId,
            Details = details,
            Timestamp = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }

    private int? GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return claim != null && int.TryParse(claim.Value, out var id) ? id : null;
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

    private async Task<(bool Success, string? Error)> AddUserToRoleAsync(User user, string roleName)
    {
        var exists = await _roleManager.RoleExistsAsync(roleName);
        if (!exists)
        {
            return (false, $"Role '{roleName}' does not exist.");
        }

        var result = await _identityUserManager.AddToRoleAsync(user, roleName);
        if (!result.Succeeded)
        {
            return (false, string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        return (true, null);
    }

    public class AssignRoleRequest
    {
        [Required]
        public int UserId { get; set; }

        [Required]
        [StringLength(100)]
        public string Role { get; set; } = string.Empty;
    }
}
