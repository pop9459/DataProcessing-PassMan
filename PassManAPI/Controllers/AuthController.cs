using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using PassManAPI.Data;
using PassManAPI.DTOs;
using PassManAPI.Models;
using PassManAPI.Managers;
using PassManAPI.Services;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.EntityFrameworkCore;
using Google.Apis.Auth;

namespace PassManAPI.Controllers;

[ApiController]
[Route("api/auth")]
[Authorize]
public class AuthController : ControllerBase
{
    private const string DefaultRole = "VaultOwner";
    private readonly ApplicationDbContext _db;
    private readonly PassManAPI.Managers.UserManager _userManager;
    private readonly Microsoft.AspNetCore.Identity.UserManager<User> _identityUserManager;
    private readonly SignInManager<User> _signInManager;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly RoleManager<IdentityRole<int>> _roleManager;
    private readonly ILookupNormalizer _normalizer;

    public AuthController(
        ApplicationDbContext db,
        PassManAPI.Managers.UserManager userManager,
        Microsoft.AspNetCore.Identity.UserManager<User> identityUserManager,
        SignInManager<User> signInManager,
        IJwtTokenService jwtTokenService,
        RoleManager<IdentityRole<int>> roleManager,
        ILookupNormalizer normalizer
    )
    {
        _db = db;
        _userManager = userManager;
        _identityUserManager = identityUserManager;
        _signInManager = signInManager;
        _jwtTokenService = jwtTokenService;
        _roleManager = roleManager;
        _normalizer = normalizer;
    }

    /// <summary>
    /// Registers a new user and returns a JWT access token.
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
        if (!roleResult.Succeeded)
        {
            return BadRequest(string.Join(", ", roleResult.Errors.Select(e => e.Description)));
        }

