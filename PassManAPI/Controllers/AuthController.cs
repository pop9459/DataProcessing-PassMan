using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PassManAPI.Data;
using PassManAPI.DTOs;
using PassManAPI.Models;
using PassManAPI.Managers;
using PassManAPI.Services;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace PassManAPI.Controllers;

[ApiController]
[Route("api/auth")]
[Authorize]
public class AuthController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager _userManager;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly ILookupNormalizer _normalizer;
    private readonly IJwtTokenService _jwtTokenService;

    public AuthController(
        ApplicationDbContext db,
        UserManager userManager,
        IPasswordHasher<User> passwordHasher,
        ILookupNormalizer normalizer,
        IJwtTokenService jwtTokenService
    )
    {
        _db = db;
        _userManager = userManager;
        _passwordHasher = passwordHasher;
        _normalizer = normalizer;
        _jwtTokenService = jwtTokenService;
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

        var token = _jwtTokenService.CreateAccessToken(
            result.Data.Id,
            result.Data.Email,
            result.Data.UserName
        );
        var response = new AuthResponse(token, ToProfile(result.Data));
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

        var token = _jwtTokenService.CreateAccessToken(user);
        return Ok(new AuthResponse(token, ToProfile(user)));
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
            return NotFound("User not found.");
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
        new(
            user.Id,
            user.Email ?? string.Empty,
            user.UserName,
            user.PhoneNumber,
            user.CreatedAt,
            user.UpdatedAt,
            user.LastLoginAt,
            user.EncryptedVaultKey
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
            user.EncryptedVaultKey
        );

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
}