        var token = _jwtTokenService.CreateAccessToken(
            result.Data.Id,
            result.Data.Email,
            result.Data.UserName
        );
        var response = new AuthResponse
        {
            AccessToken = token,
            User = ToProfile(result.Data)
        };
        return CreatedAtAction(nameof(GetCurrentUser), new { }, response);
    }

    /// <summary>
    /// Authenticates a user and returns a JWT access token.
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

        var email = request.Email.Trim();
        var user = await _identityUserManager.FindByEmailAsync(email);
        if (user is null)
        {
            return Unauthorized("Invalid credentials.");
        }

        if (!await _identityUserManager.IsEmailConfirmedAsync(user))
        {
            return Unauthorized("Email not confirmed.");
        }

        var signInResult = await _signInManager.CheckPasswordSignInAsync(
            user,
            request.Password,
            lockoutOnFailure: true
        );

        if (signInResult.IsLockedOut)
        {
            return StatusCode(StatusCodes.Status423Locked, "Account is locked. Try again later.");
        }

        if (!signInResult.Succeeded)
        {
            return Unauthorized("Invalid credentials.");
        }

        user.LastLoginAt = DateTime.UtcNow;
        await _identityUserManager.UpdateAsync(user);

        var token = _jwtTokenService.CreateAccessToken(user);
        return Ok(new AuthResponse
        {
            AccessToken = token,
            User = ToProfile(user)
        });
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
                    NormalizedUserName = (payload.Name ?? payload.Email.Split('@')[0]).ToUpperInvariant(),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    LastLoginAt = DateTime.UtcNow,
                    EmailConfirmed = true,
                    SubscriptionTierId = SubscriptionTier.DefaultTiers.First(t => t.Name == "Free").Id
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

            var token = _jwtTokenService.CreateAccessToken(user);
            return Ok(new AuthResponse
            {
                AccessToken = token,
                User = ToProfile(user)
            });

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
    /// Returns the current user's profile using JWT claims.
    /// </summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetCurrentUser()
    {
        // Resolve user id from JWT claims.
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await _userManager.GetUserByIdAsync(userId.Value);
        if (!result.Success || result.Data is null)
        {
            // User no longer exists - their token is no longer valid
            return Unauthorized("User not found or token invalid.");
        }

        return Ok(ToProfile(result.Data));
    }

    /// <summary>
    /// Updates the current user's profile (email/username/phone/encrypted key).
    /// </summary>
    [HttpPut("me")]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateProfile(
        [FromBody] UpdateProfileRequest request
    )
    {
        // Resolve user id from JWT claims.
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
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
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeleteAccount()
    {
        // Resolve user id from JWT claims.
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await _userManager.DeleteUserAsync(userId.Value);
        if (!result.Success)
        {
            return NotFound(result.Error ?? "User not found.");
        }

        return NoContent();
    }

    private static UserProfileResponse ToProfile(User user) =>
        new()
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            UserName = user.UserName,
            PhoneNumber = user.PhoneNumber,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,
            LastLoginAt = user.LastLoginAt,
            EncryptedVaultKey = user.EncryptedVaultKey,
            SubscriptionTierId = user.SubscriptionTierId
        };

    private static UserProfileResponse ToProfile(Managers.UserResponse user) =>
        new()
        {
            Id = user.Id,
            Email = user.Email,
            UserName = user.UserName,
            PhoneNumber = user.PhoneNumber,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,
            LastLoginAt = user.LastLoginAt,
            EncryptedVaultKey = user.EncryptedVaultKey,
            SubscriptionTierId = user.SubscriptionTierId
        };

    // Reads the authenticated user id from standard JWT claims.
    private int? GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier) ??
                    User.FindFirst(JwtRegisteredClaimNames.Sub);

        if (claim is null)
        {
            return null;
        }

        return int.TryParse(claim.Value, out var userId) ? userId : null;
    }

    /// <summary>
    /// Gets all permissions for the current user based on their roles.
    /// </summary>
    [HttpGet("permissions")]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetPermissions()
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var identityUser = await _identityUserManager.FindByIdAsync(userId.Value.ToString());
        if (identityUser == null)
        {
            return Unauthorized();
        }

        var roles = await _identityUserManager.GetRolesAsync(identityUser);
        var permissions = new HashSet<string>();

        foreach (var roleName in roles)
        {
            var role = await _roleManager.FindByNameAsync(roleName);
            if (role != null)
            {
                var roleClaims = await _roleManager.GetClaimsAsync(role);
                foreach (var claim in roleClaims.Where(c => c.Type == "permission"))
                {
                    permissions.Add(claim.Value);
                }
            }
        }

        return Ok(permissions.ToList());
    }

    /// <summary>
    /// Assigns a role to a user, replacing any existing roles (Admin only).
    /// </summary>
    [HttpPost("assign-role")]
    [Authorize(Policy = PermissionConstants.RoleManage)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignRole([FromBody] AssignRoleRequest request)
    {
        var identityUser = await _identityUserManager.FindByIdAsync(request.UserId.ToString());
        if (identityUser == null)
        {
            return NotFound("User not found.");
        }

        var role = await _roleManager.FindByNameAsync(request.RoleName);
        if (role == null)
        {
            return BadRequest($"Role '{request.RoleName}' does not exist.");
        }

        // Remove existing roles before assigning the new one
        var currentRoles = await _identityUserManager.GetRolesAsync(identityUser);
        if (currentRoles.Any())
        {
            await _identityUserManager.RemoveFromRolesAsync(identityUser, currentRoles);
        }

        var result = await AddUserToRoleAsync(identityUser, request.RoleName);
        if (!result.Succeeded)
        {
            return BadRequest(string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        return Ok(new { Message = $"User assigned to role '{request.RoleName}' successfully." });
    }

    /// <summary>
    /// Helper: add user to role, creating the role if missing.
    /// </summary>
    private async Task<IdentityResult> AddUserToRoleAsync(User user, string roleName)
    {
        if (!await _roleManager.RoleExistsAsync(roleName))
        {
            await _roleManager.CreateAsync(new IdentityRole<int>(roleName));
        }
        return await _identityUserManager.AddToRoleAsync(user, roleName);
    }
}

/// <summary>
/// Request for assigning a role to a user.
/// </summary>
public record AssignRoleRequest
{
    public int UserId { get; init; }
    
    [System.Text.Json.Serialization.JsonPropertyName("role")]
    public string RoleName { get; init; } = string.Empty;
}

/// <summary>
/// Request for Google OAuth login.
/// </summary>
public class GoogleLoginRequest
{
    public string IdToken { get; set; } = string.Empty;
}
